using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using StudentPortal.Models.ProfessorDb;
using StudentPortal.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

using MongoDB.Bson;
namespace StudentPortal.Controllers.ProfessorDb
{
    [Route("professordb/[controller]")]
    public class ProfessorDbController : Controller
    {
        private readonly MongoDbService _mongo;
        private readonly EmailService _email;
        private readonly IConfiguration _configuration;

        public ProfessorDbController(MongoDbService mongo, EmailService email, IConfiguration configuration)
        {
            _mongo = mongo;
            _email = email;
            _configuration = configuration;
        }

        // GET: /ProfessorDb
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            // Get professor info from session
            var professorName = HttpContext.Session.GetString("UserName") ?? "Professor";
            var professorEmail = HttpContext.Session.GetString("UserEmail") ?? "";

            // Compute initials from name
            string GetInitials(string fullName)
            {
                if (string.IsNullOrWhiteSpace(fullName)) return "PR";
                var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return string.Concat(parts.Select(p => p[0])).ToUpper();
            }

            // For professors, show only their own classes (by owner email)
            var classes = (await _mongo.GetClassesByOwnerEmailAsync(professorEmail)).ToList();
            foreach (var c in classes)
            {
                if (!string.IsNullOrWhiteSpace(c.ScheduleId))
                {
                    var (room, scheduleTime) = await _mongo.GetRoomAndScheduleTimeByScheduleIdAsync(c.ScheduleId);
                    c.RoomDisplay = room;
                    c.ScheduleTimeDisplay = scheduleTime;
                }
            }

            var vm = new ProfessorDashboardViewModel
            {
                ProfessorName = professorName,
                ProfessorInitials = GetInitials(professorName),
                Classes = classes
            };

            ViewBag.Role = "Professor";
            return View("~/Views/ProfessorDb/ProfessorDb/Index.cshtml", vm);
        }

        // GET: /professordb/ProfessorDb/GetMyAssignedSubjects
        [HttpGet("GetMyAssignedSubjects")]
        public async Task<IActionResult> GetMyAssignedSubjects()
        {
            var professorEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(professorEmail))
                return Json(new { success = true, subjects = Array.Empty<object>() });

