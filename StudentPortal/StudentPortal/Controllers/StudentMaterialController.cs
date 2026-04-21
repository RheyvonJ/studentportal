using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using StudentPortal.Models.StudentDb;
using StudentPortal.Services;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers
{
    public class StudentMaterialController : Controller
    {
        private readonly MongoDbService _mongoDb;
        private readonly IWebHostEnvironment _env;
        private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

        public StudentMaterialController(MongoDbService mongoDb, IWebHostEnvironment env)
        {
            _mongoDb = mongoDb;
            _env = env;
        }

        private static string GetContentType(string filePathOrName)
        {
            if (string.IsNullOrWhiteSpace(filePathOrName)) return "application/octet-stream";
            return _contentTypeProvider.TryGetContentType(filePathOrName, out var ct)
                ? ct
                : "application/octet-stream";
        }

        private string NormalizeRedirectUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;

            // If uploads are stored as absolute URLs to localhost (common in dev),
            // rewrite to same-origin so it works in any environment.
            if (System.Uri.TryCreate(url, System.UriKind.Absolute, out var abs))
            {
                var path = abs.PathAndQuery;
                if (!string.IsNullOrWhiteSpace(path) && path.StartsWith("/uploads/", System.StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
                return url;
            }

            // Relative URLs are already same-origin.
            return url;
        }

        [HttpGet("/StudentMaterial/{classCode}/{contentId}")]
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
            if (contentItem == null || contentItem.Type != "material")
                return NotFound("Material not found.");

            if (contentItem.ClassId != classItem.Id)
                return NotFound("Material not found in this class.");

            var files = await _mongoDb.GetUploadsByContentIdAsync(contentId);
            var materialList = await _mongoDb.GetMaterialsByClassIdAsync(classItem.Id);
            // Include current material first so sidebar can mark is-current; keep up to 3 rows total
            var others = materialList.Where(m => m.Id != contentId).Take(2).ToList();
            var recentRows = new List<StudentRecentUploadItem>();
            var currentMat = materialList.FirstOrDefault(m => m.Id == contentId);
            if (currentMat != null)
            {
                recentRows.Add(new StudentRecentUploadItem
                {
                    Title = currentMat.Title ?? string.Empty,
                    TargetUrl = $"/StudentMaterial/{classItem.ClassCode}/{currentMat.Id}",
                    IconClass = "fa-solid fa-book-open-reader",
                    ContentId = currentMat.Id ?? string.Empty
                });
            }
            foreach (var m in others)
            {
                recentRows.Add(new StudentRecentUploadItem
                {
                    Title = m.Title ?? string.Empty,
                    TargetUrl = $"/StudentMaterial/{classItem.ClassCode}/{m.Id}",
                    IconClass = "fa-solid fa-book-open-reader",
                    ContentId = m.Id ?? string.Empty
                });
            }
            var instructorName = !string.IsNullOrWhiteSpace(classItem.InstructorName)
                ? classItem.InstructorName
                : (!string.IsNullOrWhiteSpace(classItem.CreatorName)
                    ? classItem.CreatorName
                    : (await _mongoDb.GetProfessorByEmailAsync(classItem.OwnerEmail))?.GetFullName() ?? "Instructor");
            var initials = !string.IsNullOrWhiteSpace(classItem.CreatorInitials)
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

            var vm = new StudentMaterialViewModel
            {
                MaterialId = contentItem.Id ?? contentId,
                SubjectName = classItem.SubjectName ?? string.Empty,
                SectionName = !string.IsNullOrWhiteSpace(classItem.SectionLabel) ? classItem.SectionLabel : (classItem.Section ?? string.Empty),
                SubjectCode = classItem.SubjectCode ?? string.Empty,
                ClassCode = classItem.ClassCode ?? string.Empty,
                InstructorName = instructorName,
                InstructorInitials = string.IsNullOrWhiteSpace(initials) ? "IN" : initials,
                InstructorRole = roleLabel,
                TeacherDepartment = teacherDepartment,
                RoomName = roomName,
                FloorDisplay = floorDisplay,

                MaterialTitle = contentItem.Title ?? string.Empty,
                Description = contentItem.Description ?? string.Empty,
                UploadedBy = contentItem.UploadedBy ?? string.Empty,
                UploadDate = contentItem.CreatedAt,
                RecentMaterialRows = recentRows,
                Files = files.Select(f => new MaterialFile { FileName = f.FileName, FileUrl = f.FileUrl }).ToList()
            };

            return View("~/Views/StudentDb/StudentMaterial/Index.cshtml", vm);
        }

        [HttpGet("/StudentMaterial/ViewFile/{fileName}")]
        public async Task<IActionResult> ViewFile(string fileName, [FromQuery] string contentId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(contentId))
                    return NotFound("File not found.");

                var uploadItem = await _mongoDb.GetUploadByFileNameAsync(fileName, contentId);
                if (uploadItem == null)
                    return NotFound("File not found.");

                var url = uploadItem.FileUrl ?? string.Empty;
                var fileWithGuid = WebUtility.UrlDecode(System.IO.Path.GetFileName(url));
                var uploadsDir = System.IO.Path.Combine(_env.WebRootPath, "uploads");
                var physPath = !string.IsNullOrWhiteSpace(fileWithGuid)
                    ? System.IO.Path.Combine(uploadsDir, fileWithGuid)
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(physPath) && System.IO.File.Exists(physPath))
                {
                    var contentType = GetContentType(fileWithGuid);
                    var res = PhysicalFile(physPath, contentType);
                    if (res is PhysicalFileResult p)
                    {
                        p.EnableRangeProcessing = true;
                    }
                    return res;
                }

                if (!string.IsNullOrWhiteSpace(url))
                {
                    return Redirect(NormalizeRedirectUrl(url));
                }

                return NotFound("File URL not available.");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("/StudentMaterial/DownloadFile/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName, [FromQuery] string contentId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(contentId))
                    return NotFound("File not found.");

                var uploadItem = await _mongoDb.GetUploadByFileNameAsync(fileName, contentId);
                if (uploadItem == null)
                    return NotFound("File not found.");

                var url = uploadItem.FileUrl ?? string.Empty;
                var fileWithGuid = WebUtility.UrlDecode(System.IO.Path.GetFileName(url));
                var uploadsDir = System.IO.Path.Combine(_env.WebRootPath, "uploads");
                var physPath = !string.IsNullOrWhiteSpace(fileWithGuid)
                    ? System.IO.Path.Combine(uploadsDir, fileWithGuid)
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(physPath) && System.IO.File.Exists(physPath))
                {
                    var contentType = GetContentType(fileWithGuid);
                    Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
                    var res = PhysicalFile(physPath, contentType, fileName);
                    if (res is PhysicalFileResult p)
                    {
                        p.EnableRangeProcessing = true;
                    }
                    return res;
                }

                if (!string.IsNullOrWhiteSpace(url))
                {
                    return Redirect(NormalizeRedirectUrl(url));
                }

                return NotFound("File URL not available.");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string FileName, string ContentId, System.DateTime ExpiresUtc)>
            _publicPreviewTokens = new();

        [HttpPost("/StudentMaterial/CreatePublicPreviewToken")]
        public IActionResult CreatePublicPreviewToken([FromForm] string fileName, [FromForm] string contentId)
        {
            try
            {
                var email = HttpContext.Session.GetString("UserEmail");
                if (string.IsNullOrEmpty(email)) return Unauthorized();
                if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(contentId)) return BadRequest();

                // short-lived token so Google viewer can fetch the file
                var token = System.Guid.NewGuid().ToString("N");
                _publicPreviewTokens[token] = (fileName, contentId, System.DateTime.UtcNow.AddMinutes(3));
                return Json(new { success = true, token });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("/PublicPreview/{token}")]
        public async Task<IActionResult> PublicPreview(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token)) return NotFound();
                if (!_publicPreviewTokens.TryGetValue(token, out var t)) return NotFound();
                if (t.ExpiresUtc <= System.DateTime.UtcNow)
                {
                    _publicPreviewTokens.TryRemove(token, out _);
                    return NotFound();
                }

                var uploadItem = await _mongoDb.GetUploadByFileNameAsync(t.FileName, t.ContentId);
                if (uploadItem == null) return NotFound();
                var url = uploadItem.FileUrl ?? string.Empty;
                var fileWithGuid = WebUtility.UrlDecode(System.IO.Path.GetFileName(url));
                var uploadsDir = System.IO.Path.Combine(_env.WebRootPath, "uploads");
                var physPath = !string.IsNullOrWhiteSpace(fileWithGuid) ? System.IO.Path.Combine(uploadsDir, fileWithGuid) : string.Empty;
                if (string.IsNullOrWhiteSpace(physPath) || !System.IO.File.Exists(physPath)) return NotFound();

                var contentType = GetContentType(fileWithGuid);

                var res = PhysicalFile(physPath, contentType);
                if (res is PhysicalFileResult p) p.EnableRangeProcessing = true;
                return res;
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("/StudentMaterial/GetComments")]
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

        [HttpPost("/StudentMaterial/PostComment")]
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
            return Json(new { success = true, comment = new { id = item.Id, authorName = item.AuthorName, role = NormalizeCommentRole(item.Role), text = item.Text, createdAt = item.CreatedAt, replies = item.Replies.Select(r => new { authorName = r.AuthorName, role = NormalizeCommentRole(r.Role), text = r.Text, createdAt = r.CreatedAt }).ToList() } });
        }

        [HttpPost("/StudentMaterial/PostReply")]
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
            return "IN";
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
