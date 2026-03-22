using Microsoft.AspNetCore.Mvc;
using SIA_IPT.Models.AdminAssessment;
using StudentPortal.Services;
using StudentPortal.Models.AdminDb;
using System.Linq;
using System.Text.Json;

namespace SIA_IPT.Controllers
{
    public class AdminAssessmentController : Controller
    {
        private readonly MongoDbService _mongoDb;

        public AdminAssessmentController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }
        [HttpGet("/AdminAssessment/{classCode}/{contentId}")]
        public async Task<IActionResult> Index(string classCode, string contentId)
        {
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            var content = await _mongoDb.GetContentByIdAsync(contentId);
            if (classItem == null || content == null || content.ClassId != classItem.Id || content.Type != "assessment")
            {
                return NotFound();
            }

            var recent = (await _mongoDb.GetContentsForClassAsync(classItem.Id, classItem.ClassCode))?
                .Where(c => c.Type == "assessment")
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => c.Title)
                .Take(3)
                .ToList() ?? new List<string>();
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
            var displayInitials = string.IsNullOrEmpty(displayName)
                ? "IN"
                : new string(displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0])).ToArray());
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

            var vm = new AdminAssessmentViewModel
            {
                AssessmentId = content.Id ?? string.Empty,
                SubjectName = classItem.SubjectName,
                SubjectCode = classItem.SubjectCode,
                ClassCode = classItem.ClassCode,
                InstructorName = displayName,
                InstructorInitials = displayInitials,
                TeacherRole = teacherRole,
                TeacherDepartment = teacherDepartment,
                RoomName = roomName,
                FloorDisplay = floorDisplay,
                RecentMaterials = recent,
                AssessmentTitle = content.Title,
                AssessmentDescription = content.Description,
                Attachments = content.Attachments ?? new List<string>(),
                PostedDate = content.CreatedAt.ToLocalTime().ToString("MMM d, yyyy"),
                Deadline = content.Deadline.HasValue ? content.Deadline.Value.ToLocalTime().ToString("MMM d, yyyy") : "N/A",
                EditedDate = content.UpdatedAt > content.CreatedAt ? content.UpdatedAt.ToLocalTime().ToString("MMM d, yyyy") : "",
                Submissions = new List<StudentSubmission>(),
                LinkUrl = content.LinkUrl ?? string.Empty
            };

            var logs = await _mongoDb.GetAntiCheatLogsAsync(classItem.Id, content.Id);
            int copy = 0, paste = 0, inspect = 0, tabSwitch = 0, openPrograms = 0, screenShare = 0;
            foreach (var l in logs)
            {
                var count = l.EventCount > 0 ? l.EventCount : 1;
                var type = (l.EventType ?? string.Empty).ToLower();
                switch (type)
                {
                    case "mouse_activity":
                        // ignore from Inspect totals; tracked separately via raw logs
                        break;
                    case "copy_paste":
                        try
                        {
                            using var doc = JsonDocument.Parse(l.Details ?? "{}");
                            var root = doc.RootElement;
                            var action = root.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";
                            if (string.Equals(action, "paste", System.StringComparison.OrdinalIgnoreCase)) paste += count; else copy += count;
                        }
                        catch { copy += count; }
                        break;
                    case "inspect":
                        inspect += count; break;
                    case "tab_switch":
                        tabSwitch += count; break;
                    case "tabswitching":
                        tabSwitch += count; break;
                    case "open_programs":
                        openPrograms += count; break;
                    case "window focus":
                        openPrograms += count; break;
                    case "screen_share":
                        screenShare += count; break;
                    default:
                        try
                        {
                            using var doc = JsonDocument.Parse(l.Details ?? "{}");
                            var root = doc.RootElement;
                            if (type == "copy" || (root.TryGetProperty("action", out var act) && string.Equals(act.GetString(), "copy", System.StringComparison.OrdinalIgnoreCase)))
                                copy += count;
                            else if (type == "paste" || (root.TryGetProperty("action", out var act2) && string.Equals(act2.GetString(), "paste", System.StringComparison.OrdinalIgnoreCase)))
                                paste += count;
                        }
                        catch { }
                        break;
                }
            }

            vm.LogCopy = copy;
            vm.LogPaste = paste;
            vm.LogInspect = inspect;
            vm.LogTabSwitch = tabSwitch;
            vm.LogOpenPrograms = openPrograms;
            vm.LogScreenShare = screenShare;

            return View("~/Views/AdminDb/AdminAssessment/Index.cshtml", vm);
        }

        [HttpPost("/AdminAssessment/{classCode}/{contentId}/set-score/{studentId}")]
        public async Task<IActionResult> SetScore(string classCode, string contentId, string studentId, [FromForm] double score, [FromForm] double maxScore)
        {
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            if (classItem == null || contentItem == null) return NotFound();
            await _mongoDb.UpdateAssessmentScoreAsync(classItem.Id, contentItem.Id, studentId, score, maxScore);
            return Ok(new { status = "score_set" });
        }

        [HttpPost("/AdminAssessment/UpdateAssessment")]
        public async Task<IActionResult> UpdateAssessment([FromBody] UpdateContentRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.AssessmentId)) return BadRequest(new { success = false, message = "Invalid request" });
            var content = await _mongoDb.GetContentByIdAsync(req.AssessmentId);
            if (content == null || content.Type != "assessment") return Json(new { success = false, message = "Assessment not found" });
            content.Title = req.Title ?? content.Title;
            content.Description = req.Description ?? content.Description;
            if (req.Link != null)
            {
                content.LinkUrl = req.Link;
            }
            if (!string.IsNullOrEmpty(req.Deadline))
            {
                if (DateTime.TryParse(req.Deadline, out var dl)) content.Deadline = dl;
            }
            content.UpdatedAt = DateTime.UtcNow;
            content.MetaText = GenerateUpdatedMetaText(content);
            await _mongoDb.UpdateContentAsync(content);
            return Json(new { success = true });
        }

        [HttpPost("/AdminAssessment/ReplaceAttachment")]
        public async Task<IActionResult> ReplaceAttachment([FromBody] ReplaceAssessmentAttachmentRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.AssessmentId) || string.IsNullOrEmpty(request.FileName))
                return BadRequest(new { success = false, message = "Invalid request" });

            var content = await _mongoDb.GetContentByIdAsync(request.AssessmentId);
            if (content == null || content.Type != "assessment")
                return Json(new { success = false, message = "Assessment not found" });

            content.Attachments = new List<string> { request.FileName };
            content.UpdatedAt = DateTime.UtcNow;
            content.MetaText = GenerateUpdatedMetaText(content);
            await _mongoDb.UpdateContentAsync(content);

            var uploads = await _mongoDb.GetUploadsByClassIdAsync(content.ClassId);
            var recentUpload = uploads
                .Where(u => u.UploadedBy == (User?.Identity?.Name ?? "Admin") && u.FileName == request.FileName)
                .OrderByDescending(u => u.UploadedAt)
                .FirstOrDefault();
            if (recentUpload != null)
            {
                recentUpload.ContentId = content.Id ?? string.Empty;
                await _mongoDb.UpdateUploadAsync(recentUpload);
            }

            return Json(new { success = true, message = "Attachment replaced" });
        }

        public class ReplaceAssessmentAttachmentRequest
        {
            public string AssessmentId { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public string? FileUrl { get; set; }
        }

        [HttpPost("/AdminAssessment/DeleteAssessment")]
        public async Task<IActionResult> DeleteAssessment([FromBody] DeleteContentRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.AssessmentId)) return BadRequest(new { success = false, message = "Invalid request" });
            await _mongoDb.DeleteContentAsync(req.AssessmentId);
            await _mongoDb.DeleteUploadsByContentIdAsync(req.AssessmentId);
            return Json(new { success = true });
        }

        private string GenerateUpdatedMetaText(StudentPortal.Models.AdminMaterial.ContentItem content)
        {
            var meta = $"Posted: {content.CreatedAt:MMM dd, yyyy}";
            if (content.Deadline.HasValue)
            {
                meta += $" | Deadline: {content.Deadline.Value:MMM dd, yyyy}";
            }
            if (content.UpdatedAt > content.CreatedAt)
            {
                meta += $" | Edited: {content.UpdatedAt:MMM dd, yyyy}";
            }
            var filesCount = content.Attachments?.Count ?? 0;
            if (filesCount > 0)
            {
                meta += $" | Files: {filesCount}";
            }
            return meta;
        }

        [HttpGet("/AdminAssessment/DownloadFile/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName, string assessmentId)
        {
            try
            {
                var uploadItem = await _mongoDb.GetUploadByFileNameAsync(fileName, assessmentId);
                if (uploadItem == null)
                    return NotFound("File not found.");

                var url = uploadItem.FileUrl ?? string.Empty;
                var fileWithGuid = System.IO.Path.GetFileName(url);
                var uploadsDir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "uploads");
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

        [HttpGet("/AdminAssessment/ViewFile/{fileName}")]
        public async Task<IActionResult> ViewFile(string fileName, string contentId)
        {
            try
            {
                var uploadItem = await _mongoDb.GetUploadByFileNameAsync(fileName, contentId);
                if (uploadItem == null)
                    return NotFound("File not found.");

                var url = uploadItem.FileUrl ?? string.Empty;
                var fileWithGuid = System.IO.Path.GetFileName(url);
                var uploadsDir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "uploads");
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

        [HttpGet("/AdminAssessment/{classCode}/{contentId}/Logs")]
        public async Task<IActionResult> GetLogs(string classCode, string contentId, [FromQuery] string? type)
        {
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            if (classItem == null || contentItem == null) return NotFound();

            var requested = (type ?? string.Empty).ToLowerInvariant();
            var logs = await _mongoDb.GetAntiCheatLogsAsync(classItem.Id, contentItem.Id);

            bool MatchesType(StudentPortal.Models.AdminDb.AntiCheatLog l)
            {
                var t = (l.EventType ?? string.Empty).ToLowerInvariant();
                if (string.IsNullOrEmpty(requested)) return true;
                switch (requested)
                {
                    case "copy":
                        if (t == "copy") return true;
                        if (t == "copy_paste")
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(l.Details ?? "{}");
                                var root = doc.RootElement;
                                var action = root.TryGetProperty("action", out var a) ? a.GetString() ?? string.Empty : string.Empty;
                                return string.Equals(action, "copy", System.StringComparison.OrdinalIgnoreCase);
                            }
                            catch { return false; }
                        }
                        return false;
                    case "paste":
                        if (t == "paste") return true;
                        if (t == "copy_paste")
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(l.Details ?? "{}");
                                var root = doc.RootElement;
                                var action = root.TryGetProperty("action", out var a) ? a.GetString() ?? string.Empty : string.Empty;
                                return string.Equals(action, "paste", System.StringComparison.OrdinalIgnoreCase);
                            }
                            catch { return false; }
                        }
                        return false;
                    case "inspect":
                        return t == "inspect";
                    case "tabswitch":
                        return t == "tab_switch" || t == "tabswitching";
                    case "openprograms":
                        return t == "open_programs" || t == "window focus";
                    case "screenshare":
                        return t == "screen_share";
                    default:
                        return false;
                }
            }

            string NormalizeType(StudentPortal.Models.AdminDb.AntiCheatLog l)
            {
                var t = (l.EventType ?? string.Empty).ToLowerInvariant();
                if (t == "copy_paste")
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(l.Details ?? "{}");
                        var root = doc.RootElement;
                        var action = root.TryGetProperty("action", out var a) ? a.GetString() ?? string.Empty : string.Empty;
                        if (string.Equals(action, "paste", System.StringComparison.OrdinalIgnoreCase)) return "paste";
                        return "copy";
                    }
                    catch { return "copy"; }
                }
                if (t == "tab_switch" || t == "tabswitching") return "tabswitch";
                if (t == "open_programs" || t == "window focus") return "openprograms";
                if (t == "screen_share") return "screenshare";
                if (t == "mouse_activity") return "mouseactivity";
                return t;
            }

            var result = logs
                .Where(MatchesType)
                .Select(l => new
                {
                    student = l.StudentName,
                    email = l.StudentEmail,
                    type = NormalizeType(l),
                    count = l.EventCount > 0 ? l.EventCount : 1,
                    duration = l.EventDuration,
                    severity = l.Severity,
                    flagged = l.Flagged,
                    time = l.LogTimeUtc,
                    details = l.Details
                })
                .OrderByDescending(x => x.time)
                .Take(500)
                .ToList();

            return Json(new { success = true, logs = result });
        }
    }
}
