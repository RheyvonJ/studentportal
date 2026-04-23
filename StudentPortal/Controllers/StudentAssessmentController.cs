using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.StudentDb;
using StudentPortal.Services;
using StudentPortal.Utilities;
using StudentPortal.Models.AdminDb;
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

        private async Task<bool> IsAssessmentLockedForStudentAsync(string classCode, string contentId)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email)) return false;

            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null) return false;

            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            if (contentItem == null || contentItem.Type != "assessment") return false;

            if (contentItem.ClassId != classItem.Id) return false;

            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (user == null) return false;

            var resolvedContentId = AssessmentAntiCheatRules.ResolveAssessmentContentId(contentItem.Id, contentId);
            var assessmentResult = await _mongoDb.GetAssessmentResultForStudentAsync(classItem.Id, resolvedContentId, user);
            if (assessmentResult?.IntegrityLockedAtUtc != null)
                return true;

            var logs = await _mongoDb.GetAntiCheatLogsAsync(classItem.Id, resolvedContentId);
            var relevant = logs ?? new List<AntiCheatLog>();
            var unlock = await _mongoDb.GetAssessmentUnlockAsync(classItem.Id, resolvedContentId, user.Id ?? string.Empty);
            var studentTotalForLock = AssessmentAntiCheatRules.SumIntegrityEventsForLock(relevant, user.Id, user.Email, unlock);
            return AssessmentAntiCheatRules.IsIntegrityLockActive(studentTotalForLock);
        }

        [HttpGet("/StudentAssessment/{classCode}/{contentId}/quiz-lock-status")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> GetQuizLockStatus(string classCode, string contentId)
        {
            if (string.IsNullOrWhiteSpace(classCode) || string.IsNullOrWhiteSpace(contentId))
                return BadRequest(new { success = false, message = "Missing identifiers." });

            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
                return Unauthorized(new { success = false, message = "Not authenticated." });

            var isLocked = await IsAssessmentLockedForStudentAsync(classCode, contentId);
            return Ok(new { success = true, isLocked });
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
            var files = await _mongoDb.GetUploadsByContentIdAsync(contentId);
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

            var model = new StudentAssessmentViewModel
            {
                AssessmentTitle = contentItem.Title ?? "Assessment",
                Description = contentItem.Description ?? string.Empty,
                PostedDate = contentItem.CreatedAt.ToLocalTime().ToString("MMM d, yyyy"),
                Deadline = contentItem.Deadline?.ToLocalTime().ToString("MMM d, yyyy") ?? string.Empty,
                StudentName = user?.FullName ?? "Student",
                StudentInitials = GetInitials(user?.FullName ?? "ST"),
                InstructorName = instructorName,
                InstructorInitials = string.IsNullOrWhiteSpace(instructorInitials) ? "IN" : instructorInitials,
                InstructorRole = roleLabel,
                TeacherDepartment = teacherDepartment,
                RoomName = roomName,
                FloorDisplay = floorDisplay,
                Attachments = (files ?? new List<StudentPortal.Models.AdminMaterial.UploadItem>())
                    .Select(f => new StudentPortal.Models.StudentDb.TaskAttachment
                    {
                        FileName = f.FileName,
                        FileUrl = f.FileUrl
                    })
                    .ToList(),
                ClassCode = classItem.ClassCode ?? classCode,
                SectionName = !string.IsNullOrWhiteSpace(classItem.SectionLabel) ? classItem.SectionLabel : (classItem.Section ?? string.Empty),
                SubjectName = classItem.SubjectName ?? string.Empty,
                SubjectCode = classItem.SubjectCode ?? string.Empty,
                ContentId = AssessmentAntiCheatRules.ResolveAssessmentContentId(contentItem.Id, contentId),
                IsSubmissionLocked = ContentSubmissionRules.IsSubmissionLocked(contentItem, System.DateTime.UtcNow)
            };

            // Fallback for legacy records where uploads collection may be empty.
            if ((model.Attachments == null || model.Attachments.Count == 0) && contentItem.Attachments != null && contentItem.Attachments.Count > 0)
            {
                model.Attachments = contentItem.Attachments
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => new StudentPortal.Models.StudentDb.TaskAttachment
                    {
                        FileName = x,
                        FileUrl = $"/uploads/{x}"
                    })
                    .ToList();
            }

            bool isDone = false;
            StudentPortal.Models.StudentDb.AssessmentResult? assessmentResult = null;
            var resolvedContentIdSt = AssessmentAntiCheatRules.ResolveAssessmentContentId(contentItem.Id, contentId);
            if (user != null)
            {
                assessmentResult = await _mongoDb.GetAssessmentResultForStudentAsync(classItem.Id, resolvedContentIdSt, user);
                if (assessmentResult != null)
                {
                    model.IsAnswered = assessmentResult.SubmittedAt.HasValue;
                    model.Score = assessmentResult.Score;
                    model.MaxScore = assessmentResult.MaxScore;
                    isDone = string.Equals(assessmentResult.Status, "done", System.StringComparison.OrdinalIgnoreCase);
                }
            }

            var deadlineLocked = ContentSubmissionRules.IsSubmissionLocked(contentItem, System.DateTime.UtcNow);
            StudentPortal.Models.AdminDb.AssessmentUnlock? unlock = null;
            var auditIntegrityLock = false;
            try
            {
                var logs = await _mongoDb.GetAntiCheatLogsAsync(classItem.Id, resolvedContentIdSt);
                var relevant = logs ?? new System.Collections.Generic.List<AntiCheatLog>();
                unlock = await _mongoDb.GetAssessmentUnlockAsync(classItem.Id, resolvedContentIdSt, user?.Id ?? string.Empty);
                var studentTotalForLock = AssessmentAntiCheatRules.SumIntegrityEventsForLock(relevant, user?.Id, user?.Email, unlock);
                auditIntegrityLock = AssessmentAntiCheatRules.IsIntegrityLockActive(studentTotalForLock);
            }
            catch
            {
                auditIntegrityLock = false;
            }

            var pinnedIntegrityLock = assessmentResult?.IntegrityLockedAtUtc != null;
            // Do not gate on unlock.Unlocked: log totals already exclude pre-unlock events; a new lock after restore must still void the quiz.
            var isVoidForStudent = pinnedIntegrityLock || auditIntegrityLock;
            ViewBag.IsAssessmentVoid = isVoidForStudent;
            ViewBag.IsOpenQuizLocked = isVoidForStudent || isDone || deadlineLocked || model.IsAnswered;

            ViewBag.InstructorName = instructorName;
            ViewBag.InstructorInitials = model.InstructorInitials;
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
                role = NormalizeCommentRole(c.Role),
                text = c.Text,
                createdAt = c.CreatedAt,
                replies = c.Replies.Select(r => new { authorName = r.AuthorName, role = NormalizeCommentRole(r.Role), text = r.Text, createdAt = r.CreatedAt }).ToList()
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
            var role = NormalizeCommentRole(user?.Role ?? "Student");
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
                    role = NormalizeCommentRole(item.Role),
                    text = item.Text,
                    createdAt = item.CreatedAt,
                    replies = item.Replies.Select(r => new { authorName = r.AuthorName, role = NormalizeCommentRole(r.Role), text = r.Text, createdAt = r.CreatedAt }).ToList()
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
            var role = NormalizeCommentRole(user?.Role ?? "Student");
            var updated = await _mongoDb.AddTaskReplyAsync(commentId, authorEmail, authorName, role, text ?? string.Empty);
            if (updated == null) return Json(new { success = false, message = "Failed to add reply" });
            var last = updated.Replies.LastOrDefault();
            return Json(new { success = true, reply = last != null ? new { authorName = last.AuthorName, role = NormalizeCommentRole(last.Role), text = last.Text, createdAt = last.CreatedAt } : null });
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

        [HttpPost]
        public async Task<IActionResult> MarkAsDone(string classCode, string contentId)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Index", "StudentDb");
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (classItem == null || contentItem == null || user == null) return RedirectToAction("Index", new { classCode, contentId });
            if (ContentSubmissionRules.IsSubmissionLocked(contentItem, System.DateTime.UtcNow))
            {
                TempData["ToastMessage"] = "The deadline has passed. Submissions are closed.";
                return RedirectToAction("Index", new { classCode, contentId });
            }
            var resolvedContentIdMd = AssessmentAntiCheatRules.ResolveAssessmentContentId(contentItem.Id, contentId);
            var resPinned = await _mongoDb.GetAssessmentResultForStudentAsync(classItem.Id, resolvedContentIdMd, user);
            if (resPinned?.IntegrityLockedAtUtc != null)
            {
                TempData["ToastMessage"] = "This assessment is locked due to integrity alerts. Contact your instructor.";
                return RedirectToAction("Index", new { classCode, contentId });
            }
            var logsMd = await _mongoDb.GetAntiCheatLogsAsync(classItem.Id, resolvedContentIdMd);
            var relevantMd = logsMd ?? new System.Collections.Generic.List<AntiCheatLog>();
            var unlockMd = await _mongoDb.GetAssessmentUnlockAsync(classItem.Id, resolvedContentIdMd, user.Id ?? string.Empty);
            var totalMdForLock = AssessmentAntiCheatRules.SumIntegrityEventsForLock(relevantMd, user.Id, user.Email, unlockMd);
            if (AssessmentAntiCheatRules.IsIntegrityLockActive(totalMdForLock))
            {
                TempData["ToastMessage"] = "This assessment is locked due to integrity alerts. Contact your instructor.";
                return RedirectToAction("Index", new { classCode, contentId });
            }
            await _mongoDb.MarkAssessmentDoneAsync(classItem.Id, classItem.ClassCode, resolvedContentIdMd, user.Id ?? string.Empty, user.Email ?? string.Empty);
            TempData["ToastMessage"] = "✅ Assessment marked as done!";
            return RedirectToAction("Index", new { classCode, contentId });
        }
    }
}
