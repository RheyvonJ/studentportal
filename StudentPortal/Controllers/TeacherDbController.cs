using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using StudentPortal.Models;
using StudentPortal.Models.ProfessorDb;
using StudentPortal.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace StudentPortal.Controllers.TeacherDb
{
    [Route("teacherdb/[controller]")]
    public class TeacherDbController : Controller
    {
        private readonly MongoDbService _mongo;
        private readonly EmailService _email;
        private readonly IConfiguration _configuration;

        public TeacherDbController(MongoDbService mongo, EmailService email, IConfiguration configuration)
        {
            _mongo = mongo;
            _email = email;
            _configuration = configuration;
        }

        [HttpGet("Schedule")]
        public IActionResult Schedule()
        {
            var professorEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(professorEmail))
            {
                var returnUrl = $"{Request.Path}{Request.QueryString}";
                return RedirectToAction("Login", "Account", new { returnUrl });
            }

            var professorName = HttpContext.Session.GetString("UserName") ?? "Professor";
            var sessionRole = (HttpContext.Session.GetString("UserRole") ?? string.Empty).Trim();

            string GetInitials(string fullName)
            {
                if (string.IsNullOrWhiteSpace(fullName)) return "PR";
                var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return string.Concat(parts.Select(p => p[0])).ToUpper();
            }

            var vm = new ProfessorDashboardViewModel
            {
                ProfessorName = professorName,
                ProfessorInitials = GetInitials(professorName),
                Classes = new List<StudentPortal.Models.AdminDb.ClassItem>()
            };

            ViewBag.Role = string.IsNullOrWhiteSpace(sessionRole) ? "Teacher" : sessionRole;
            ViewBag.CurrentPage = "schedule";
            ViewBag.HideHeader = true;
            return View("~/Views/TeacherDb/TeacherDb/Schedule.cshtml", vm);
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var professorName = HttpContext.Session.GetString("UserName") ?? "Professor";
            var professorEmail = HttpContext.Session.GetString("UserEmail") ?? "";
            if (string.IsNullOrWhiteSpace(professorEmail))
            {
                var returnUrl = $"{Request.Path}{Request.QueryString}";
                return RedirectToAction("Login", "Account", new { returnUrl });
            }

            string GetInitials(string fullName)
            {
                if (string.IsNullOrWhiteSpace(fullName)) return "PR";
                var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return string.Concat(parts.Select(p => p[0])).ToUpper();
            }

            var classes = (await _mongo.GetClassesByOwnerEmailAsync(professorEmail)).ToList();
            await Task.WhenAll(classes.Select(async c =>
            {
                if (!string.IsNullOrWhiteSpace(c.ScheduleId))
                {
                    var (room, scheduleTime) = await _mongo.GetRoomAndScheduleTimeByScheduleIdAsync(c.ScheduleId);
                    c.RoomDisplay = room;
                    c.ScheduleTimeDisplay = scheduleTime;
                }
            }));

            var vm = new ProfessorDashboardViewModel
            {
                ProfessorName = professorName,
                ProfessorInitials = GetInitials(professorName),
                Classes = classes
            };

            ViewBag.Role = "Teacher";
            return View("~/Views/TeacherDb/TeacherDb/Index.cshtml", vm);
        }

        [HttpGet("Settings")]
        public async Task<IActionResult> Settings()
        {
            var professorEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(professorEmail))
            {
                var returnUrl = $"{Request.Path}{Request.QueryString}";
                return RedirectToAction("Login", "Account", new { returnUrl });
            }

            var professorName = HttpContext.Session.GetString("UserName") ?? "Professor";
            var initials = string.Concat(professorName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x[0]))
                .ToUpperInvariant();

            ViewBag.Role = "Teacher";
            ViewBag.CurrentPage = "settings";
            ViewBag.AdminName = professorName;
            ViewBag.AdminInitials = string.IsNullOrWhiteSpace(initials) ? "PR" : initials;
            ViewBag.AccountEmail = professorEmail;
            ViewBag.SecurityRole = HttpContext.Session.GetString("UserRole") ?? "Teacher";
            ViewBag.HasTemporaryPassword = false;
            try
            {
                var professor = await _mongo.GetProfessorByEmailAsync(professorEmail);
                ViewBag.HasTemporaryPassword = professor?.IsTemporaryPassword == true;
            }
            catch { }
            return View("~/Views/TeacherDb/TeacherDb/Settings.cshtml", new TeacherChangePasswordViewModel());
        }

        [HttpPost("ChangePassword")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(TeacherChangePasswordViewModel model)
        {
            var professorEmail = (HttpContext.Session.GetString("UserEmail") ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(professorEmail))
            {
                var returnUrl = $"{Request.Path}{Request.QueryString}";
                return RedirectToAction("Login", "Account", new { returnUrl });
            }

            var professorName = HttpContext.Session.GetString("UserName") ?? "Professor";
            var initials = string.Concat(professorName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x[0]))
                .ToUpperInvariant();

            ViewBag.Role = "Teacher";
            ViewBag.CurrentPage = "settings";
            ViewBag.AdminName = professorName;
            ViewBag.AdminInitials = string.IsNullOrWhiteSpace(initials) ? "PR" : initials;
            ViewBag.AccountEmail = professorEmail;
            ViewBag.SecurityRole = HttpContext.Session.GetString("UserRole") ?? "Teacher";
            ViewBag.HasTemporaryPassword = false;

            if (!ModelState.IsValid)
            {
                return View("~/Views/TeacherDb/TeacherDb/Settings.cshtml", model);
            }

            var professor = await _mongo.GetProfessorByEmailAsync(professorEmail);
            if (professor == null)
            {
                ModelState.AddModelError(string.Empty, "Unable to find your teacher account.");
                return View("~/Views/TeacherDb/TeacherDb/Settings.cshtml", model);
            }
            ViewBag.HasTemporaryPassword = professor.IsTemporaryPassword == true;

            var storedPassword = professor.GetPasswordHash();
            if (string.IsNullOrWhiteSpace(storedPassword))
            {
                ModelState.AddModelError(string.Empty, "Your account has no password configured.");
                return View("~/Views/TeacherDb/TeacherDb/Settings.cshtml", model);
            }

            bool currentPasswordIsValid;
            try
            {
                currentPasswordIsValid = BCrypt.Net.BCrypt.Verify(model.CurrentPassword, storedPassword);
            }
            catch
            {
                currentPasswordIsValid = string.Equals(storedPassword, model.CurrentPassword, StringComparison.Ordinal);
            }

            if (!currentPasswordIsValid)
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
                return View("~/Views/TeacherDb/TeacherDb/Settings.cshtml", model);
            }

            if (string.Equals(model.CurrentPassword, model.NewPassword, StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(model.NewPassword), "New password must be different from current password.");
                return View("~/Views/TeacherDb/TeacherDb/Settings.cshtml", model);
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            var changed = await _mongo.UpdateProfessorPasswordByEmailAsync(professorEmail, hashedPassword);
            if (!changed)
            {
                ModelState.AddModelError(string.Empty, "Unable to update password right now. Please try again.");
                return View("~/Views/TeacherDb/TeacherDb/Settings.cshtml", model);
            }

            TempData["ToastMessage"] = "Password updated successfully.";
            return RedirectToAction("Settings");
        }

        [HttpGet("GetMyAssignedSubjects")]
        public async Task<IActionResult> GetMyAssignedSubjects()
        {
            var professorEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(professorEmail))
                return Json(new { success = true, subjects = Array.Empty<object>() });

            var professor = await _mongo.GetProfessorByEmailAsync(professorEmail);
            var professorId = professor?.Id ?? string.Empty;
            var items = new List<MongoDB.Bson.BsonDocument>();
            if (string.IsNullOrWhiteSpace(professorId))
            {
                var sessionName = HttpContext.Session.GetString("UserName") ?? (professor?.GetFullName() ?? string.Empty);
                var byName = await _mongo.GetClassMeetingsForProfessorFlexibleAsync(null, sessionName, professorEmail);
                items = byName ?? new List<MongoDB.Bson.BsonDocument>();
            }
            else
            {
                items = await _mongo.GetProfessorAssignedSubjectsAsync(professorId) ?? new List<MongoDB.Bson.BsonDocument>();
                if (items.Count == 0)
                {
                    var byName = await _mongo.GetClassMeetingsForProfessorFlexibleAsync(professorId, professor?.GetFullName() ?? string.Empty, professorEmail);
                    items = byName ?? new List<MongoDB.Bson.BsonDocument>();
                }
            }

            var subjects = new List<object>();
            foreach (var d in items)
            {
                var section = d.TryGetValue("section", out var s) ? s.ToString() : string.Empty;
                var subjectCode = d.TryGetValue("subjectCode", out var c) ? c.ToString() : string.Empty;
                var units = d.TryGetValue("units", out var u) ? u.ToString() : string.Empty;
                var scheduleId = d.TryGetValue("scheduleId", out var sch) ? sch.ToString() : string.Empty;
                var subjectName = d.TryGetValue("subjectName", out var n) ? n.ToString() : string.Empty;
                var schoolYear = d.TryGetValue("schoolYear", out var sy) ? sy.ToString() : string.Empty;
                var semester = d.TryGetValue("semester", out var sem) ? sem.ToString() : string.Empty;
                if (string.IsNullOrWhiteSpace(semester) && d.TryGetValue("Semester", out var sem2)) semester = sem2.ToString();
                if (string.IsNullOrWhiteSpace(semester) && d.TryGetValue("term", out var term)) semester = term.ToString();
                if (string.IsNullOrWhiteSpace(semester) && d.TryGetValue("Term", out var term2)) semester = term2.ToString();
                var timeSlotDisplay = d.TryGetValue("timeSlotDisplay", out var ts) ? ts.ToString() : string.Empty;
                var roomName = d.TryGetValue("roomName", out var rn) ? rn.ToString() : string.Empty;
                var sectionIdVal = d.Contains("sectionId") ? d["sectionId"]?.ToString() ?? string.Empty
                    : (d.Contains("SectionId") ? d["SectionId"]?.ToString() ?? string.Empty
                    : (d.Contains("SectionID") ? d["SectionID"]?.ToString() ?? string.Empty : string.Empty));

                string classCode = string.Empty;
                try
                {
                    var existing = await _mongo.GetClassByScheduleIdAndOwnerAsync(scheduleId, professorEmail);
                    if (existing != null) classCode = existing.ClassCode ?? string.Empty;
                }
                catch { }

                subjects.Add(new
                {
                    section,
                    subjectCode,
                    units,
                    scheduleId,
                    subjectName,
                    schoolYear,
                    semester,
                    timeSlotDisplay,
                    roomName,
                    classCode,
                    sectionId = sectionIdVal
                });
            }

            return Json(new { success = true, subjects });
        }

        [HttpPost("CreateClass")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClass(string subjectName, string subjectCode, string section, string? schoolYear, string scheduleId, string? sectionId)
        {
            if (string.IsNullOrWhiteSpace(subjectName) ||
                string.IsNullOrWhiteSpace(subjectCode) ||
                string.IsNullOrWhiteSpace(section))
            {
                TempData["ToastMessage"] = "⚠️ Please fill all required fields.";
                return RedirectToAction("Index");
            }

            var professorName = HttpContext.Session.GetString("UserName") ?? "Professor";
            var professorEmail = HttpContext.Session.GetString("UserEmail") ?? "";

            string GetInitials(string fullName)
            {
                if (string.IsNullOrWhiteSpace(fullName)) return "PR";
                var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return string.Concat(parts.Select(p => p[0])).ToUpper();
            }

            var creatorInitials = GetInitials(professorName);

            var newClass = new StudentPortal.Models.AdminDb.ClassItem
            {
                SubjectName = subjectName,
                SubjectCode = subjectCode,
                ScheduleId = scheduleId ?? string.Empty,
                EnrollmentSectionId = string.IsNullOrWhiteSpace(sectionId) ? string.Empty : sectionId.Trim(),
                Section = section,
                SchoolYear = schoolYear ?? string.Empty,
                Course = string.Empty,
                Year = string.Empty,
                Semester = string.Empty,
                CreatorName = professorName,
                CreatorInitials = creatorInitials,
                CreatorRole = "Professor",
                OwnerEmail = professorEmail,
                ClassCode = _mongo.GenerateClassCode(),
                BackgroundImageUrl = ""
            };

            await _mongo.CreateClassAsync(newClass);

            // Resolve once on the request thread: Request/Url are not safe inside fire-and-forget Task.Run.
            var inviteEmailLogoUrl = ResolveSchoolLogoAbsoluteUrl();
            var inviteEmailClassUrl = ResolveClassAbsoluteUrl(newClass.ClassCode);

            // Run auto-invite in background so the UI doesn't "spin" forever
            _ = Task.Run(async () =>
            {
                try
                {
                    var recipients = await _mongo.GetStudentEmailsForClassAsync(newClass) ?? new List<string>();

                    var distinctRecipients = recipients
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Select(e => (e ?? string.Empty).Trim())
                        .Where(e => !string.IsNullOrWhiteSpace(e) && e.Contains("@"))
                        .Where(e => !string.Equals(e, professorEmail, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    Console.WriteLine($"[CreateClass] Auto-invite recipients for {newClass.ClassCode}: {distinctRecipients.Count}");

                    var subj = JoinClassEmailTemplate.DefaultSubject;
                    var courseOrSubject = !string.IsNullOrWhiteSpace(newClass.SubjectName) ? newClass.SubjectName : newClass.Course;
                    var schoolName = _configuration["Portal:SchoolName"] ?? "Sta. Lucia Senior High School";
                    var logoUrl = inviteEmailLogoUrl;
                    var classUrl = inviteEmailClassUrl;

                    // Also email the creator (teacher) with the class code + link.
                    try
                    {
                        var me = (professorEmail ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(me) && me.Contains("@"))
                        {
                            var meBody = JoinClassEmailTemplate.BuildHtml(professorName, courseOrSubject, newClass.ClassCode, schoolName, logoUrl, classUrl);
                            var (okMe, errMe) = await _email.SendEmailAsync(me, subj, meBody, isHtml: true);
                            if (!okMe)
                                Console.WriteLine($"[CreateClass] Creator email failed to {me}: {errMe}");
                        }
                    }
                    catch { }

                    foreach (var r in distinctRecipients)
                    {
                        string displayName = "Student";
                        try
                        {
                            var user = await _mongo.GetUserByEmailAsync(r);
                            if (user != null && !string.IsNullOrWhiteSpace(user.FullName))
                                displayName = user.FullName;
                        }
                        catch { }

                        try
                        {
                            var bodyHtml = JoinClassEmailTemplate.BuildHtml(displayName, courseOrSubject, newClass.ClassCode, schoolName, logoUrl, classUrl);
                            var (ok, err) = await _email.SendEmailAsync(r, subj, bodyHtml, isHtml: true);
                            if (!ok)
                                Console.WriteLine($"[CreateClass] Invite email failed to {r}: {err}");
                        }
                        catch { }

                        try
                        {
                            var existing = await _mongo.GetUserByEmailAsync(r);
                            if (existing == null)
                            {
                                var enrollmentStudent = await _mongo.GetEnrollmentStudentByEmailAsync(r);
                                if (enrollmentStudent != null)
                                    await _mongo.CreateUserFromEnrollmentStudentAsync(enrollmentStudent);
                            }
                            await _mongo.AddStudentToClass(r, newClass.ClassCode);
                            await _mongo.ApproveJoinRequestsByEmailAndClassCodeAsync(r, newClass.ClassCode);
                            await _mongo.AddNotificationAsync(new StudentPortal.Models.StudentDb.UserNotification
                            {
                                Email = r,
                                Type = "class-auto-joined",
                                Text = $"You have been auto-joined to \"{newClass.SubjectName}\" ({newClass.ClassCode})",
                                Code = newClass.ClassCode,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                        catch { }
                    }
                }
                catch { }
            });

            TempData["ToastMessage"] = $"✅ Class \"{subjectName}\" created (Code: {newClass.ClassCode}). Invites are sending in the background.";

            return RedirectToAction("Index");
        }

        private string? ResolveSchoolLogoAbsoluteUrl()
        {
            // Prefer inline-embedded logo for email clients (cid:school-logo).
            if (string.Equals(_configuration["Portal:EmailEmbedLogo"], "true", StringComparison.OrdinalIgnoreCase))
                return "cid:school-logo";

            var path = _configuration["Portal:SchoolLogo"] ?? "~/images/SLSHS.png";
            var publicBase = (_configuration["Portal:PublicSiteUrl"] ?? string.Empty).Trim().TrimEnd('/');
            var localPath = path.StartsWith("~/", StringComparison.Ordinal) ? path[1..]
                : (path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path);
            if (!string.IsNullOrWhiteSpace(publicBase))
                return publicBase + localPath;
            if (Request?.Host.HasValue == true)
                return $"{Request.Scheme}://{Request.Host}{Url.Content(path)}";
            return null;
        }

        private string? ResolveClassAbsoluteUrl(string classCode)
        {
            if (string.IsNullOrWhiteSpace(classCode)) return null;

            var relative = Url.Action("Index", "StudentClass", new { classCode });
            if (string.IsNullOrWhiteSpace(relative))
                relative = "/StudentClass/" + Uri.EscapeDataString(classCode.Trim());

            var publicBase = (_configuration["Portal:PublicSiteUrl"] ?? string.Empty).Trim().TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(publicBase))
                return publicBase + relative;

            if (Request?.Host.HasValue == true)
                return $"{Request.Scheme}://{Request.Host}{relative}";

            return null;
        }
    }
}
