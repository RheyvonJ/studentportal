using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.AdminDb;
using StudentPortal.Models.Studentdb;
using StudentPortal.Models.StudentDb;
using StudentPortal.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers.Studentdb
{
    [Route("studentdb/[controller]")]
    public class StudentDbController : Controller
    {
        private readonly MongoDbService _mongoDb;
        private readonly LibraryService _libraryService;

        public StudentDbController(MongoDbService mongoDb, LibraryService libraryService)
        {
            _mongoDb = mongoDb;
            _libraryService = libraryService;
        }

        // GET: studentdb/StudentDb
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
                return Content("User is not logged in. Please log in first.");

            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (user == null)
                return Content("User not found in the database.");

            var joinedClasses = user.JoinedClasses ?? new List<string>();

            // Fetch pending join requests
            var pendingRequests = await _mongoDb.GetJoinRequestsByEmailAsync(email);
            var pendingClassCodes = pendingRequests
                                    .Where(r => r.Status == "Pending")
                                    .Select(r => r.ClassCode)
                                    .ToList();

            // Combine approved + pending
            var allClassCodes = joinedClasses.Concat(pendingClassCodes).Distinct().ToList();

            var classes = new List<ClassItem>();
            if (allClassCodes.Count > 0)
                classes = await _mongoDb.GetClassesByCodesAsync(allClassCodes); // CHANGED: GetClassesByCodesAsync instead of GetClassesByIdsAsync

            // Build display name from FirstName, MiddleName, LastName, or fallback to FullName
            string displayName = "Student";
            var nameParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(user.FirstName)) nameParts.Add(user.FirstName);
            if (!string.IsNullOrWhiteSpace(user.MiddleName)) nameParts.Add(user.MiddleName);
            if (!string.IsNullOrWhiteSpace(user.LastName)) nameParts.Add(user.LastName);
            
            if (nameParts.Count > 0)
            {
                displayName = string.Join(" ", nameParts);
            }
            else if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                displayName = user.FullName.Trim();
            }
            
            // Build initials from name parts
            string initials = "ST";
            if (nameParts.Count > 0)
            {
                // Use first letter of first name and last name
                var firstInitial = nameParts[0][0].ToString().ToUpper();
                var lastInitial = nameParts.Count > 1 ? nameParts[nameParts.Count - 1][0].ToString().ToUpper() : "";
                initials = firstInitial + lastInitial;
            }
            else if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                var fullNameParts = user.FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (fullNameParts.Length > 0)
                {
                    initials = string.Join("", fullNameParts.Take(2).Select(w => w[0])).ToUpper();
                }
            }
            
            // Prepare view model with instructor fallback (OwnerEmail -> Professor name) and room/schedule
            var classContents = new List<StudentPortal.Models.Studentdb.ClassContent>();
            foreach (var c in classes)
            {
                var status = joinedClasses.Contains(c.ClassCode) ? "Approved" : "Pending";
                string instructorDisplay = !string.IsNullOrWhiteSpace(c.InstructorName) ? c.InstructorName : "";
                if (string.IsNullOrWhiteSpace(instructorDisplay) && !string.IsNullOrWhiteSpace(c.CreatorName))
                {
                    instructorDisplay = c.CreatorName;
                }
                if (string.IsNullOrWhiteSpace(instructorDisplay) && !string.IsNullOrWhiteSpace(c.OwnerEmail))
                {
                    var prof = await _mongoDb.GetProfessorByEmailAsync(c.OwnerEmail);
                    var full = prof?.GetFullName();
                    if (!string.IsNullOrWhiteSpace(full)) instructorDisplay = full;
                }

                if (string.IsNullOrWhiteSpace(instructorDisplay)) instructorDisplay = "Instructor";

                string roomDisplay = c.RoomDisplay ?? string.Empty;
                string scheduleTimeDisplay = c.ScheduleTimeDisplay ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(c.ScheduleId))
                {
                    var (room, scheduleTime) = await _mongoDb.GetRoomAndScheduleTimeByScheduleIdAsync(c.ScheduleId);
                    if (!string.IsNullOrWhiteSpace(room)) roomDisplay = room;
                    if (!string.IsNullOrWhiteSpace(scheduleTime)) scheduleTimeDisplay = scheduleTime;
                }

                classContents.Add(new StudentPortal.Models.Studentdb.ClassContent
                {
                    Title = string.IsNullOrWhiteSpace(c.SubjectName) ? "No title" : c.SubjectName,
                    SubjectCode = c.SubjectCode ?? string.Empty,
                    Section = string.IsNullOrWhiteSpace(c.SectionLabel) ? "No section" : c.SectionLabel,
                    InstructorName = instructorDisplay,
                    InstructorRole = (!string.IsNullOrWhiteSpace(c.CreatorRole) && c.CreatorRole.ToLower() == "professor") ? "Professor" : "Instructor",
                    BackgroundImageUrl = string.IsNullOrWhiteSpace(c.BackgroundImageUrl) ? "/images/classbg.jpg" : c.BackgroundImageUrl,
                    ClassCode = c.ClassCode,
                    Status = status,
                    RoomDisplay = roomDisplay,
                    ScheduleTimeDisplay = scheduleTimeDisplay
                });
            }

            var model = new StudentPortal.Models.Studentdb.AdminDashboardViewModel
            {
                UserName = displayName,
                Avatar = initials,
                CurrentPage = "home",
                Classes = classContents
            };

            return View("~/Views/Studentdb/StudentDb/Index.cshtml", model);
        }

        // POST: studentdb/StudentDb/RequestJoin
        [HttpPost("RequestJoin")]
        [Consumes("application/json")]
        public async Task<IActionResult> RequestJoin([FromBody] JoinClassRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ClassCode))
                return BadRequest(new { Success = false, Message = "Class code is required." });

            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
                return BadRequest(new { Success = false, Message = "User is not logged in." });

            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (user == null)
                return NotFound(new { Success = false, Message = "User not found." });

            var classItem = await _mongoDb.GetClassByCodeAsync(request.ClassCode.Trim());
            if (classItem == null)
                return NotFound(new { Success = false, Message = "Class not found." });

            // Check if already joined or pending
            if ((user.JoinedClasses?.Contains(classItem.ClassCode) ?? false) ||
                (await _mongoDb.JoinRequestExistsAsync(email, classItem.ClassCode)))
            {
                return BadRequest(new { Success = false, Message = "You have already joined or requested this class." });
            }

            // ✅ Create the join request
            var joinRequest = new JoinRequest
            {
                ClassId = classItem.Id,
                ClassCode = classItem.ClassCode,
                StudentEmail = user.Email,
                StudentName = string.IsNullOrWhiteSpace(user.FullName) ? "Student" : user.FullName,
                Status = "Pending",
                RequestedAt = DateTime.UtcNow
            };

            await _mongoDb.CreateJoinRequestAsync(joinRequest);

            // Return the created request so frontend can show/update
            return Ok(new { Success = true, Message = "Join request submitted! Waiting for admin approval.", JoinRequest = joinRequest });
        }

        [HttpGet("Notifications")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Notifications()
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            Response.Headers["X-Notifications-Endpoint"] = "studentdb/StudentDb/Notifications";

            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
                return Unauthorized(new { success = false });

            var user = await _mongoDb.GetUserByEmailAsync(email);
            var joinedCodes = user?.JoinedClasses ?? new List<string>();
            var prevJoinedCsv = HttpContext.Session.GetString("Notifications_PrevJoinedCodes") ?? string.Empty;
            var prevJoinedCodes = prevJoinedCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Distinct().ToList();

            var nowUtc = DateTime.UtcNow;

            // Soft-delete notifications older than 30 days so the list stays fresh
            await _mongoDb.CleanupOldNotificationsAsync(email, nowUtc.AddDays(-30));

            var lastCheckedStr = HttpContext.Session.GetString("Notifications_LastCheckedUtc");
            DateTime lastCheckedUtc;
            if (!DateTime.TryParse(lastCheckedStr, out lastCheckedUtc))
            {
                // Fall back to latest notification timestamp to avoid re-sending old items after restart
                var latest = await _mongoDb.GetLatestNotificationCreatedAtAsync(email);
                lastCheckedUtc = latest ?? nowUtc.AddDays(-30);
            }
            else
            {
                // Small overlap to avoid missing near-simultaneous inserts
                lastCheckedUtc = lastCheckedUtc.AddSeconds(-2);
            }
            // Allow looking back up to 7 days on session loss to recover penalties/approvals
            var floorUtc = nowUtc.AddDays(-7);
            if (lastCheckedUtc < floorUtc) lastCheckedUtc = floorUtc;

            var approvedReqs = await _mongoDb.GetApprovedJoinRequestsByEmailSinceAsync(email, lastCheckedUtc);
            var rejectedReqs = await _mongoDb.GetRejectedJoinRequestsByEmailSinceAsync(email, lastCheckedUtc);
            var approved = approvedReqs.Select(r => r.ClassCode).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
            var rejected = rejectedReqs.Select(r => r.ClassCode).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();

            var unenrolled = prevJoinedCodes.Except(joinedCodes).ToList();

            int assessments = 0, tasks = 0, materials = 0, announcements = 0;
            foreach (var classCode in joinedCodes)
            {
                var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
                if (classItem == null) continue;
                var contents = await _mongoDb.GetContentsForClassAsync(classItem.Id, classItem.ClassCode);
                foreach (var c in contents)
                {
                    if (c.CreatedAt <= lastCheckedUtc) continue;
                    var t = (c.Type ?? string.Empty).ToLowerInvariant();
                    if (t == "assessment") assessments++;
                    else if (t == "task") tasks++;
                    else if (t == "material") materials++;
                    else if (t == "announcement") announcements++;

                    var title = string.IsNullOrWhiteSpace(c.Title) ? (t == "assessment" ? "Assessment" : t == "task" ? "Task" : t == "material" ? "Material" : "Content") : c.Title;
                    if (t == "assessment")
                    {
                        await _mongoDb.AddNotificationAsync(new UserNotification { Email = email, Type = "content-assessment-new", Text = $"New assessment posted: {title}", Code = classItem.ClassCode, CreatedAt = DateTime.UtcNow });
                    }
                    else if (t == "task")
                    {
                        await _mongoDb.AddNotificationAsync(new UserNotification { Email = email, Type = "content-task-new", Text = $"New task posted: {title}", Code = classItem.ClassCode, CreatedAt = DateTime.UtcNow });
                    }
                    else if (t == "material")
                    {
                        await _mongoDb.AddNotificationAsync(new UserNotification { Email = email, Type = "content-material-new", Text = $"New material uploaded: {title}", Code = classItem.ClassCode, CreatedAt = DateTime.UtcNow });
                    }
                    else if (t == "meeting")
                    {
                        await _mongoDb.AddNotificationAsync(new UserNotification { Email = email, Type = "meeting", Text = $"New online meeting posted: {title}", Code = classItem.ClassCode, CreatedAt = DateTime.UtcNow });
                    }
                }
            }

            var libChanges = await _libraryService.GetReservationStatusChangesAsync(email, lastCheckedUtc);
            var libPenalties = await _libraryService.GetRecentPenaltiesAsync(email, lastCheckedUtc);
            var libDueSoon = await _libraryService.GetDueSoonReservationsAsync(email, nowUtc, TimeSpan.FromHours(24));

            foreach (var code in approved)
            {
                await _mongoDb.AddNotificationAsync(new UserNotification { Email = email, Type = "class-approved", Text = $"Your request to join the class \"{code}\" has been approved.", Code = code, CreatedAt = DateTime.UtcNow });
            }
            foreach (var code in rejected)
            {
                await _mongoDb.AddNotificationAsync(new UserNotification { Email = email, Type = "class-rejected", Text = $"Your request to join class \"{code}\" was rejected.", Code = code, CreatedAt = DateTime.UtcNow });
            }
            foreach (var code in unenrolled)
            {
                await _mongoDb.AddNotificationAsync(new UserNotification { Email = email, Type = "class-unenrolled", Text = $"You have been unenrolled from class {code}.", Code = code, CreatedAt = DateTime.UtcNow });
            }
            if (assessments > 0)
            {
                await _mongoDb.AddNotificationAsync(new UserNotification { Email = email, Type = "assessment", Text = $"{assessments} new assessment(s) posted", CreatedAt = DateTime.UtcNow });
            }
            if (tasks > 0)
            {
                await _mongoDb.AddNotificationAsync(new UserNotification { Email = email, Type = "task", Text = $"{tasks} new task(s) posted", CreatedAt = DateTime.UtcNow });
            }
            if (materials > 0)
            {
                await _mongoDb.AddNotificationAsync(new UserNotification { Email = email, Type = "material", Text = $"{materials} new material(s) posted", CreatedAt = DateTime.UtcNow });
            }
            if (announcements > 0)
            {
                await _mongoDb.AddNotificationAsync(new UserNotification { Email = email, Type = "announcement", Text = $"{announcements} new announcement(s)", CreatedAt = DateTime.UtcNow });
            }
            foreach (var title in libChanges.approvedTitles)
            {
                await _mongoDb.AddNotificationAsync(new UserNotification { Email = email, Type = "libra-approved", Text = $"Your book reservation \"{title}\" has been approved.", Code = title, CreatedAt = DateTime.UtcNow });
            }
            foreach (var title in libChanges.rejectedTitles)
            {
                await _mongoDb.AddNotificationAsync(new UserNotification { Email = email, Type = "libra-rejected", Text = $"Your book reservation \"{title}\" was rejected.", Code = title, CreatedAt = DateTime.UtcNow });
            }
            foreach (var title in libChanges.cancelledTitles)
            {
                await _mongoDb.AddNotificationAsync(new UserNotification { Email = email, Type = "libra-cancelled", Text = $"Your book reservation \"{title}\" was cancelled.", Code = title, CreatedAt = DateTime.UtcNow });
            }
            foreach (var (title, due) in libDueSoon)
            {
                var code = $"{title}|{due:O}";
                var localDue = due.ToLocalTime();
                await _mongoDb.AddNotificationAsync(new UserNotification
                {
                    Email = email,
                    Type = "libra-due-soon",
                    Text = $"\"{title}\" is due by {localDue:MMM d, h:mm tt}.",
                    Code = code,
                    CreatedAt = DateTime.UtcNow
                });
            }
            foreach (var p in libPenalties)
            {
                var msg = p.Message;
                var created = p.CreatedAt;
                var code = $"penalty|{created:O}|{msg}";
                await _mongoDb.AddNotificationAsync(new UserNotification
                {
                    Email = email,
                    Type = "libra-penalty",
                    Text = $"Library penalty: {msg}",
                    Code = code,
                    CreatedAt = created
                });
            }

            HttpContext.Session.SetString("Notifications_PrevJoinedCodes", string.Join(',', joinedCodes));
            // Store a slightly earlier timestamp to reduce chance of missing updates
            HttpContext.Session.SetString("Notifications_LastCheckedUtc", nowUtc.AddSeconds(-2).ToString("o"));

            var items = await _mongoDb.GetNotificationsByEmailAsync(email, 100);
            var unreadCount = items.Count(n => !n.Read);
            return Ok(new {
                success = true,
                items,
                unread = unreadCount,
                approved,
                rejected,
                unenrolled,
                assessments,
                tasks,
                materials,
                announcements,
                libraApprovedReservations = libChanges.approvedTitles,
                libraRejectedReservations = libChanges.rejectedTitles,
                libraCancelledReservations = libChanges.cancelledTitles,
                libraPenalties = libPenalties.Select(x => new { message = x.Message, createdAt = x.CreatedAt }),
                libraDueSoon = libDueSoon.Select(x => new { title = x.Title, due = x.DueDate })
            });
        }

        [HttpPost("Notifications/MarkRead")]
        [Consumes("application/json")]
        public async Task<IActionResult> MarkRead([FromBody] MarkReadNotificationRequest request)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email)) return Unauthorized(new { success = false });
            if (request == null || string.IsNullOrWhiteSpace(request.Id)) return BadRequest(new { success = false });
            await _mongoDb.MarkNotificationReadAsync(request.Id);
            return Ok(new { success = true });
        }

        [HttpPost("Notifications/Delete")]
        [Consumes("application/json")]
        public async Task<IActionResult> Delete([FromBody] DeleteNotificationRequest request)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email)) return Unauthorized(new { success = false });
            if (request == null || string.IsNullOrWhiteSpace(request.Id)) return BadRequest(new { success = false });
            await _mongoDb.DeleteNotificationAsync(request.Id);
            return Ok(new { success = true });
        }
    }
}
