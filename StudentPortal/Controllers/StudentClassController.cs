using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.StudentDb;
using StudentPortal.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers
{
    public class StudentClassController : Controller
    {
        private readonly MongoDbService _mongoDb;

        public StudentClassController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        [HttpGet("StudentClass/{classCode}")]
        public async Task<IActionResult> Index(string classCode)
        {
            if (string.IsNullOrEmpty(classCode))
            {
                return RedirectToAction("Index", "StudentDb");
            }

            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
            {
                // Preserve the exact invitation/deep-link (including query string) so the user lands back here after login.
                var returnUrl = $"{Request.Path}{Request.QueryString}";
                if (string.IsNullOrWhiteSpace(returnUrl))
                    returnUrl = $"/StudentClass/{classCode}";
                return RedirectToAction("Login", "Account", new { returnUrl });
            }

            // Get class details from database
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null)
                return Content("Class not found.");

            // Get user info
            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (user == null)
                return Content("User not found.");

            // Get ALL content items for this class from database
            var contentItems = await _mongoDb.GetContentsForClassAsync(classItem.Id, classItem.ClassCode);

            // Transform database content to your view model format - USING ACTUAL DATABASE TYPES
            var contentCards = new List<ContentCard>();

            foreach (var content in contentItems.OrderByDescending(c => c.CreatedAt))
            {
                // Use the actual Type from database (material, task, assessment, announcement)
                string contentType = content.Type?.ToLower() ?? "material";

                // Determine target action based on ACTUAL content type from database
                string targetAction = contentType switch
                {
                    "material" => $"/StudentMaterial/{classItem.ClassCode}/{content.Id}",
                    "task" => $"/StudentTask/{classItem.ClassCode}/{content.Id}",
                    "assessment" => $"/StudentAssessment/{classItem.ClassCode}/{content.Id}",
                    "announcement" => null,
                    "meeting" => !string.IsNullOrWhiteSpace(content.LinkUrl) ? content.LinkUrl : null,
                    _ => null
                };

                // Use actual urgency settings from database
                string urgency = content.HasUrgency ? content.UrgencyColor : null;

                contentCards.Add(new ContentCard
                {
                    ContentId = content.Id ?? string.Empty,
                    Type = contentType,
                    Title = contentType == "announcement"
                        ? (string.IsNullOrWhiteSpace(content.Description) ? "(No announcement text)" : content.Description)
                        : content.Title,
                    Meta = contentType == "announcement"
                        ? $"Posted: {content.CreatedAt.ToLocalTime():MMM d, yyyy h:mm tt}"
                        : (!string.IsNullOrEmpty(content.MetaText)
                            ? content.MetaText
                            : $"Posted: {content.CreatedAt:MMM dd, yyyy}"),
                    TargetAction = targetAction,
                    IconClass = GetIconByContentType(contentType),
                    Urgency = urgency
                });
            }

            var resolvedInstructorName = !string.IsNullOrWhiteSpace(classItem.InstructorName)
                                    ? classItem.InstructorName
                                    : (!string.IsNullOrWhiteSpace(classItem.CreatorName)
                                        ? classItem.CreatorName
                                        : (await _mongoDb.GetProfessorByEmailAsync(classItem.OwnerEmail))?.GetFullName() ?? "Instructor");

            var resolvedInitials = !string.IsNullOrWhiteSpace(classItem.CreatorInitials)
                                    ? classItem.CreatorInitials
                                    : GetInitials(resolvedInstructorName);

            var roleLabel = "Teacher";
            string teacherDepartment = string.Empty;
            string roomName = string.Empty;
            string floorDisplay = string.Empty;
            if (!string.IsNullOrWhiteSpace(classItem.OwnerEmail))
            {
                try
                {
                    teacherDepartment = await _mongoDb.GetProfessorDepartmentByEmailAsync(classItem.OwnerEmail) ?? string.Empty;
                    var prof = await _mongoDb.GetProfessorByEmailAsync(classItem.OwnerEmail);
                    if (string.IsNullOrWhiteSpace(teacherDepartment) && prof?.Programs != null && prof.Programs.Count > 0)
                        teacherDepartment = prof.Programs[0];
                }
                catch { /* optional */ }
            }
            if (!string.IsNullOrWhiteSpace(classItem.ScheduleId))
            {
                try
                {
                    var (schedRoom, schedFloor) = await _mongoDb.GetRoomAndFloorByScheduleIdAsync(classItem.ScheduleId);
                    if (!string.IsNullOrWhiteSpace(schedRoom)) roomName = schedRoom;
                    if (!string.IsNullOrWhiteSpace(schedFloor)) floorDisplay = schedFloor;
                }
                catch { /* optional */ }
            }

            var viewModel = new StudentClassViewModel
            {
                SubjectName = classItem.SubjectName ?? "Computer Science 101",
                SectionName = !string.IsNullOrWhiteSpace(classItem.SectionLabel) ? classItem.SectionLabel : (classItem.Section ?? string.Empty),
                SubjectCode = classItem.SubjectCode ?? "CS101",
                ClassCode = classItem.ClassCode ?? classCode,
                InstructorName = resolvedInstructorName,
                InstructorInitials = resolvedInitials,
                InstructorRole = roleLabel,
                TeacherDepartment = teacherDepartment,
                RoomName = roomName,
                FloorDisplay = floorDisplay,
                UserName = user.FullName ?? "Von GPT",
                Avatar = GetAvatarInitials(user.FullName ?? "Von GPT"),
                Contents = contentCards
            };

            return View("~/Views/StudentDb/StudentClass/Index.cshtml", viewModel);
        }

        private static string GetIconByContentType(string contentType)
        {
            return (contentType ?? string.Empty).ToLowerInvariant() switch
            {
                "material" => "fa-solid fa-book-open-reader",
                "task" => "fa-solid fa-file-pen",
                "assessment" => "fa-solid fa-circle-question",
                "announcement" => "fa-solid fa-bullhorn",
                "meeting" => "fa-solid fa-video",
                _ => "fa-solid fa-file"
            };
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "JD";

            var parts = name.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            else if (parts.Length == 1 && parts[0].Length >= 2)
                return parts[0].Substring(0, 2).ToUpper();
            else
                return "JD";
        }

        private string GetAvatarInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "VG";

            var parts = name.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[parts.Length - 1][0]}".ToUpper();
            else if (parts.Length == 1 && parts[0].Length >= 2)
                return parts[0].Substring(0, 2).ToUpper();
            else
                return "VG";
        }

        // --- ROUTES for specific content pages ---
        public IActionResult Material()
        {
            var vm = new StudentMaterialViewModel
            {
                SubjectName = "Computer Science 101",
                MaterialTitle = "Week 1: Introduction to Computing",
                Description = "This material introduces the fundamental concepts of computing, including the history of computers, types of systems, and basic principles of software and hardware interaction.",
                UploadedBy = "John Doe",
                UploadDate = System.DateTime.UtcNow,
                Files = new List<MaterialFile>
                {
                    new MaterialFile { FileName = "Week1_Introduction.pdf", FileUrl = "/files/Week1_Introduction.pdf" },
                    new MaterialFile { FileName = "Week1_Slides.pptx", FileUrl = "/files/Week1_Slides.pptx" }
                }
            };
            return View("~/Views/StudentDb/StudentMaterial/Index.cshtml", vm);
        }

        public IActionResult Task()
        {
            var vm = new StudentTaskViewModel
            {
                SubjectName = "Computer Science 101",
                TaskTitle = "Task 1: Intro Essay",
                Description = "Write an essay introducing yourself and your interests in the field of Computer Science.",
                PostedDate = System.DateTime.UtcNow.ToLocalTime().ToString("MMM d, yyyy"),
                Deadline = System.DateTime.UtcNow.AddDays(7).ToLocalTime().ToString("MMM d, yyyy"),
                StudentName = "Von GPT",
                StudentInitials = "VG",
                Attachments = new List<TaskAttachment>
                {
                    new TaskAttachment { FileName = "TaskInstructions.pdf", FileUrl = "/files/TaskInstructions.pdf" }
                }
            };
            return View("~/Views/StudentDb/StudentTask/Index.cshtml", vm);
        }
        public IActionResult Assessment() => View("~/Views/StudentDb/StudentAssessment/Index.cshtml");

        // --- BACK / DASHBOARD NAVIGATION ---
        public IActionResult ToDashboard()
        {
            return RedirectToAction("Index", "StudentDb");
        }
    }
}
