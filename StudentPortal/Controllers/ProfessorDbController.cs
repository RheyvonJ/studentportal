using Microsoft.AspNetCore.Mvc;
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

        public ProfessorDbController(MongoDbService mongo, EmailService email)
        {
            _mongo = mongo;
            _email = email;
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

        // GET: /professordb/professordb/GetMyAssignedSubjects
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
                    classCode
                });
            }

            return Json(new { success = true, subjects });
        }

        // GET: /professordb/professordb/GetMyAssignedSections
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

        // POST: /ProfessorDb/CreateClass
        [HttpPost("CreateClass")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClass(string subjectName, string subjectCode, string section, string? schoolYear, string scheduleId, string? sectionId)
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
                    .Where(e => !string.Equals(e, professorEmail, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var subj = "Join Class Code";
                var courseOrSubject = !string.IsNullOrWhiteSpace(newClass.SubjectName) ? newClass.SubjectName : newClass.Course;

                int sent = 0;
                int failed = 0;

                var errors = new List<string>();
                foreach (var r in distinctRecipients)
                {
                    string displayName = "Student";
                    try
                    {
                        var user = await _mongo.GetUserByEmailAsync(r);
                        if (user != null && !string.IsNullOrWhiteSpace(user.FullName))
                            displayName = user.FullName;
                        else
                        {
                            var xf = await _mongo.GetStudentExtraFieldsByEmailAsync(r);
                            if (xf != null)
                            {
                                xf.TryGetValue("Student.FirstName", out var fn);
                                xf.TryGetValue("Student.MiddleName", out var mn);
                                xf.TryGetValue("Student.LastName", out var ln);
                                var parts = new List<string>();
                                if (!string.IsNullOrWhiteSpace(fn)) parts.Add(fn);
                                if (!string.IsNullOrWhiteSpace(mn)) parts.Add(mn);
                                if (!string.IsNullOrWhiteSpace(ln)) parts.Add(ln);
                                var built = string.Join(" ", parts);
                                if (!string.IsNullOrWhiteSpace(built)) displayName = built;
                            }
                        }
                    }
                    catch { }

                    var bodyHtml = BuildJoinClassEmailHtml(displayName, courseOrSubject, newClass.ClassCode);
                    var res = await _email.SendEmailAsync(r, subj, bodyHtml, isHtml: true);
                    if (res.ok)
                    {
                        sent++;
                    }
                    else
                    {
                        failed++;
                        if (!string.IsNullOrWhiteSpace(res.error)) errors.Add(res.error);
                    }

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

                // Do not send to professor; only students should receive the code

                if (sent > 0 && failed == 0)
                    TempData["ToastMessage"] = $"✅ Emailed join code to {sent} student(s).";
                else if (sent > 0 && failed > 0)
                    TempData["ToastMessage"] = $"⚠️ Emailed {sent} student(s), {failed} failed. Last error: {(errors.Count > 0 ? errors[^1] : "unknown")}";
                else
                    TempData["ToastMessage"] = $"⚠️ No enrolled students were emailed. Error: {(errors.Count > 0 ? errors[^1] : "none found")}";
            }
            catch
            {
                TempData["ToastMessage"] = $"⚠️ Class \"{subjectName}\" created, but emailing students failed.";
            }

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

        private static string BuildJoinClassEmailHtml(string studentName, string courseName, string classCode)
        {
            const string headerColor = "#1A3E63";
            const string bodyBg = "#f0f0f0";
            const string contentBg = "#ffffff";
            const string textColor = "#333333";
            const string codeBoxBg = "#1A3E63";
            const string instructionBg = "#F0F2F5";
            const string borderLeft = "#333333";

            return $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""/><meta name=""viewport"" content=""width=device-width,initial-scale=1""/></head>
<body style=""margin:0;padding:20px;font-family:sans-serif;background:{bodyBg};"">
<div style=""max-width:600px;margin:0 auto;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.1);"">
  <div style=""background:{headerColor};color:#fff;padding:24px;text-align:center;"">
    <div style=""margin-bottom:12px;font-size:12px;color:rgba(255,255,255,0.9);"">[Sta. Lucia SHS Logo]</div>
    <div style=""font-weight:bold;font-size:18px;"">Sta. Lucia Senior High School</div>
    <div style=""font-size:12px;margin-top:4px;opacity:0.95;"">LEARNING MANAGEMENT SYSTEM</div>
  </div>
  <div style=""background:{contentBg};padding:28px;color:{textColor};"">
    <h2 style=""margin:0 0 8px;font-size:20px;color:{textColor};"">Join Class Code</h2>
    <p style=""margin:0 0 20px;font-size:14px;color:#666;"">Use the code below to join your class in the student portal.</p>
    <hr style=""border:none;border-top:1px solid #ddd;margin:20px 0;""/>
    <p style=""margin:0 0 12px;font-size:14px;"">Greetings, <strong>{System.Net.WebUtility.HtmlEncode(studentName)}</strong>,</p>
    <p style=""margin:0 0 20px;font-size:14px;"">Here is the join class code for the course <strong>{System.Net.WebUtility.HtmlEncode(courseName)}</strong>:</p>
    <div style=""background:{codeBoxBg};color:#fff;padding:16px 24px;border-radius:6px;text-align:center;margin:20px 0;"">
      <span style=""font-size:22px;font-weight:bold;letter-spacing:2px;"">{System.Net.WebUtility.HtmlEncode(classCode)}</span>
    </div>
    <div style=""background:{instructionBg};border-left:4px solid {borderLeft};padding:16px;margin:20px 0;border-radius:0 4px 4px 0;"">
      <p style=""margin:0;font-size:14px;color:{textColor};line-height:1.5;"">Please use this code to join the class in your student portal. If you encounter any issues, feel free to reach out for assistance. We look forward to your active participation in the course.</p>
    </div>
  </div>
  <div style=""background:{headerColor};color:#fff;padding:20px;text-align:center;"">
    <p style=""margin:0;font-size:14px;"">Thank you.</p>
    <p style=""margin:8px 0 0;font-size:12px;opacity:0.9;"">Sta. Lucia Senior High School - Learning Management System</p>
  </div>
</div>
</body>
</html>";
        }
    }
}