            var professor = await _mongo.GetProfessorByEmailAsync(professorEmail);
            var professorId = professor?.Id ?? string.Empty;
            var items = new System.Collections.Generic.List<MongoDB.Bson.BsonDocument>();
            if (string.IsNullOrWhiteSpace(professorId))
            {
                // Fallback: fetch by teacher name if professor record isn't found
                var sessionName = HttpContext.Session.GetString("UserName") ?? (professor?.GetFullName() ?? string.Empty);
                var byName = await _mongo.GetClassMeetingsForProfessorFlexibleAsync(null, sessionName, professorEmail);
                items = byName ?? new System.Collections.Generic.List<MongoDB.Bson.BsonDocument>();
            }
            else
            {
                items = await _mongo.GetProfessorAssignedSubjectsAsync(professorId)
                        ?? new System.Collections.Generic.List<MongoDB.Bson.BsonDocument>();
                if (items.Count == 0)
                {
                    var byName = await _mongo.GetClassMeetingsForProfessorFlexibleAsync(professorId, professor?.GetFullName() ?? string.Empty, professorEmail);
                    items = byName ?? new System.Collections.Generic.List<MongoDB.Bson.BsonDocument>();
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
                    classCode,
                    sectionId = sectionIdVal
                });
            }

            return Json(new { success = true, subjects });
        }

        // GET: /professordb/ProfessorDb/GetMyAssignedSections
        [HttpGet("GetMyAssignedSections")]
        public async Task<IActionResult> GetMyAssignedSections()
        {
            var professorEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(professorEmail))
                return Json(new { success = true, sections = Array.Empty<object>() });

            var professor = await _mongo.GetProfessorByEmailAsync(professorEmail);
            var professorId = professor?.Id ?? string.Empty;
            var professorName = professor?.GetFullName() ?? string.Empty;

            var sections = await _mongo.GetProfessorAssignedSectionsAsync(professorId, professorName, professorEmail);
            var list = sections.Select(s => new { sectionId = s.sectionId, sectionName = s.sectionName }).ToList<object>();
            return Json(new { success = true, sections = list });
        }

        // POST: /professordb/ProfessorDb/UpsertTeacherAssignment
        [HttpPost("UpsertTeacherAssignment")]
        public async Task<IActionResult> UpsertTeacherAssignment([FromBody] string payload)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(payload)) return BadRequest(new { success = false, message = "Empty payload" });
                var bson = BsonDocument.Parse(payload);
                await _mongo.UpsertClassMeetingAsync(bson);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: /professordb/ProfessorDb/CheckScheduleClassDuplicate — same rule as CreateClass (schedule + owner)
        [HttpGet("CheckScheduleClassDuplicate")]
        public async Task<IActionResult> CheckScheduleClassDuplicate([FromQuery] string scheduleId)
        {
            var professorEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(scheduleId) || string.IsNullOrWhiteSpace(professorEmail))
                return Json(new { success = true, exists = false });

            var duplicate = await _mongo.GetClassByScheduleIdAndOwnerAsync(scheduleId.Trim(), professorEmail);
            if (duplicate == null)
                return Json(new { success = true, exists = false });

            var sec = !string.IsNullOrWhiteSpace(duplicate.SectionLabel) ? duplicate.SectionLabel : duplicate.Section;
            return Json(new { success = true, exists = true, subjectName = duplicate.SubjectName ?? string.Empty, section = sec });
        }

        // POST: /ProfessorDb/CreateClass
        [HttpPost("CreateClass")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClass(string subjectName, string subjectCode, string section, string? schoolYear, string scheduleId, string? sectionId, bool allowDuplicate = false)
        {
            // Basic validation (same pattern as AdminDbController)
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

            var duplicate = await _mongo.GetClassByScheduleIdAndOwnerAsync(scheduleId ?? string.Empty, professorEmail);
            if (duplicate != null && !allowDuplicate)
            {
                var sec = !string.IsNullOrWhiteSpace(duplicate.SectionLabel) ? duplicate.SectionLabel : duplicate.Section;
                TempData["ToastMessage"] = $"⚠️ You already have a class for this assignment: \"{duplicate.SubjectName}\" (Section: {sec}). Open Create class again and confirm to add another.";
                return RedirectToAction("Index");
            }

            var newClass = new StudentPortal.Models.AdminDb.ClassItem
            {
                SubjectName = subjectName,
                SubjectCode = subjectCode,
                ScheduleId = scheduleId ?? string.Empty,
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

            // Run auto-invite in background so the UI doesn't "spin" forever
            _ = Task.Run(async () =>
            {
                try
                {
                    var recipients = new List<string>();
                    if (!string.IsNullOrWhiteSpace(sectionId))
                        recipients = await _mongo.GetStudentEmailsBySectionIdAsync(sectionId) ?? new List<string>();
                    if (recipients.Count == 0)
                        recipients = await _mongo.GetStudentEmailsByScheduleIdAsync(scheduleId) ?? new List<string>();
                    if (recipients.Count == 0)
                    {
                        var sec = !string.IsNullOrWhiteSpace(newClass.SectionLabel) ? newClass.SectionLabel : newClass.Section;
                        recipients = await _mongo.GetStudentEmailsBySectionAsync(sec) ?? new List<string>();
                    }
                    var distinctRecipients = recipients
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Select(e => (e ?? string.Empty).Trim())
                        .Where(e => !string.IsNullOrWhiteSpace(e) && e.Contains("@"))
                        .Where(e => !string.Equals(e, professorEmail, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    var subj = JoinClassEmailTemplate.DefaultSubject;
                    var courseOrSubject = !string.IsNullOrWhiteSpace(newClass.SubjectName) ? newClass.SubjectName : newClass.Course;
                    var schoolName = _configuration["Portal:SchoolName"] ?? "Sta. Lucia Senior High School";
                    var logoUrl = ResolveSchoolLogoAbsoluteUrl();
                    var classUrl = ResolveClassAbsoluteUrl(newClass.ClassCode);

                    // Also email the creator (you) with the class code + link.
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

            // Keep the outcome-specific toast set above
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Archive/delete a class. Only allowed if the current professor is the class owner.
        /// </summary>
        [HttpPost("DeleteClass")]
        public async Task<IActionResult> DeleteClass([FromBody] DeleteClassRequest req)
        {
            var ownerEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ownerEmail))
                return Json(new { success = false, message = "Not authenticated" });

            try
            {
                var classItem = !string.IsNullOrWhiteSpace(req?.ClassId)
                    ? await _mongo.GetClassByIdAsync(req.ClassId!)
                    : (!string.IsNullOrWhiteSpace(req?.ClassCode) ? await _mongo.GetClassByCodeAsync(req.ClassCode!) : null);
                if (classItem == null)
                    return Json(new { success = false, message = "Class not found" });

                if (!string.Equals(classItem.OwnerEmail?.Trim(), ownerEmail.Trim(), StringComparison.OrdinalIgnoreCase))
                    return Json(new { success = false, message = "You can only delete classes you created." });

                var contents = await _mongo.GetContentsForClassAsync(classItem.Id, classItem.ClassCode);
                foreach (var c in contents)
                {
                    await _mongo.DeleteUploadsByContentIdAsync(c.Id);
                    await _mongo.DeleteContentAsync(c.Id);
                }

                var ok = await _mongo.DeleteClassByIdAsync(classItem.Id);
                return Json(new { success = ok });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public class DeleteClassRequest
        {
            public string? ClassId { get; set; }
            public string? ClassCode { get; set; }
        }

        private string? ResolveSchoolLogoAbsoluteUrl()
        {
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


