using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using StudentPortal.Models.AdminDb;
using StudentPortal.Models.StudentDb;
using StudentPortal.Services;
using StudentPortal.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers.AdminDb
{
    [Route("admindb/[controller]")]
    public class AdminDbController : Controller
    {
        private readonly MongoDbService _mongo;
        private readonly EmailService _email;
        private readonly IConfiguration _configuration;

        public AdminDbController(MongoDbService mongo, EmailService email, IConfiguration configuration)
        {
            _mongo = mongo;
            _email = email;
            _configuration = configuration;
        }

        // GET: /admindb/admindb
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var classes = (await _mongo.GetAllClassesAsync()).ToList();
            await Task.WhenAll(classes.Select(async c =>
            {
                if (!string.IsNullOrWhiteSpace(c.ScheduleId))
                {
                    var (room, scheduleTime) = await _mongo.GetRoomAndScheduleTimeByScheduleIdAsync(c.ScheduleId);
                    c.RoomDisplay = room;
                    c.ScheduleTimeDisplay = scheduleTime;
                }
            }));

            var professorName = HttpContext.Session.GetString("UserName") ?? "Professor";
            var professorEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(professorEmail))
            {
                try
                {
                    var prof = await _mongo.GetProfessorByEmailAsync(professorEmail);
                    var full = prof?.GetFullName();
                    if (!string.IsNullOrWhiteSpace(full)) professorName = full;
                }
                catch { }
            }
            string initials = string.Concat((professorName ?? "Professor").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(p => p[0])).ToUpper();

            var vm = new AdminDashboardViewModel
            {
                AdminName = professorName,
                AdminInitials = string.IsNullOrWhiteSpace(initials) ? "PR" : initials,
                Classes = classes.ToList()
            };

            ViewBag.AdminName = vm.AdminName;
            ViewBag.AdminInitials = vm.AdminInitials;
            return View("~/Views/AdminDb/AdminDb/Index.cshtml", vm);
        }

        [HttpPost("DeleteClass")]
        public async Task<IActionResult> DeleteClass([FromBody] DeleteClassRequest req)
        {
            try
            {
                var classItem = !string.IsNullOrWhiteSpace(req.ClassId)
                    ? await _mongo.GetClassByIdAsync(req.ClassId!)
                    : (!string.IsNullOrWhiteSpace(req.ClassCode) ? await _mongo.GetClassByCodeAsync(req.ClassCode!) : null);
                if (classItem == null) return Json(new { success = false, message = "Class not found" });

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

        // GET: /admindb/admindb/CheckClassExists — used before create confirmation (same rules as CreateClass)
        [HttpGet("CheckClassExists")]
        public async Task<IActionResult> CheckClassExists(
            string subjectName,
            string section,
            string? year,
            string? course,
            string? semester)
        {
            if (string.IsNullOrWhiteSpace(subjectName) || string.IsNullOrWhiteSpace(section))
                return Json(new { success = false, exists = false });

            year = string.IsNullOrWhiteSpace(year) ? "N/A" : year.Trim();
            course = string.IsNullOrWhiteSpace(course) ? "N/A" : course.Trim();
            semester = string.IsNullOrWhiteSpace(semester) ? "N/A" : semester.Trim();

            var exists = await _mongo.ClassExistsAsync(subjectName, section, year, course, semester);
            return Json(new { success = true, exists });
        }

        // POST: /admindb/admindb/CreateClass
        [HttpPost("CreateClass")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClass(
            string subjectName,
            string subjectCode,
            string section,
            string? professorId,
            string? scheduleId,
            string? sectionId,
            string? profEmail,
            string? year,
            string? course,
            string? semester,
            bool allowDuplicate = false)
        {
            if (string.IsNullOrWhiteSpace(subjectName) ||
                string.IsNullOrWhiteSpace(subjectCode) ||
                string.IsNullOrWhiteSpace(section))
            {
                TempData["ToastMessage"] = "⚠️ Please fill Subject, Subject Code, and Section.";
                return RedirectToAction("Index");
            }

            // eSys-aligned create flow: keep these optional, but avoid empty values.
            year = string.IsNullOrWhiteSpace(year) ? "N/A" : year.Trim();
            course = string.IsNullOrWhiteSpace(course) ? "N/A" : course.Trim();
            semester = string.IsNullOrWhiteSpace(semester) ? "N/A" : semester.Trim();

            // Duplicate classes require explicit confirmation (allowDuplicate) from the create dialog
            if (await _mongo.ClassExistsAsync(subjectName, section, year, course, semester) && !allowDuplicate)
            {
                TempData["ToastMessage"] = $"⚠️ Class \"{subjectName}\" ({course} {year} {section}) already exists for {semester} semester. Open Create class again and confirm to add another.";
                return RedirectToAction("Index");
            }

            var creatorName = User?.Identity?.Name ?? "Admin";
            var creatorInitials = new string(creatorName
                .Where(char.IsLetter)
                .Take(2)
                .ToArray())
                .ToUpper();

            var newClass = new ClassItem
            {
                SubjectName = subjectName,
                SubjectCode = subjectCode,
                Section = section,
                Course = course,
                Year = year,
                Semester = semester,
                ScheduleId = string.IsNullOrWhiteSpace(scheduleId) ? string.Empty : scheduleId.Trim(),
                EnrollmentSectionId = string.IsNullOrWhiteSpace(sectionId) ? string.Empty : sectionId.Trim(),
                CreatorName = creatorName,
                CreatorInitials = creatorInitials,
                CreatorRole = "Creator",
                ClassCode = _mongo.GenerateClassCode(),
                BackgroundImageUrl = ""
            };

            await _mongo.CreateClassAsync(newClass);

            var adminEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
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
                        .Where(e =>
                        {
                            if (string.Equals(e, adminEmail, StringComparison.OrdinalIgnoreCase)) return false;
                            if (!string.IsNullOrWhiteSpace(profEmail) &&
                                string.Equals(e, profEmail.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
                            return true;
                        })
                        .ToList();
                    Console.WriteLine($"[CreateClass] Auto-invite recipients for {newClass.ClassCode}: {distinctRecipients.Count}");

                    var subj = JoinClassEmailTemplate.DefaultSubject;
                    var courseOrSubject = !string.IsNullOrWhiteSpace(newClass.SubjectName) ? newClass.SubjectName : newClass.Course;
                    var schoolName = _configuration["Portal:SchoolName"] ?? "Sta. Lucia Senior High School";
                    var logoUrl = inviteEmailLogoUrl;
                    var classUrl = inviteEmailClassUrl;

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
                            await _mongo.AddNotificationAsync(new UserNotification
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

        [HttpGet("GetProfessorSubjects")]
        public async Task<IActionResult> GetProfessorSubjects([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Json(new { success = false, message = "Missing email", subjects = Array.Empty<object>() });

            var classes = await _mongo.GetClassesByOwnerEmailAsync(email);
            var subjects = classes
                .Where(c => !string.IsNullOrWhiteSpace(c.SubjectName))
                .GroupBy(c => new { name = c.SubjectName.Trim(), code = (c.SubjectCode ?? string.Empty).Trim() })
                .Select(g => new { subjectName = g.Key.name, subjectCode = g.Key.code })
                .ToList();

            return Json(new { success = true, subjects });
        }

        [HttpGet("GetProfessorAssignedSubjects")]
        public async Task<IActionResult> GetProfessorAssignedSubjects([FromQuery] string professorId)
        {
            if (string.IsNullOrWhiteSpace(professorId))
                return Json(new { success = false, message = "Missing professorId", subjects = Array.Empty<object>() });

            var items = await _mongo.GetProfessorAssignedSubjectsAsync(professorId);
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

                subjects.Add(new
                {
                    section,
                    subjectCode,
                    units,
                    scheduleId,
                    subjectName,
                    schoolYear,
                    classCode = "",
                    sectionId = sectionIdVal
                });
            }

            return Json(new { success = true, subjects });
        }
    }
}
