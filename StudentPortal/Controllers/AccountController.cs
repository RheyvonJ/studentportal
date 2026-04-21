using Microsoft.AspNetCore.Mvc;
using StudentPortal.DTO;
using StudentPortal.Models;
using StudentPortal.Services;
using BCrypt.Net;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StudentPortal.Controllers
{
    public class AccountController : Controller
    {
        private readonly MongoDbService _mongoService;
        private readonly EmailService _emailService;

        public AccountController(MongoDbService mongoService, EmailService emailService)
        {
            _mongoService = mongoService;
            _emailService = emailService;
        }

        // --- LOGIN ---
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            try
            {
                // Best-effort "last updated" timestamp for the running deployment.
                // On Railway/Linux containers this typically reflects the deployed build output.
                var asm = Assembly.GetExecutingAssembly();
                var path = asm.Location;
                if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
                {
                    var lastWriteUtc = System.IO.File.GetLastWriteTimeUtc(path);
                    ViewBag.LastUpdatedUtc = lastWriteUtc.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
                }
                else
                {
                    ViewBag.LastUpdatedUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
                }
            }
            catch
            {
                ViewBag.LastUpdatedUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
            }

            // Preserve deep-link so after login we can redirect back.
            ViewBag.ReturnUrl = returnUrl;

            // Pre-fill model with returnUrl for hidden field binding.
            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                return View(new LoginViewModel { ReturnUrl = returnUrl });
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "Please enter both email and password.";
                return View(model);
            }

            static bool IsSafeLocalReturnUrl(string? url)
            {
                if (string.IsNullOrWhiteSpace(url)) return false;
                // Only allow local paths (prevent open redirects)
                return Uri.TryCreate(url, UriKind.Relative, out _)
                       && url.StartsWith("/", StringComparison.Ordinal)
                       && !url.StartsWith("//", StringComparison.Ordinal);
            }

            // Normalize email for consistent lookup
            var normalizedEmail = (model.Email ?? string.Empty).Trim().ToLowerInvariant();
            
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                ViewBag.Error = "Please enter a valid email address.";
                return View(model);
            }

            // ALWAYS check enrollment system FIRST for student authentication
            // This ensures enrollment system is the source of truth for students
            EnrollmentStudent? enrollmentStudent = null;
            try
            {
                enrollmentStudent = await _mongoService.GetEnrollmentStudentByEmailAsync(normalizedEmail);
            }
            catch (Exception)
            {
                // Continue to check portal users for non-student roles
            }
            
            if (enrollmentStudent != null)
            {
                // EnrollmentSystem is the source of truth for whether a student is allowed to log in.
                // IMPORTANT: `deactivatedAt` / `InactivatedAt` may remain set as *historical* fields after reactivation.
                // If `IsActive` is explicitly true, trust that over timestamps.
                var activePascal = enrollmentStudent.IsActive;
                var activeCamel = enrollmentStudent.IsActiveCamel;

                // If both flags exist and disagree, treat "true" as authoritative (reactivated accounts).
                bool? activeResolved = null;
                if (activePascal.HasValue && activeCamel.HasValue && activePascal.Value != activeCamel.Value)
                    activeResolved = activePascal.Value || activeCamel.Value;
                else if (activePascal.HasValue) activeResolved = activePascal.Value;
                else if (activeCamel.HasValue) activeResolved = activeCamel.Value;

                var isActiveTrue = activeResolved == true;
                var isActiveFalse = activeResolved == false;

                var isExplicitlyInactive =
                    isActiveFalse
                    || (
                        !isActiveTrue
                        && (
                            enrollmentStudent.DeactivatedAt.HasValue
                            || enrollmentStudent.InactivatedAt.HasValue
                        )
                    );

                if (isExplicitlyInactive)
                {
                    var reason = (enrollmentStudent.DeactivationReason ?? enrollmentStudent.InactiveReason ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(reason))
                    {
                        ViewBag.Error = "Your account has been deactivated. Please contact the administrator.";
                    }
                    else
                    {
                        ViewBag.Error = $"Your account has been deactivated. Reason: {reason}";
                    }
                    return View(model);
                }

                // Some EnrollmentSystem records expose a status string separate from AccountStatus (e.g., enrollmentStatus).
                // Treat any non-enrolled/non-active status as blocked.
                if (!string.IsNullOrWhiteSpace(enrollmentStudent.EnrollmentStatus))
                {
                    var es = enrollmentStudent.EnrollmentStatus.Trim();
                    var esLower = es.ToLowerInvariant();
                    if (esLower is "inactive" or "deactivated" or "blocked" or "suspended")
                    {
                        // Same rationale as AccountStatus: if the account is explicitly active again,
                        // don't let a stale enrollmentStatus string block login after reactivation/re-enrollment.
                        if (!isActiveTrue)
                        {
                            var reason = (enrollmentStudent.DeactivationReason ?? enrollmentStudent.InactiveReason ?? string.Empty).Trim();
                            ViewBag.Error = string.IsNullOrWhiteSpace(reason)
                                ? $"Your account is {es}. Please contact the administrator."
                                : $"Your account is {es}. Reason: {reason}";
                            return View(model);
                        }
                    }
                }

                // Check account status (if it exists in the database)
                // Allow both Active and Pending to log in (for students and teachers),
                // block any other explicit status (e.g., Inactive, Blocked, etc.).
                if (!string.IsNullOrEmpty(enrollmentStudent.AccountStatus))
                {
                    var status = enrollmentStudent.AccountStatus.Trim();
                    var statusLower = status.ToLowerInvariant();
                    if (statusLower != "active" && statusLower != "pending")
                    {
                        // If EnrollmentSystem explicitly marks the account active, don't let a stale AccountStatus block login.
                        if (!isActiveTrue)
                        {
                            ViewBag.Error = $"Your account is {status}. Please contact the administrator.";
                            return View(model);
                        }
                    }
                }

                // Get or create portal user for lockout tracking
                var portalUser = await _mongoService.GetUserByEmailAsync(enrollmentStudent.Email);
                
                // Check if account is locked - use UTC consistently
                if (portalUser != null && portalUser.LockoutEndTime.HasValue && portalUser.LockoutEndTime.Value > DateTime.UtcNow)
                {
                    var remaining = portalUser.LockoutEndTime.Value - DateTime.UtcNow;
                    ViewBag.Error = $"Your account is locked. Try again in {remaining.Minutes} minute(s).";
                    return View(model);
                }

                // Verify password using PasswordHash from enrollment system
                bool passwordValid = false;
                try
                {
                    passwordValid = BCrypt.Net.BCrypt.Verify(model.Password, enrollmentStudent.PasswordHash);
                }
                catch (Exception)
                {
                    ViewBag.Error = "Invalid email or password.";
                    return View(model);
                }

                if (!passwordValid)
                {
                    // Track failed attempts in portal user record
                    if (portalUser == null)
                    {
                        // Create portal user for tracking failed attempts
                        portalUser = await _mongoService.CreateUserFromEnrollmentStudentAsync(enrollmentStudent);
                    }
                    
                    portalUser.FailedLoginAttempts = (portalUser.FailedLoginAttempts ?? 0) + 1;
                    
                    // Lock after 3 failed attempts
                    if (portalUser.FailedLoginAttempts >= 3)
                    {
                        portalUser.LockoutEndTime = DateTime.UtcNow.AddMinutes(3);
                        portalUser.FailedLoginAttempts = 0; // reset after lock
                        await _mongoService.UpdateUserLoginStatusAsync(portalUser.Email, portalUser.FailedLoginAttempts, portalUser.LockoutEndTime);
                        ViewBag.Error = "Too many failed attempts. Your account is locked for 3 minutes.";
                    }
                    else
                    {
                        await _mongoService.UpdateUserLoginStatusAsync(portalUser.Email, portalUser.FailedLoginAttempts, null);
                        ViewBag.Error = $"Incorrect email or password. {3 - portalUser.FailedLoginAttempts} attempt(s) left before suspension.";
                    }
                    
                    return View(model);
                }

                // Password is valid - sync user from enrollment to portal system
                portalUser = await _mongoService.CreateUserFromEnrollmentStudentAsync(enrollmentStudent);
                
                if (portalUser == null)
                {
                    ViewBag.Error = "Unable to complete login. Please try again.";
                    return View(model);
                }

                // Clear lockout if time has passed
                if (portalUser.LockoutEndTime.HasValue && portalUser.LockoutEndTime.Value <= DateTime.UtcNow)
                {
                    portalUser.LockoutEndTime = null;
                    portalUser.FailedLoginAttempts = 0;
                    await _mongoService.UpdateUserLoginStatusAsync(portalUser.Email, 0, null);
                }

                // Password correct - reset security fields and sync password hash
                portalUser.FailedLoginAttempts = 0;
                portalUser.LockoutEndTime = null;
                portalUser.Password = enrollmentStudent.PasswordHash; // Always sync password hash
                portalUser.IsVerified = true; // Ensure user is verified
                portalUser.Role = "Student"; // Ensure role is Student
                await _mongoService.UpdateUserAsync(portalUser);

                // ✅ Successful login from enrollment system
                // Build display name from FirstName, MiddleName, LastName, or fallback to Username
                string displayName = enrollmentStudent.Username;
                if (portalUser != null)
                {
                    var nameParts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(portalUser.FirstName)) nameParts.Add(portalUser.FirstName);
                    if (!string.IsNullOrWhiteSpace(portalUser.MiddleName)) nameParts.Add(portalUser.MiddleName);
                    if (!string.IsNullOrWhiteSpace(portalUser.LastName)) nameParts.Add(portalUser.LastName);
                    
                    if (nameParts.Count > 0)
                    {
                        displayName = string.Join(" ", nameParts);
                    }
                    else if (!string.IsNullOrWhiteSpace(portalUser.FullName))
                    {
                        displayName = portalUser.FullName;
                    }
                    else
                    {
                        // Fallback directly from EnrollmentSystem record (SHSStudents)
                        var enParts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(enrollmentStudent.FirstName)) enParts.Add(enrollmentStudent.FirstName);
                        if (!string.IsNullOrWhiteSpace(enrollmentStudent.MiddleName) && !string.Equals(enrollmentStudent.MiddleName.Trim(), "NA", StringComparison.OrdinalIgnoreCase))
                            enParts.Add(enrollmentStudent.MiddleName);
                        if (!string.IsNullOrWhiteSpace(enrollmentStudent.LastName)) enParts.Add(enrollmentStudent.LastName);
                        var built = string.Join(" ", enParts);
                        if (!string.IsNullOrWhiteSpace(built)) displayName = built;
                    }
                }
                
                // Save session data
                HttpContext.Session.SetString("UserEmail", enrollmentStudent.Email);
                HttpContext.Session.SetString("UserName", displayName);
                HttpContext.Session.SetString("UserRole", "Student");
                HttpContext.Session.SetString("UserId", portalUser.Id ?? enrollmentStudent.Id);
                HttpContext.Session.SetString("UserType", enrollmentStudent.Type);

                // Redirect back to deep link if provided
                if (IsSafeLocalReturnUrl(model.ReturnUrl))
                    return LocalRedirect(model.ReturnUrl!);

                return RedirectToAction("Index", "StudentDb");
            }

            // Student not found in enrollment system - check ProfessorDB for professors
            Professor? professor = null;
            try
            {
                professor = await _mongoService.GetProfessorByEmailAsync(normalizedEmail);
            }
            catch (Exception)
            {
                // Continue to check portal users if ProfessorDB check fails
            }

            if (professor != null)
            {
                // Get professor email and name using helper methods
                var professorEmail = professor.GetEmail();
                var professorName = professor.GetFullName();
                
                if (string.IsNullOrEmpty(professorEmail))
                {
                    ViewBag.Error = "Professor account found but email is missing.";
                    return View(model);
                }

                // Get or create portal user for lockout tracking
                var portalUser = await _mongoService.GetUserByEmailAsync(professorEmail);
                
                // Check if account is locked
                if (portalUser != null && portalUser.LockoutEndTime.HasValue && portalUser.LockoutEndTime.Value > DateTime.UtcNow)
                {
                    var remaining = portalUser.LockoutEndTime.Value - DateTime.UtcNow;
                    ViewBag.Error = $"Your account is locked. Try again in {remaining.Minutes} minute(s).";
                    return View(model);
                }

                // Check if temporary password has expired
                if (professor.IsTemporaryPassword == true && professor.TempPasswordExpiresAt.HasValue)
                {
                    if (professor.TempPasswordExpiresAt.Value < DateTime.UtcNow)
                    {
                        ViewBag.Error = "Your temporary password has expired. Please contact the administrator to reset your password.";
                        return View(model);
                    }
                }

                // Determine password field (could be Password, PasswordHash, or plain text)
                var professorPasswordHash = professor.GetPasswordHash();
                var storedPassword = professorPasswordHash;
                
                // Debug logging
                Console.WriteLine($"[Login] Professor found - Email: {professorEmail}, HasPasswordHash: {!string.IsNullOrEmpty(professorPasswordHash)}, FullName: {professorName}");
                
                if (string.IsNullOrEmpty(storedPassword))
                {
                    ViewBag.Error = "Invalid email or password.";
                    return View(model);
                }

                // Verify password - try BCrypt first, then plain text comparison
                bool passwordValid = false;
                try
                {
                    // Try BCrypt verification first (most common)
                    passwordValid = BCrypt.Net.BCrypt.Verify(model.Password, storedPassword);
                }
                catch
                {
                    // If BCrypt fails, try plain text comparison (for unhashed passwords)
                    try
                    {
                        passwordValid = storedPassword.Equals(model.Password, StringComparison.Ordinal);
                    }
                    catch
                    {
                        passwordValid = false;
                    }
                }

                if (!passwordValid)
                {
                    // Track failed attempts in portal user record
                    if (portalUser == null)
                    {
                        // Create portal user in StudentDB for tracking failed attempts
                        // Hash the password if it's plain text for storage
                        var passwordToStore = storedPassword;
                        if (!storedPassword.StartsWith("$2") && !storedPassword.StartsWith("$2a") && !storedPassword.StartsWith("$2b"))
                        {
                            // Plain text password, hash it for storage
                            passwordToStore = BCrypt.Net.BCrypt.HashPassword(model.Password);
                        }
                        
                        // Sync professor to StudentDB Users collection with role "Professor"
                        portalUser = await _mongoService.CreateUserFromProfessorAsync(professor, passwordToStore);
                    }
                    
                    if (portalUser != null)
                    {
                        portalUser.FailedLoginAttempts = (portalUser.FailedLoginAttempts ?? 0) + 1;
                        
                        // Lock after 3 failed attempts
                        if (portalUser.FailedLoginAttempts >= 3)
                        {
                            portalUser.LockoutEndTime = DateTime.UtcNow.AddMinutes(3);
                            portalUser.FailedLoginAttempts = 0;
                            await _mongoService.UpdateUserLoginStatusAsync(portalUser.Email, portalUser.FailedLoginAttempts, portalUser.LockoutEndTime);
                            ViewBag.Error = "Too many failed attempts. Your account is locked for 3 minutes.";
                        }
                        else
                        {
                            await _mongoService.UpdateUserLoginStatusAsync(portalUser.Email, portalUser.FailedLoginAttempts, null);
                            ViewBag.Error = $"Incorrect email or password. {3 - portalUser.FailedLoginAttempts} attempt(s) left before suspension.";
                        }
                    }
                    else
                    {
                        ViewBag.Error = "Incorrect email or password.";
                    }
                    
                    return View(model);
                }

                // Password is valid - sync user from ProfessorDB to StudentDB Users collection
                // Hash the password if it's plain text for storage
                var finalPassword = storedPassword;
                if (!storedPassword.StartsWith("$2") && !storedPassword.StartsWith("$2a") && !storedPassword.StartsWith("$2b"))
                {
                    // Plain text password, hash it for storage
                    finalPassword = BCrypt.Net.BCrypt.HashPassword(model.Password);
                }

                // Sync professor to StudentDB Users collection with role "Professor"
                portalUser = await _mongoService.CreateUserFromProfessorAsync(professor, finalPassword);

                if (portalUser == null)
                {
                    ViewBag.Error = "Unable to complete login. Please try again.";
                    return View(model);
                }

                // Clear lockout if time has passed
                if (portalUser.LockoutEndTime.HasValue && portalUser.LockoutEndTime.Value <= DateTime.UtcNow)
                {
                    portalUser.LockoutEndTime = null;
                    portalUser.FailedLoginAttempts = 0;
                    await _mongoService.UpdateUserLoginStatusAsync(portalUser.Email, 0, null);
                }

                // Password correct - reset security fields
                portalUser.FailedLoginAttempts = 0;
                portalUser.LockoutEndTime = null;
                portalUser.Password = finalPassword;
                portalUser.IsVerified = true;
                portalUser.Role = "Professor"; // Ensure role is Professor
                if (!string.IsNullOrEmpty(professorName))
                {
                    portalUser.FullName = professorName;
                }
                await _mongoService.UpdateUserAsync(portalUser);

                // ✅ Successful login from ProfessorDB - now synced to StudentDB Users
                // Save session data
                HttpContext.Session.SetString("UserEmail", professorEmail);
                HttpContext.Session.SetString("UserName", !string.IsNullOrEmpty(professorName) ? professorName : professorEmail);
                HttpContext.Session.SetString("UserRole", "Professor"); // Set role as Professor
                HttpContext.Session.SetString("UserId", portalUser.Id ?? professor.Id);

                if (IsSafeLocalReturnUrl(model.ReturnUrl))
                    return LocalRedirect(model.ReturnUrl!);

                return RedirectToAction("Index", "ProfessorDb");
            }

            // Not found in enrollment or ProfessorDB - check portal system ONLY for non-student users (admin/faculty)
            var user = await _mongoService.GetUserByEmailAsync(normalizedEmail);
            
            if (user == null)
            {
                ViewBag.Error = "No account found with this email. Students must be registered through the enrollment system.";
                return View(model);
            }

            // Only allow portal-only users if they are NOT students
            // Students MUST come from enrollment system
            if (user.Role?.ToLower() == "student")
            {
                ViewBag.Error = "Student accounts must be registered through the enrollment system. Please contact the administrator.";
                return View(model);
            }

            // For non-student users (admin/faculty), proceed with portal authentication

            // Check if user is temporarily locked
            if (user.LockoutEndTime.HasValue && user.LockoutEndTime.Value > DateTime.UtcNow)
            {
                var remaining = user.LockoutEndTime.Value - DateTime.UtcNow;
                ViewBag.Error = $"Your account is locked. Try again in {remaining.Minutes} minute(s).";
                return View(model);
            }

            // Clear lockout if time has passed
            if (user.LockoutEndTime.HasValue && user.LockoutEndTime.Value <= DateTime.UtcNow)
            {
                user.LockoutEndTime = null;
                user.FailedLoginAttempts = 0;
                await _mongoService.UpdateUserLoginStatusAsync(user.Email, 0, null);
            }

            // 🔒 Verify hashed password
            if (!BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
            {
                // Increment failed attempts
                user.FailedLoginAttempts = (user.FailedLoginAttempts ?? 0) + 1;

                // Lock after 3 failed attempts
                if (user.FailedLoginAttempts >= 3)
                {
                    user.LockoutEndTime = DateTime.UtcNow.AddMinutes(3);
                    user.FailedLoginAttempts = 0; // reset after lock
                    await _mongoService.UpdateUserLoginStatusAsync(user.Email, user.FailedLoginAttempts, user.LockoutEndTime);
                    ViewBag.Error = "Too many failed attempts. Your account is locked for 3 minutes.";
                }
                else
                {
                    await _mongoService.UpdateUserLoginStatusAsync(user.Email, user.FailedLoginAttempts, null);
                    ViewBag.Error = $"Incorrect email or password. {3 - user.FailedLoginAttempts} attempt(s) left before suspension.";
                }

                return View(model);
            }

            // Reset failed attempts after successful login
            user.FailedLoginAttempts = 0;
            user.LockoutEndTime = null;
            await _mongoService.UpdateUserLoginStatusAsync(user.Email, 0, null);

            // 🔐 Verify if account is activated
            if (!user.IsVerified)
            {
                ViewBag.Error = "Your account is not verified yet. Please check your email for OTP.";
                return View(model);
            }

            // ✅ Build display name from FirstName, MiddleName, LastName, or fallback to FullName/Email
            string userDisplayName = user.Email;
            var userNameParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(user.FirstName)) userNameParts.Add(user.FirstName);
            if (!string.IsNullOrWhiteSpace(user.MiddleName)) userNameParts.Add(user.MiddleName);
            if (!string.IsNullOrWhiteSpace(user.LastName)) userNameParts.Add(user.LastName);
            
            if (userNameParts.Count > 0)
            {
                userDisplayName = string.Join(" ", userNameParts);
            }
            else if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                userDisplayName = user.FullName;
            }
            
            // Save session data
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserName", userDisplayName);
            HttpContext.Session.SetString("UserRole", user.Role ?? "Student");

            switch (user.Role?.ToLower())
            {
                case "admin":
                case "faculty":
                    return RedirectToAction("Index", "AdminDb");
                case "professor":
                    return RedirectToAction("Index", "ProfessorDb");
                default:
                    if (IsSafeLocalReturnUrl(model.ReturnUrl))
                        return LocalRedirect(model.ReturnUrl!);
                    return RedirectToAction("Index", "StudentDb");
            }
        }

        public IActionResult Index()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userEmail))
                return RedirectToAction("Login", "Account");

            if (userRole != "Admin")
                return RedirectToAction("Dashboard");

            ViewBag.UserName = userEmail;
            return View();
        }

        [HttpGet]
        public IActionResult Logout()
        {
            // ✅ Clear all session data
            HttpContext.Session.Clear();

            // ✅ Redirect to Login page
            return RedirectToAction("Login", "Account");
        }

        // --- REGISTER ---
        [HttpGet]
        public IActionResult Register() => View();

        // STEP 2: SEND OTP
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendOtp(RegisterPayload model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Invalid registration data." });

            // ✅ Check if user already exists
            var existingUser = await _mongoService.GetUserByEmailAsync(model.Email);
            if (existingUser != null)
            {
                return Json(new { success = false, message = "Email already exists. Please use another one." });
            }

            // Generate OTP
            var otp = new Random().Next(100000, 999999).ToString();

            // Hash password before saving
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password);

            // Create a new unverified user record (Student by default)
            await _mongoService.CreateUserAsync(model.Email, hashedPassword, otp, "Student");

            try
            {
                await _emailService.SendEmailAsync(
                    model.Email,
                    "MySUQC OTP Verification",
                    $"Your OTP code is: {otp}"
                );

                return Json(new { success = true, message = "OTP sent successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Email send failed: " + ex.Message);
                return Json(new { success = false, message = "We couldn't send the OTP email. Please try again." });
            }
        }

        // STEP 3: VERIFY OTP
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(OtpPayload payload)
        {
            var isValid = await _mongoService.VerifyOtpAsync(payload.EmailAddress, payload.OTP);

            if (!isValid)
                return Json(new { success = false, message = "Invalid OTP. Please try again." });

            await _mongoService.MarkUserAsVerifiedAsync(payload.EmailAddress);
            return Json(new { success = true, message = "Your account has been verified!" });
        }

        // STEP 3B: RESEND OTP
        [HttpPost]
        public async Task<IActionResult> ResendOtp(string email)
        {
            if (string.IsNullOrEmpty(email))
                return Json(new { success = false, message = "Email is required." });

            var user = await _mongoService.GetUserByEmailAsync(email);
            if (user == null)
                return Json(new { success = false, message = "User not found." });

            if (user.IsVerified)
                return Json(new { success = false, message = "Account already verified." });

            var otp = new Random().Next(100000, 999999).ToString();
            await _mongoService.UpdateOtpAsync(email, otp);

            try
            {
                await _emailService.SendEmailAsync(
                    email,
                    "MySUQC OTP Verification (Resent)",
                    $"Your new OTP code is: {otp}"
                );

                return Json(new { success = true, message = "OTP resent successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Resend OTP failed: " + ex.Message);
                return Json(new { success = false, message = "Failed to resend OTP. Try again later." });
            }
        }

        // --- FORGOT PASSWORD ---
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> SendVerificationCode([FromBody] ForgotPasswordViewModel model)
        {
            if (string.IsNullOrEmpty(model.Email))
                return BadRequest("Email is required.");

            var user = await _mongoService.GetUserByEmailAsync(model.Email);
            if (user == null)
                return NotFound("No account found with that email.");

            var code = new Random().Next(100000, 999999).ToString();
            HttpContext.Session.SetString("VerificationCode", code);
            HttpContext.Session.SetString("ResetEmail", model.Email);

            await _emailService.SendEmailAsync(model.Email, "Password Reset Code", $"Your verification code is: {code}");

            return Ok("Verification code sent.");
        }

        [HttpPost]
        public IActionResult VerifyCode([FromBody] ForgotPasswordViewModel model)
        {
            var storedCode = HttpContext.Session.GetString("VerificationCode");
            if (storedCode == null)
                return BadRequest("Session expired. Please restart the reset process.");

            if (model.VerificationCode != storedCode)
                return BadRequest("Invalid verification code.");

            return Ok("Code verified successfully.");
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword([FromBody] ForgotPasswordViewModel model)
        {
            var email = HttpContext.Session.GetString("ResetEmail");
            if (email == null)
                return BadRequest("Session expired. Please restart the reset process.");

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            await _mongoService.UpdateUserPasswordAsync(email, hashedPassword);

            HttpContext.Session.Remove("VerificationCode");
            HttpContext.Session.Remove("ResetEmail");

            return Ok("Password has been reset successfully.");
        }
    }
}
