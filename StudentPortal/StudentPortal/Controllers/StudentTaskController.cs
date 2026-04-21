using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.StudentDb;
using StudentPortal.Services;
using StudentPortal.Utilities;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers
{
    public class StudentTaskController : Controller
    {
        private readonly MongoDbService _mongoDb;
        private readonly IWebHostEnvironment _env;

        public StudentTaskController(MongoDbService mongoDb, IWebHostEnvironment env)
        {
            _mongoDb = mongoDb;
            _env = env;
        }

        [HttpGet("/StudentTask/{classCode}/{contentId}")]
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
            if (contentItem == null || contentItem.Type != "task")
                return NotFound("Task not found.");

            if (contentItem.ClassId != classItem.Id)
                return NotFound("Task not found in this class.");

            var user = await _mongoDb.GetUserByEmailAsync(email);
            var files = await _mongoDb.GetUploadsByContentIdAsync(contentId);
            var existing = user?.Id != null ? await _mongoDb.GetSubmissionByStudentAndTaskAsync(user.Id, contentItem.Id!) : null;

            var instructorName = !string.IsNullOrWhiteSpace(classItem.InstructorName)
                ? classItem.InstructorName
                : (!string.IsNullOrWhiteSpace(classItem.CreatorName)
                    ? classItem.CreatorName
                    : "Instructor");
            var instructorInitials = !string.IsNullOrWhiteSpace(classItem.CreatorInitials)
                ? classItem.CreatorInitials
                : GetInitials(instructorName);
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

            var recentTasksNav = (await _mongoDb.GetContentsForClassAsync(classItem.Id, classItem.ClassCode))?
                .Where(c => c.Type == "task")
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .Select(c => new StudentTaskRecentNavItem
                {
                    Title = string.IsNullOrWhiteSpace(c.Title) ? "(Untitled)" : c.Title!,
                    ContentId = c.Id ?? "",
                    IsCurrent = c.Id == contentItem.Id
                })
                .ToList() ?? new System.Collections.Generic.List<StudentTaskRecentNavItem>();

            var vm = new StudentTaskViewModel
            {
                TaskId = contentItem.Id ?? contentId,
                ClassCode = classItem.ClassCode ?? classCode,
                SubjectName = classItem.SubjectName ?? string.Empty,
                SectionName = !string.IsNullOrWhiteSpace(classItem.SectionLabel) ? classItem.SectionLabel : (classItem.Section ?? string.Empty),
                SubjectCode = classItem.SubjectCode ?? string.Empty,
                InstructorName = instructorName,
                InstructorInitials = string.IsNullOrWhiteSpace(instructorInitials) ? "IN" : instructorInitials,
                InstructorRole = roleLabel,
                TeacherDepartment = teacherDepartment,
                RoomName = roomName,
                FloorDisplay = floorDisplay,
                TaskTitle = contentItem.Title ?? string.Empty,
                Description = contentItem.Description ?? string.Empty,
                PostedDate = contentItem.CreatedAt.ToLocalTime().ToString("MMM d, yyyy"),
                Deadline = contentItem.Deadline?.ToLocalTime().ToString("MMM d, yyyy") ?? "",
                DeadlineDate = contentItem.Deadline,
                StudentName = user?.FullName ?? "Student",
                StudentEmail = user?.Email ?? string.Empty,
                StudentInitials = GetInitials(user?.FullName ?? "ST"),
                Attachments = files.Select(f => new TaskAttachment { FileName = f.FileName, FileUrl = f.FileUrl }).ToList(),
                IsSubmitted = existing?.Submitted ?? false,
                SubmittedAt = existing?.SubmittedAt,
                IsApproved = existing?.IsApproved ?? false,
                HasPassed = existing?.HasPassed ?? false,
                ApprovedDate = existing?.ApprovedDate,
                Grade = existing?.Grade ?? string.Empty,
                Feedback = !string.IsNullOrWhiteSpace(existing?.TeacherRemarks) ? existing!.TeacherRemarks : (existing?.Feedback ?? string.Empty),
                PrivateComment = existing?.PrivateComment ?? string.Empty,
                SubmittedFileName = existing?.FileName ?? string.Empty,
                SubmittedFileUrl = existing?.FileUrl ?? string.Empty,
                SubmittedFileSize = existing?.FileSize > 0 ? FormatFileSize(existing.FileSize) : string.Empty,
                RecentTasks = recentTasksNav,
                IsSubmissionLocked = ContentSubmissionRules.IsSubmissionLocked(contentItem, System.DateTime.UtcNow)
            };

            ViewBag.InstructorName = instructorName;
            ViewBag.InstructorInitials = vm.InstructorInitials;
            ViewBag.SubjectCode = vm.SubjectCode;

            return View("~/Views/StudentDb/StudentTask/Index.cshtml", vm);
        }

        [HttpGet("/StudentTask/GetComments")]
        public async Task<IActionResult> GetComments([FromQuery] string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return BadRequest(new { success = false, message = "Missing taskId" });
            var items = await _mongoDb.GetTaskCommentsAsync(taskId);
            var comments = items.Select(c => new
            {
                id = c.Id,
                authorName = c.AuthorName,
                role = NormalizeCommentRole(c.Role),
                text = c.Text,
                createdAt = c.CreatedAt,
                replies = c.Replies.Select(r => new { authorName = r.AuthorName, role = NormalizeCommentRole(r.Role), text = r.Text, createdAt = r.CreatedAt }).ToList()
            }).ToList();
            return Json(new { success = true, comments });
        }

        [HttpPost("/StudentTask/PostComment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostComment(string taskId, string classCode, string text)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            var user = !string.IsNullOrEmpty(email) ? await _mongoDb.GetUserByEmailAsync(email) : null;
            var authorName = user?.FullName ?? (User?.Identity?.Name ?? "Student");
            var authorEmail = user?.Email ?? (email ?? "student@local");
            var role = NormalizeCommentRole(user?.Role ?? "Student");
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null) return Json(new { success = false, message = "Class not found" });
            var item = await _mongoDb.AddTaskCommentAsync(taskId, classItem.Id, authorEmail, authorName, role, text ?? string.Empty);
            if (item == null) return Json(new { success = false, message = "Failed to add comment" });
            return Json(new { success = true, comment = new { id = item.Id, authorName = item.AuthorName, role = NormalizeCommentRole(item.Role), text = item.Text, createdAt = item.CreatedAt, replies = item.Replies.Select(r => new { authorName = r.AuthorName, role = NormalizeCommentRole(r.Role), text = r.Text, createdAt = r.CreatedAt }).ToList() } });
        }

        [HttpPost("/StudentTask/PostReply")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostReply(string commentId, string text)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            var user = !string.IsNullOrEmpty(email) ? await _mongoDb.GetUserByEmailAsync(email) : null;
            var authorName = user?.FullName ?? (User?.Identity?.Name ?? "Student");
            var authorEmail = user?.Email ?? (email ?? "student@local");
            var role = NormalizeCommentRole(user?.Role ?? "Student");
            var updated = await _mongoDb.AddTaskReplyAsync(commentId, authorEmail, authorName, role, text ?? string.Empty);
            if (updated == null) return Json(new { success = false, message = "Failed to add reply" });
            var last = updated.Replies.LastOrDefault();
            return Json(new { success = true, reply = last != null ? new { authorName = last.AuthorName, role = NormalizeCommentRole(last.Role), text = last.Text, createdAt = last.CreatedAt } : null });
        }

        [HttpPost("/StudentTask/SubmitTask")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitTask(string taskId, string classCode, string privateComment, string studentEmail)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email)) email = studentEmail;
            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (user == null)
                return Json(new { success = false, message = "User not found" });

            var task = await _mongoDb.GetContentByIdAsync(taskId);
            if (task == null || task.Type != "task")
                return Json(new { success = false, message = "Task not found" });
            if (ContentSubmissionRules.IsSubmissionLocked(task, System.DateTime.UtcNow))
                return Json(new { success = false, message = "The deadline has passed. Submissions are closed." });

            var submission = await _mongoDb.GetSubmissionByStudentAndTaskAsync(user.Id!, taskId) ?? new StudentPortal.Models.Submission
            {
                StudentId = user.Id!,
                StudentName = user.FullName,
                StudentEmail = user.Email,
                TaskId = taskId
            };

            submission.PrivateComment = privateComment ?? string.Empty;
            submission.Submitted = true;
            submission.SubmittedAt = System.DateTime.UtcNow;

            var file = Request.Form.Files["file"];
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Please attach a file to submit" });
            }
            string? fileName = null;
            string? fileUrl = null;
            long fileSize = 0;

            if (file != null && file.Length > 0)
            {
                var uploadsRoot = System.IO.Path.Combine(_env.WebRootPath, "uploads", taskId);
                System.IO.Directory.CreateDirectory(uploadsRoot);
                fileName = System.IO.Path.GetFileName(file.FileName);
                var savePath = System.IO.Path.Combine(uploadsRoot, fileName);
                using (var stream = new System.IO.FileStream(savePath, System.IO.FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                fileUrl = $"/uploads/{taskId}/{fileName}";
                fileSize = file.Length;

                submission.FileName = fileName;
                submission.FileUrl = fileUrl;
                submission.FileSize = fileSize;
            }

            await _mongoDb.CreateOrUpdateSubmissionAsync(submission);

            return Json(new
            {
                success = true,
                message = "Task submitted successfully",
                submittedFileName = fileName,
                submittedFileUrl = fileUrl,
                submittedFileSize = fileSize > 0 ? FormatFileSize(fileSize) : null
            });
        }

        [HttpPost("/StudentTask/MarkAsDone")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsDone(string taskId, string classCode, string privateComment, string studentEmail)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email)) email = studentEmail;
            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (user == null)
                return Json(new { success = false, message = "User not found" });

            var taskContent = await _mongoDb.GetContentByIdAsync(taskId);
            if (taskContent == null || taskContent.Type != "task")
                return Json(new { success = false, message = "Task not found" });
            if (ContentSubmissionRules.IsSubmissionLocked(taskContent, System.DateTime.UtcNow))
                return Json(new { success = false, message = "The deadline has passed. Submissions are closed." });

            var submission = await _mongoDb.GetSubmissionByStudentAndTaskAsync(user.Id!, taskId) ?? new StudentPortal.Models.Submission
            {
                StudentId = user.Id!,
                StudentName = user.FullName,
                StudentEmail = user.Email,
                TaskId = taskId
            };

            submission.PrivateComment = privateComment ?? string.Empty;
            submission.Submitted = true;
            submission.SubmittedAt = System.DateTime.UtcNow;

            var file = Request.Form.Files["file"];
            string? fileName = null;
            string? fileUrl = null;
            long fileSize = 0;

            if (file != null && file.Length > 0)
            {
                var uploadsRoot = System.IO.Path.Combine(_env.WebRootPath, "uploads", taskId);
                System.IO.Directory.CreateDirectory(uploadsRoot);
                fileName = System.IO.Path.GetFileName(file.FileName);
                var savePath = System.IO.Path.Combine(uploadsRoot, fileName);
                using (var stream = new System.IO.FileStream(savePath, System.IO.FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                fileUrl = $"/uploads/{taskId}/{fileName}";
                fileSize = file.Length;

                submission.FileName = fileName;
                submission.FileUrl = fileUrl;
                submission.FileSize = fileSize;
            }

            await _mongoDb.CreateOrUpdateSubmissionAsync(submission);

            return Json(new {
                success = true,
                message = "Marked as done",
                submittedFileName = fileName,
                submittedFileUrl = fileUrl,
                submittedFileSize = fileSize > 0 ? FormatFileSize(fileSize) : null
            });
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            var kb = bytes / 1024d;
            if (kb < 1024) return $"{kb:0.#} KB";
            var mb = kb / 1024d;
            return $"{mb:0.#} MB";
        }

        private string GetInitials(string name)
        {
            var parts = (name ?? string.Empty).Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return ($"{parts[0][0]}{parts[^1][0]}").ToUpper();
            if (parts.Length == 1) return parts[0].Substring(0, System.Math.Min(2, parts[0].Length)).ToUpper();
            return "ST";
        }

        private static string NormalizeCommentRole(string? role)
        {
            if (string.IsNullOrWhiteSpace(role)) return "Student";
            return string.Equals(role.Trim(), "Professor", System.StringComparison.OrdinalIgnoreCase)
                ? "Teacher"
                : role.Trim();
        }
    }
}
