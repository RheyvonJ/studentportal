using Microsoft.AspNetCore.Mvc;
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

        public TeacherDbController(MongoDbService mongo, EmailService email)
        {
            _mongo = mongo;
            _email = email;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var professorName = HttpContext.Session.GetString("UserName") ?? "Professor";
            var professorEmail = HttpContext.Session.GetString("UserEmail") ?? "";

            string GetInitials(string fullName)
            {
                if (string.IsNullOrWhiteSpace(fullName)) return "PR";
                var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return string.Concat(parts.Select(p => p[0])).ToUpper();
            }

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
            return View("~/Views/TeacherDb/TeacherDb/Index.cshtml", vm);
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
                var timeSlotDisplay = d.TryGetValue("timeSlotDisplay", out var ts) ? ts.ToString() : string.Empty;
                var roomName = d.TryGetValue("roomName", out var rn) ? rn.ToString() : string.Empty;

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
                    timeSlotDisplay,
                    roomName,
                    classCode
                });
            }

            return Json(new { success = true, subjects });
        }

        [HttpPost("CreateClass")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClass(string subjectName, string subjectCode, string section, string? schoolYear, string scheduleId)
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

            if (!string.IsNullOrWhiteSpace(scheduleId))
            {
                try
                {
                    var recipients = await _mongo.GetStudentEmailsByScheduleIdAsync(scheduleId) ?? new List<string>();
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

                        var body = $"Greetings, {displayName}\n\nHere is the join class code for the course {courseOrSubject}:\n\n{newClass.ClassCode}\n\nPlease use this code to join the class in your student portal. If you encounter any issues, feel free to reach out for assistance. We look forward to your active participation in the course.";

                        var res = await _email.SendEmailAsync(r, subj, body);
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

                    if (sent > 0 && failed == 0)
                        TempData["ToastMessage"] = $"✅ Class \"{subjectName}\" created and emailed join code to {sent} student(s).";
                    else if (sent > 0 && failed > 0)
                        TempData["ToastMessage"] = $"⚠️ Class created and emailed {sent} student(s), {failed} failed. Last error: {(errors.Count > 0 ? errors[^1] : "unknown")}";
                    else
                        TempData["ToastMessage"] = $"⚠️ Class created but no enrolled students were emailed. Error: {(errors.Count > 0 ? errors[^1] : "none found")}";
                }
                catch
                {
                    TempData["ToastMessage"] = $"⚠️ Class \"{subjectName}\" created, but emailing students failed.";
                }
            }
            else
            {
                TempData["ToastMessage"] = $"✅ Class \"{subjectName}\" created. No schedule selected, so no students were emailed.";
            }

            return RedirectToAction("Index");
        }
    }
}
