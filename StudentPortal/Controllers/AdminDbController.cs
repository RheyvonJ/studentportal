using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.AdminDb;
using StudentPortal.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers.AdminDb
{
    [Route("admindb/[controller]")]
    public class AdminDbController : Controller
    {
        private readonly MongoDbService _mongo;

        public AdminDbController(MongoDbService mongo)
        {
            _mongo = mongo;
        }

        // GET: /admindb/admindb
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var classes = (await _mongo.GetAllClassesAsync()).ToList();
            foreach (var c in classes)
            {
                if (!string.IsNullOrWhiteSpace(c.ScheduleId))
                {
                    var (room, scheduleTime) = await _mongo.GetRoomAndScheduleTimeByScheduleIdAsync(c.ScheduleId);
                    c.RoomDisplay = room;
                    c.ScheduleTimeDisplay = scheduleTime;
                }
            }

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

        // POST: /admindb/admindb/CreateClass
        [HttpPost("CreateClass")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClass(
            string subjectName,
            string subjectCode,
            string section,
            string? professorId,
            string? scheduleId,
            string? year,
            string? course,
            string? semester)
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

            // Check if class with same section exists
            if (await _mongo.ClassExistsAsync(subjectName, section, year, course, semester))
            {
                TempData["ToastMessage"] = $"⚠️ Class \"{subjectName}\" ({course} {year} {section}) already exists for {semester} semester.";
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
                CreatorName = creatorName,
                CreatorInitials = creatorInitials,
                CreatorRole = "Creator",
                ClassCode = _mongo.GenerateClassCode(),
                BackgroundImageUrl = ""
            };

            await _mongo.CreateClassAsync(newClass);

            TempData["ToastMessage"] = $"✅ Class \"{subjectName}\" (Section: {newClass.SectionLabel}, Code: {newClass.ClassCode}) created successfully!";
            return RedirectToAction("Index");
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
            var subjects = items.Select(d => new
            {
                section = d.TryGetValue("section", out var s) ? s.ToString() : string.Empty,
                subjectCode = d.TryGetValue("subjectCode", out var c) ? c.ToString() : string.Empty,
                units = d.TryGetValue("units", out var u) ? u.ToString() : string.Empty,
                scheduleId = d.TryGetValue("scheduleId", out var sch) ? sch.ToString() : string.Empty,
                subjectName = d.TryGetValue("subjectName", out var n) ? n.ToString() : string.Empty
            }).ToList();

            return Json(new { success = true, subjects });
        }
    }
}
