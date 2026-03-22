using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.StudentDb;
using StudentPortal.Services;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace StudentPortal.Controllers
{
    public class StudentAssessmentController : Controller
    {
        private readonly MongoDbService _mongoDb;

        public StudentAssessmentController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        [HttpGet("/StudentAssessment/{classCode}/{contentId}")]
        public async Task<IActionResult> Index(string classCode, string contentId)
        {
            if (string.IsNullOrEmpty(classCode) || string.IsNullOrEmpty(contentId))
                return NotFound("Class code or Content ID not provided.");

            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Index", "StudentDb");

            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null)
                return NotFound("Class not found.");

            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            if (contentItem == null || contentItem.Type != "assessment")
                return NotFound("Assessment not found.");

            if (contentItem.ClassId != classItem.Id)
                return NotFound("Assessment not found in this class.");

            var user = await _mongoDb.GetUserByEmailAsync(email);

            var model = new StudentAssessmentViewModel
            {
                AssessmentTitle = contentItem.Title ?? "Assessment",
                Description = contentItem.Description ?? string.Empty,
                PostedDate = contentItem.CreatedAt.ToString("MMM d, yyyy"),
                Deadline = contentItem.Deadline?.ToString("MMM d, yyyy") ?? string.Empty,
                StudentName = user?.FullName ?? "Student",
                StudentInitials = GetInitials(user?.FullName ?? "ST"),
                Attachments = new System.Collections.Generic.List<StudentPortal.Models.StudentDb.TaskAttachment>(),
                ClassCode = classItem.ClassCode ?? classCode,
                ContentId = contentItem.Id ?? contentId
            };

            bool isDone = false;
            if (user != null)
            {
                var result = await _mongoDb.GetAssessmentResultAsync(classItem.Id, contentItem.Id, user.Id ?? string.Empty);
                if (result != null)
                {
                    model.IsAnswered = result.SubmittedAt.HasValue;
                    model.Score = result.Score;
                    model.MaxScore = result.MaxScore;
                    isDone = string.Equals(result.Status, "done", System.StringComparison.OrdinalIgnoreCase);
                }
            }

            try
            {
                var logs = await _mongoDb.GetAntiCheatLogsAsync(classItem.Id, contentItem.Id);
                var relevant = logs ?? new System.Collections.Generic.List<StudentPortal.Models.AdminDb.AntiCheatLog>();
                var studentTotal = relevant
                    .Where(l => (!string.IsNullOrEmpty(user?.Id) && l.StudentId == (user?.Id ?? string.Empty))
                             || (!string.IsNullOrEmpty(user?.Email) && string.Equals(l.StudentEmail, user?.Email, System.StringComparison.OrdinalIgnoreCase)))
                    .Sum(l => l.EventCount);
                var isVoidForStudent = studentTotal >= 20;
                ViewBag.IsAssessmentVoid = isVoidForStudent;
                ViewBag.IsOpenQuizLocked = isVoidForStudent || isDone;
            }
            catch { ViewBag.IsAssessmentVoid = false; ViewBag.IsOpenQuizLocked = isDone; }

            var instructorName = !string.IsNullOrWhiteSpace(classItem.InstructorName)
                ? classItem.InstructorName
                : (!string.IsNullOrWhiteSpace(classItem.CreatorName) ? classItem.CreatorName : "Instructor");
            ViewBag.InstructorName = instructorName;
            ViewBag.InstructorInitials = GetInitials(instructorName);
            ViewBag.SubjectCode = classItem.SubjectCode ?? string.Empty;
            ViewBag.SubjectName = classItem.SubjectName ?? string.Empty;

            return View("~/Views/StudentDb/StudentAssessment/Index.cshtml", model);
        }

        [HttpGet("/StudentAssessment/GetComments")]
        public async Task<IActionResult> GetComments([FromQuery] string contentId)
        {
            if (string.IsNullOrEmpty(contentId)) return BadRequest(new { success = false, message = "Missing contentId" });
            var items = await _mongoDb.GetTaskCommentsAsync(contentId);
            var comments = items.Select(c => new
            {
                id = c.Id,
                authorName = c.AuthorName,
                role = c.Role,
                text = c.Text,
                createdAt = c.CreatedAt,
                replies = c.Replies.Select(r => new { authorName = r.AuthorName, role = r.Role, text = r.Text, createdAt = r.CreatedAt }).ToList()
            }).ToList();
            return Json(new { success = true, comments });
        }

        [HttpPost("/StudentAssessment/PostComment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostComment(string contentId, string classCode, string text)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            var user = !string.IsNullOrEmpty(email) ? await _mongoDb.GetUserByEmailAsync(email) : null;
            var authorName = user?.FullName ?? (User?.Identity?.Name ?? "Student");
            var authorEmail = user?.Email ?? (email ?? "student@local");
            var role = user?.Role ?? "Student";
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null) return Json(new { success = false, message = "Class not found" });
            var item = await _mongoDb.AddTaskCommentAsync(contentId, classItem.Id, authorEmail, authorName, role, text ?? string.Empty);
            if (item == null) return Json(new { success = false, message = "Failed to add comment" });
            return Json(new
            {
                success = true,
                comment = new
                {
                    id = item.Id,
                    authorName = item.AuthorName,
                    role = item.Role,
                    text = item.Text,
                    createdAt = item.CreatedAt,
                    replies = item.Replies.Select(r => new { authorName = r.AuthorName, role = r.Role, text = r.Text, createdAt = r.CreatedAt }).ToList()
                }
            });
        }

        [HttpPost("/StudentAssessment/PostReply")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostReply(string commentId, string text)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            var user = !string.IsNullOrEmpty(email) ? await _mongoDb.GetUserByEmailAsync(email) : null;
            var authorName = user?.FullName ?? (User?.Identity?.Name ?? "Student");
            var authorEmail = user?.Email ?? (email ?? "student@local");
            var role = user?.Role ?? "Student";
            var updated = await _mongoDb.AddTaskReplyAsync(commentId, authorEmail, authorName, role, text ?? string.Empty);
            if (updated == null) return Json(new { success = false, message = "Failed to add reply" });
            var last = updated.Replies.LastOrDefault();
            return Json(new { success = true, reply = last != null ? new { authorName = last.AuthorName, role = last.Role, text = last.Text, createdAt = last.CreatedAt } : null });
        }

        private string GetInitials(string name)
        {
            var parts = (name ?? string.Empty).Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return ($"{parts[0][0]}{parts[^1][0]}").ToUpper();
            if (parts.Length == 1) return parts[0].Substring(0, System.Math.Min(2, parts[0].Length)).ToUpper();
            return "ST";
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsDone(string classCode, string contentId)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Index", "StudentDb");
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (classItem == null || contentItem == null || user == null) return RedirectToAction("Index", new { classCode, contentId });
            await _mongoDb.MarkAssessmentDoneAsync(classItem.Id, classItem.ClassCode, contentItem.Id, user.Id ?? string.Empty, user.Email ?? string.Empty);
            TempData["ToastMessage"] = "✅ Assessment marked as done!";
            return RedirectToAction("Index", new { classCode, contentId });
        }
    }
}
