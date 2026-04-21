using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.AdminTask;
using StudentPortal.Models.AdminMaterial;
using StudentPortal.Services;
using StudentPortal.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers
{
    public class AdminTaskController : Controller
    {
        private readonly MongoDbService _mongoDb;
        private readonly IWebHostEnvironment _env;

        public AdminTaskController(MongoDbService mongoDb, IWebHostEnvironment env)
        {
            _mongoDb = mongoDb;
            _env = env;
        }

        [HttpGet("/AdminTask/{classCode}/{contentId}")]
        public async Task<IActionResult> Index(string classCode, string contentId)
        {
            if (string.IsNullOrEmpty(classCode) || string.IsNullOrEmpty(contentId))
                return NotFound("Class code or Content ID not provided.");

            // Get content item from database
            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            if (contentItem == null || contentItem.Type != "task")
                return NotFound("Task not found.");

            // Get class information using class code
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null)
                return NotFound("Class not found.");

            // Verify that the content belongs to this class
            if (contentItem.ClassId != classItem.Id)
                return NotFound("Task not found in this class.");

            var submissions = await _mongoDb.GetTaskSubmissionsAsync(contentId);
            var vmSubmissions = submissions.Select(s => new TaskSubmission
            {
                Id = s.Id,
                FullName = s.StudentName,
                StudentEmail = s.StudentEmail,
                Submitted = s.Submitted,
                SubmittedAt = s.SubmittedAt,
                IsApproved = s.IsApproved,
                HasPassed = s.HasPassed,
                ApprovedDate = s.ApprovedDate,
                Grade = s.Grade,
                Feedback = !string.IsNullOrWhiteSpace(s.TeacherRemarks) ? s.TeacherRemarks : s.Feedback,
                SubmittedFileName = s.FileName,
                SubmittedFileUrl = s.FileUrl,
                SubmittedFileSize = FormatFileSize(s.FileSize)
            }).ToList();

            var professorName = HttpContext.Session.GetString("UserName") ?? "Professor";
            var professorEmail = HttpContext.Session.GetString("UserEmail") ?? (classItem.OwnerEmail ?? string.Empty);
            string teacherRole = "Professor";
            string teacherDepartment = "";
            string roomName = "";
            string floorDisplay = "";
            if (!string.IsNullOrWhiteSpace(professorEmail))
            {
                var prof = await _mongoDb.GetProfessorByEmailAsync(professorEmail);
                var full = prof?.GetFullName();
                if (!string.IsNullOrWhiteSpace(full)) professorName = full;
                var role = prof?.FacultyRole ?? prof?.GetRole() ?? "";
                if (!string.IsNullOrWhiteSpace(role)) teacherRole = role;
                teacherDepartment = await _mongoDb.GetProfessorDepartmentByEmailAsync(professorEmail);
                if (string.IsNullOrWhiteSpace(teacherDepartment) && prof?.Programs != null && prof.Programs.Count > 0)
                    teacherDepartment = prof.Programs[0];

                try
                {
                    var (tRole, tDept) = await _mongoDb.GetTeacherRoleAndDepartmentByEmailAsync(professorEmail);
                    if (!string.IsNullOrWhiteSpace(tRole)) teacherRole = tRole;
                    if (!string.IsNullOrWhiteSpace(tDept)) teacherDepartment = tDept;
                }
                catch { }

                try
                {
                    var items = await _mongoDb.GetClassMeetingsForProfessorFlexibleAsync(prof?.Id, prof?.GetFullName(), professorEmail) ?? new System.Collections.Generic.List<MongoDB.Bson.BsonDocument>();
                    foreach (var d in items)
                    {
                        if (d.TryGetValue("roomName", out var rn) && !string.IsNullOrWhiteSpace(rn.ToString()))
                        {
                            roomName = rn.ToString();
                            break;
                        }
                    }
                }
                catch { }
                if (!string.IsNullOrWhiteSpace(roomName))
                {
                    var digits = new string(roomName.Where(char.IsDigit).ToArray());
                    if (!string.IsNullOrWhiteSpace(digits))
                    {
                        var first = digits[0];
                        int floorNum = int.TryParse(first.ToString(), out var n) ? n : 0;
                        if (floorNum > 0)
                        {
                            floorDisplay = floorNum == 1 ? "1st Floor" : floorNum == 2 ? "2nd Floor" : floorNum == 3 ? "3rd Floor" : $"{floorNum}th Floor";
                        }
                    }
                }
            }
            var displayName = professorName;
            var displayInitials = GetInitials(displayName);

            if (!string.IsNullOrWhiteSpace(classItem.ScheduleId))
            {
                try
                {
                    var (schedRoom, schedFloor) = await _mongoDb.GetRoomAndFloorByScheduleIdAsync(classItem.ScheduleId);
                    if (!string.IsNullOrWhiteSpace(schedRoom)) roomName = schedRoom;
                    if (!string.IsNullOrWhiteSpace(schedFloor)) floorDisplay = schedFloor;
                }
                catch { }
            }

            var taskList = (await _mongoDb.GetContentsForClassAsync(classItem.Id, classItem.ClassCode))?
                .Where(c => c.Type == "task")
                .OrderByDescending(c => c.CreatedAt)
                .ToList() ?? new System.Collections.Generic.List<ContentItem>();
            // Recent sidebar: current first + up to 2 others (matches AdminMaterial recent-uploads)
            var recentTasksNav = new System.Collections.Generic.List<AdminTaskRecentNavItem>();
            var currentTask = taskList.FirstOrDefault(c => c.Id == contentItem.Id);
            var otherTasks = taskList.Where(c => c.Id != contentItem.Id).Take(2).ToList();
            if (currentTask != null)
            {
                recentTasksNav.Add(new AdminTaskRecentNavItem
                {
                    Title = string.IsNullOrWhiteSpace(currentTask.Title) ? "(Untitled)" : currentTask.Title,
                    ContentId = currentTask.Id ?? "",
                    IsCurrent = true
                });
            }
            foreach (var c in otherTasks)
            {
                recentTasksNav.Add(new AdminTaskRecentNavItem
                {
                    Title = string.IsNullOrWhiteSpace(c.Title) ? "(Untitled)" : c.Title,
                    ContentId = c.Id ?? "",
                    IsCurrent = false
                });
            }

            var vm = new AdminTaskViewModel
            {
                TaskId = contentItem.Id,
                ClassId = classItem.Id,
                SubjectName = classItem.SubjectName,
                SectionName = !string.IsNullOrWhiteSpace(classItem.SectionLabel) ? classItem.SectionLabel : (classItem.Section ?? string.Empty),
                SubjectCode = classItem.SubjectCode,
                ClassCode = classItem.ClassCode,
                InstructorName = displayName,
                InstructorInitials = displayInitials,
                TeacherRole = teacherRole,
                TeacherDepartment = teacherDepartment,
                RoomName = roomName,
                FloorDisplay = floorDisplay,
                TaskTitle = contentItem.Title,
                TaskDescription = contentItem.Description,
                Attachments = contentItem.Attachments ?? new System.Collections.Generic.List<string>(),
                PostedDate = contentItem.CreatedAt.ToLocalTime(),
                Deadline = contentItem.Deadline?.ToLocalTime(),
                AllowSubmissionsPastDeadline = contentItem.AllowSubmissionsPastDeadline,
                IsSubmissionLockedForStudents = ContentSubmissionRules.IsSubmissionLocked(contentItem, DateTime.UtcNow),
                EditedDate = contentItem.UpdatedAt.ToLocalTime(),
                Submissions = vmSubmissions,
                TaskMaxGrade = contentItem.MaxGrade > 0 ? contentItem.MaxGrade : 100,
                RecentTasks = recentTasksNav
            };

            return View("~/Views/AdminDb/AdminTask/Index.cshtml", vm);
        }

        [HttpGet("/AdminTask/DownloadFile/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName, string taskId)
        {
            try
            {
                var uploadItem = await _mongoDb.GetUploadByFileNameAsync(fileName, taskId);
                if (uploadItem == null)
                    return NotFound("File not found.");

                var url = uploadItem.FileUrl ?? string.Empty;
                var fileWithGuid = System.IO.Path.GetFileName(url);
                var uploadsDir = System.IO.Path.Combine(_env.WebRootPath, "uploads");
                var physPath = !string.IsNullOrWhiteSpace(fileWithGuid)
                    ? System.IO.Path.Combine(uploadsDir, fileWithGuid)
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(physPath) && System.IO.File.Exists(physPath))
                {
                    var ext = System.IO.Path.GetExtension(fileWithGuid).ToLowerInvariant();
                    var contentType = ext == ".xlsx" ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                                   : ext == ".pdf" ? "application/pdf"
                                   : ext == ".docx" ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                                   : "application/octet-stream";
                    Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
                    return PhysicalFile(physPath, contentType, fileName);
                }

                // Fallback: search uploads directory for a file ending with _<originalName>
                try
                {
                    if (System.IO.Directory.Exists(uploadsDir))
                    {
                        var match = System.IO.Directory.EnumerateFiles(uploadsDir, "*_" + fileName).FirstOrDefault();
                        if (!string.IsNullOrEmpty(match) && System.IO.File.Exists(match))
                        {
                            var ext2 = System.IO.Path.GetExtension(match).ToLowerInvariant();
                            var contentType2 = ext2 == ".xlsx" ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                                           : ext2 == ".pdf" ? "application/pdf"
                                           : ext2 == ".docx" ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                                           : "application/octet-stream";
                            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
                            return PhysicalFile(match, contentType2, fileName);
                        }
                    }
                }
                catch { }

                if (!string.IsNullOrWhiteSpace(url))
                {
                    return Redirect(url);
                }

                return NotFound("File URL not available.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("/AdminTask/ViewFile/{fileName}")]
        public async Task<IActionResult> ViewFile(string fileName, string taskId)
        {
            try
            {
                var uploadItem = await _mongoDb.GetUploadByFileNameAsync(fileName, taskId);
                if (uploadItem == null)
                    return NotFound("File not found.");

                var url = uploadItem.FileUrl ?? string.Empty;
                var fileWithGuid = System.IO.Path.GetFileName(url);
                var uploadsDir = System.IO.Path.Combine(_env.WebRootPath, "uploads");
                var physPath = !string.IsNullOrWhiteSpace(fileWithGuid)
                    ? System.IO.Path.Combine(uploadsDir, fileWithGuid)
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(physPath) && System.IO.File.Exists(physPath))
                {
                    var ext = System.IO.Path.GetExtension(fileWithGuid).ToLowerInvariant();
                    var contentType = ext == ".pdf" ? "application/pdf"
                                   : ext == ".png" ? "image/png"
                                   : ext == ".jpg" || ext == ".jpeg" ? "image/jpeg"
                                   : ext == ".gif" ? "image/gif"
                                   : ext == ".txt" ? "text/plain"
                                   : ext == ".html" ? "text/html"
                                   : ext == ".xlsx" ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                                   : ext == ".docx" ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                                   : "application/octet-stream";
                    Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
                    return PhysicalFile(physPath, contentType);
                }

                if (!string.IsNullOrWhiteSpace(url))
                {
                    return Redirect(url);
                }

                return NotFound("File URL not available.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("/AdminTask/GetComments")]
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

        [HttpPost("/AdminTask/PostComment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostComment(string taskId, string classCode, string text)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            var user = !string.IsNullOrEmpty(email) ? await _mongoDb.GetUserByEmailAsync(email) : null;
            var authorName = user?.FullName ?? (User?.Identity?.Name ?? "Admin");
            var authorEmail = user?.Email ?? (email ?? "admin@local");
            var role = NormalizeCommentRole(user?.Role ?? "Admin");
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null) return Json(new { success = false, message = "Class not found" });
            var item = await _mongoDb.AddTaskCommentAsync(taskId, classItem.Id, authorEmail, authorName, role, text ?? string.Empty);
            if (item == null) return Json(new { success = false, message = "Failed to add comment" });
            return Json(new { success = true, comment = new { id = item.Id, authorName = item.AuthorName, role = NormalizeCommentRole(item.Role), text = item.Text, createdAt = item.CreatedAt, replies = item.Replies.Select(r => new { authorName = r.AuthorName, role = NormalizeCommentRole(r.Role), text = r.Text, createdAt = r.CreatedAt }).ToList() } });
        }

        [HttpPost("/AdminTask/PostReply")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostReply(string commentId, string text)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            var user = !string.IsNullOrEmpty(email) ? await _mongoDb.GetUserByEmailAsync(email) : null;
            var authorName = user?.FullName ?? (User?.Identity?.Name ?? "Admin");
            var authorEmail = user?.Email ?? (email ?? "admin@local");
            var role = NormalizeCommentRole(user?.Role ?? "Admin");
            var updated = await _mongoDb.AddTaskReplyAsync(commentId, authorEmail, authorName, role, text ?? string.Empty);
            if (updated == null) return Json(new { success = false, message = "Failed to add reply" });
            var last = updated.Replies.LastOrDefault();
            return Json(new { success = true, reply = last != null ? new { authorName = last.AuthorName, role = NormalizeCommentRole(last.Role), text = last.Text, createdAt = last.CreatedAt } : null });
        }

        [HttpPost("/AdminTask/UpdateTask")]
        public async Task<IActionResult> UpdateTask([FromBody] UpdateTaskRequest request)
        {
            try
            {
                var contentItem = await _mongoDb.GetContentByIdAsync(request.TaskId);
                if (contentItem == null || contentItem.Type != "task")
                    return NotFound(new { success = false, message = "Task not found" });

                // Update task properties
                contentItem.Title = request.Title;
                contentItem.Description = request.Description;
                contentItem.Deadline = request.Deadline;
                if (request.AllowSubmissionsPastDeadline.HasValue)
                    contentItem.AllowSubmissionsPastDeadline = request.AllowSubmissionsPastDeadline.Value;
                contentItem.UpdatedAt = DateTime.UtcNow;

                // Update meta text to reflect changes
                contentItem.MetaText = GenerateUpdatedMetaText(contentItem);

                await _mongoDb.UpdateContentAsync(contentItem);

                return Ok(new { success = true, message = "Task updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("/AdminTask/DeleteTask")]
        public async Task<IActionResult> DeleteTask([FromBody] DeleteTaskRequest request)
        {
            try
            {
                await _mongoDb.DeleteContentAsync(request.TaskId);
                await _mongoDb.DeleteUploadsByContentIdAsync(request.TaskId);
                return Ok(new { success = true, message = "Task deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("/AdminTask/ReplaceAttachment")]
        public async Task<IActionResult> ReplaceAttachment([FromBody] ReplaceTaskAttachmentRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.TaskId) || string.IsNullOrEmpty(request.FileName))
                    return BadRequest(new { success = false, message = "Invalid request" });

                var contentItem = await _mongoDb.GetContentByIdAsync(request.TaskId);
                if (contentItem == null || contentItem.Type != "task")
                    return NotFound(new { success = false, message = "Task not found" });

                contentItem.Attachments = new System.Collections.Generic.List<string> { request.FileName };
                contentItem.UpdatedAt = DateTime.UtcNow;
                contentItem.MetaText = GenerateUpdatedMetaText(contentItem);
                await _mongoDb.UpdateContentAsync(contentItem);

                var uploads = await _mongoDb.GetUploadsByClassIdAsync(contentItem.ClassId);
                var recentUpload = uploads
                    .Where(u => u.FileName == request.FileName && string.IsNullOrEmpty(u.ContentId))
                    .OrderByDescending(u => u.UploadedAt)
                    .FirstOrDefault();

                if (recentUpload != null)
                {
                    recentUpload.ContentId = contentItem.Id ?? string.Empty;
                    await _mongoDb.UpdateUploadAsync(recentUpload);
                }

                return Ok(new { success = true, message = "Attachment replaced" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        public class ReplaceTaskAttachmentRequest
        {
            public string TaskId { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public string? FileUrl { get; set; }
        }

        [HttpPost("/AdminTask/AddAttachment")]
        public async Task<IActionResult> AddAttachment([FromBody] AddAttachmentRequest request)
        {
            try
            {
                var contentItem = await _mongoDb.GetContentByIdAsync(request.TaskId);
                if (contentItem == null || contentItem.Type != "task")
                    return NotFound(new { success = false, message = "Task not found" });

                if (!contentItem.Attachments.Contains(request.FileName))
                {
                    contentItem.Attachments.Add(request.FileName);
                    contentItem.UpdatedAt = DateTime.UtcNow;
                    contentItem.MetaText = GenerateUpdatedMetaText(contentItem);
                    await _mongoDb.UpdateContentAsync(contentItem);
                }

                return Ok(new { success = true, message = "Attachment added successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("/AdminTask/GetSubmission")]        
        public async Task<IActionResult> GetSubmission([FromQuery] string submissionId)
        {
            if (string.IsNullOrEmpty(submissionId)) return BadRequest(new { success = false, message = "Missing submissionId" });
            var sub = await _mongoDb.GetSubmissionByIdAsync(submissionId);
            if (sub == null) return NotFound(new { success = false, message = "Submission not found" });
            var contentForSub = !string.IsNullOrEmpty(sub.TaskId) ? await _mongoDb.GetContentByIdAsync(sub.TaskId) : null;
            var taskMaxGrade = contentForSub?.MaxGrade > 0 ? contentForSub.MaxGrade : 100;
            return Json(new
            {
                success = true,
                taskMaxGrade,
                submission = new
                {
                    id = sub.Id,
                    studentName = sub.StudentName,
                    studentEmail = sub.StudentEmail,
                    submitted = sub.Submitted,
                    submittedAt = sub.SubmittedAt,
                    isApproved = sub.IsApproved,
                    hasPassed = sub.HasPassed,
                    approvedDate = sub.ApprovedDate,
                    grade = sub.Grade,
                    feedback = !string.IsNullOrWhiteSpace(sub.TeacherRemarks) ? sub.TeacherRemarks : sub.Feedback,
                    privateComment = sub.PrivateComment,
                    fileName = sub.FileName,
                    fileUrl = sub.FileUrl,
                    fileSize = sub.FileSize
                }
            });
        }

        [HttpPost("/AdminTask/GradeSubmission")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GradeSubmission(string submissionId, string grade, string feedback, bool? approve, bool? pass)
        {
            if (string.IsNullOrEmpty(submissionId)) return BadRequest(new { success = false, message = "Missing submissionId" });
            var isApproved = approve ?? true;

            var submission = await _mongoDb.GetSubmissionByIdAsync(submissionId);
            if (submission == null) return NotFound(new { success = false, message = "Submission not found" });
            var content = !string.IsNullOrEmpty(submission.TaskId) ? await _mongoDb.GetContentByIdAsync(submission.TaskId) : null;
            var maxGrade = (content?.MaxGrade ?? 0) > 0 ? content!.MaxGrade : 100;

            var gtext = (grade ?? string.Empty).Trim();
            string finalGrade = gtext;
            if (!string.IsNullOrEmpty(gtext))
            {
                if (gtext.Contains('/'))
                {
                    finalGrade = gtext;
                }
                else if (gtext.EndsWith("%"))
                {
                    var pctText = gtext.Substring(0, gtext.Length - 1);
                    if (double.TryParse(pctText, out var pct))
                    {
                        var got = Math.Round(maxGrade * (pct / 100.0), 2);
                        finalGrade = $"{got}/{maxGrade}";
                    }
                }
                else if (double.TryParse(gtext, out var gotNum))
                {
                    finalGrade = $"{gotNum}/{maxGrade}";
                }
            }

            bool computedPass = false;
            if (!string.IsNullOrEmpty(finalGrade) && finalGrade.Contains('/'))
            {
                var parts = finalGrade.Split('/');
                if (parts.Length == 2 && double.TryParse(parts[0], out var got) && double.TryParse(parts[1], out var max) && max > 0)
                {
                    var pct = (got / max) * 100.0;
                    computedPass = pct >= 75;
                }
            }
            else if (double.TryParse(gtext.TrimEnd('%'), out var pctNum))
            {
                computedPass = pctNum >= 75;
            }

            // Always derive pass/fail from stored task max + grade so client mistakes (wrong max, bad bool) cannot mark a full score as failed.
            var hasPassed = computedPass;
            var ok = await _mongoDb.UpdateSubmissionStatusAsync(submissionId, isApproved, hasPassed, finalGrade ?? string.Empty, feedback ?? string.Empty);
            if (!ok) return StatusCode(500, new { success = false, message = "Failed to update submission" });
            return Json(new { success = true, finalGrade = finalGrade ?? string.Empty, hasPassed });
        }

        private string GenerateUpdatedMetaText(ContentItem content)
        {
            var meta = $"Posted: {content.CreatedAt:MMM dd, yyyy}";

            if (content.Deadline.HasValue)
                meta += $" | Deadline: {content.Deadline.Value:MMM dd, yyyy}";

            if (content.UpdatedAt > content.CreatedAt)
                meta += $" | Edited: {content.UpdatedAt:MMM dd, yyyy}";

            var filesCount = content.Attachments?.Count ?? 0;
            if (filesCount > 0)
                meta += $" | Files: {filesCount}";

            return meta;
        }

        private static string GetInitials(string fullName)
        {
            var parts = fullName.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Select(p => p[0])).ToUpper();
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            var kb = bytes / 1024d;
            if (kb < 1024) return $"{kb:0.#} KB";
            var mb = kb / 1024d;
            return $"{mb:0.#} MB";
        }

        private static string NormalizeCommentRole(string? role)
        {
            if (string.IsNullOrWhiteSpace(role)) return "Teacher";
            return string.Equals(role.Trim(), "Professor", StringComparison.OrdinalIgnoreCase)
                ? "Teacher"
                : role.Trim();
        }

        // Request models
        [HttpPost("/AdminTask/SetSubmissionUnlock")]
        public async Task<IActionResult> SetSubmissionUnlock([FromBody] SetSubmissionUnlockRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.TaskId))
                    return BadRequest(new { success = false, message = "Invalid request" });
                var contentItem = await _mongoDb.GetContentByIdAsync(request.TaskId);
                if (contentItem == null || contentItem.Type != "task")
                    return NotFound(new { success = false, message = "Task not found" });
                contentItem.AllowSubmissionsPastDeadline = request.AllowPastDeadline;
                contentItem.UpdatedAt = DateTime.UtcNow;
                contentItem.MetaText = GenerateUpdatedMetaText(contentItem);
                await _mongoDb.UpdateContentAsync(contentItem);
                return Ok(new
                {
                    success = true,
                    allowPastDeadline = contentItem.AllowSubmissionsPastDeadline,
                    isLockedForStudents = ContentSubmissionRules.IsSubmissionLocked(contentItem, DateTime.UtcNow)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        public class SetSubmissionUnlockRequest
        {
            public string TaskId { get; set; } = "";
            public bool AllowPastDeadline { get; set; }
        }

        public class UpdateTaskRequest
        {
            public string TaskId { get; set; } = "";
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public DateTime? Deadline { get; set; }
            public bool? AllowSubmissionsPastDeadline { get; set; }
        }

        public class DeleteTaskRequest
        {
            public string TaskId { get; set; } = "";
        }

        public class AddAttachmentRequest
        {
            public string TaskId { get; set; } = "";
            public string FileName { get; set; } = "";
        }

        [HttpPost("/AdminTask/UpdateSubmissionStatus")]
        public async Task<IActionResult> UpdateSubmissionStatus([FromBody] UpdateSubmissionStatusRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.StudentId))
                    return BadRequest(new { success = false, message = "Missing submission identifier" });

                var ok = await _mongoDb.UpdateSubmissionStatusAsync(
                    request.StudentId,
                    request.IsApproved,
                    request.HasPassed,
                    request.Grade ?? string.Empty,
                    request.Feedback ?? string.Empty
                );

                if (!ok)
                    return NotFound(new { success = false, message = "Submission not found" });

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        public class UpdateSubmissionStatusRequest
        {
            public string StudentId { get; set; } = ""; // actually submissionId from UI
            public string TaskId { get; set; } = ""; // optional, unused for submissionId update
            public bool IsApproved { get; set; }
            public bool HasPassed { get; set; }
            public string? Grade { get; set; }
            public string? Feedback { get; set; }
        }

        [HttpGet("/AdminTask/GetSubmissionCounts")]
        public async Task<IActionResult> GetSubmissionCounts([FromQuery] string taskId)
        {
            if (string.IsNullOrEmpty(taskId))
                return BadRequest(new { success = false, message = "Missing taskId" });

            var total = await _mongoDb.GetSubmissionCountAsync(taskId, submittedOnly: false);
            var submitted = await _mongoDb.GetSubmissionCountAsync(taskId, submittedOnly: true);
            var approved = await _mongoDb.GetApprovedSubmissionCountAsync(taskId);

            return Ok(new { success = true, submittedCount = submitted, approvedCount = approved, totalCount = total });
        }
    }
}
