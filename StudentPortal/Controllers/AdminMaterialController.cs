using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.AdminClass;
using StudentPortal.Models.AdminDb;
using StudentPortal.Models.AdminMaterial;
using StudentPortal.Models.Library;
using StudentPortal.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers
{
    public class AdminMaterialController : Controller
    {
        private readonly MongoDbService _mongoDb;
        private readonly IWebHostEnvironment _env;
        private readonly LibraryService _libraryService;

        public AdminMaterialController(MongoDbService mongoDb, IWebHostEnvironment env, LibraryService libraryService)
        {
            _mongoDb = mongoDb;
            _env = env;
            _libraryService = libraryService;
        }

        [HttpGet("/AdminMaterial/{classCode}/{contentId}")]
        public async Task<IActionResult> Index(string classCode, string contentId)
        {
            if (string.IsNullOrEmpty(classCode) || string.IsNullOrEmpty(contentId))
                return NotFound("Class code or Content ID not provided.");

            // Get content item from database
            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            if (contentItem == null || contentItem.Type != "material")
                return NotFound("Material not found.");

            // Get class information using class code
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null)
                return NotFound("Class not found.");

            // Verify that the content belongs to this class
            if (contentItem.ClassId != classItem.Id)
                return NotFound("Material not found in this class.");

            var materialList = await _mongoDb.GetMaterialsByClassIdAsync(classItem.Id);
            var recentMaterials = materialList
                .Take(3)
                .Select(m => new AdminClassRecentUpload
                {
                    ContentId = m.Id ?? "",
                    Title = string.IsNullOrWhiteSpace(m.Title) ? "(Untitled)" : m.Title,
                    IconClass = "fa-solid fa-book-open-reader",
                    TargetUrl = Url.Action("Index", "AdminMaterial", new { classCode = classItem.ClassCode, contentId = m.Id }) ?? "#"
                })
                .ToList();

            // Get uploaded files for this material from Uploads collection - USING ContentId now
            var uploadedFiles = await _mongoDb.GetUploadsByContentIdAsync(contentId);

            await TryAutoLinkLibraryEbooksAsync(contentItem, classItem);

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

            List<MaterialLinkedEbookDisplay> linkedEbooks = new();
            try
            {
                linkedEbooks = await _libraryService.GetLinkedEbookDisplaysAsync(contentItem.LinkedLibraryEbookIds ?? new List<string>());
            }
            catch { /* library DB optional */ }

            var vm = new AdminMaterialViewModel
            {
                MaterialId = contentItem.Id,
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
                MaterialName = contentItem.Title,
                MaterialDescription = contentItem.Description,
                Attachments = uploadedFiles.Select(u => u.FileName).ToList(), // Use actual uploaded files
                LinkedEbooks = linkedEbooks,
                PostedDate = contentItem.CreatedAt.ToLocalTime().ToString("MMM d, yyyy"),
                EditedDate = contentItem.UpdatedAt > contentItem.CreatedAt ? contentItem.UpdatedAt.ToLocalTime().ToString("MMM d, yyyy") : "",
                RecentMaterials = recentMaterials
            };

            return View("~/Views/AdminDb/AdminMaterial/Index.cshtml", vm);
        }

        [HttpPost("/AdminMaterial/ReplaceAttachment")]
        public async Task<IActionResult> ReplaceAttachment([FromBody] ReplaceAttachmentRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.MaterialId) || string.IsNullOrEmpty(request.FileName))
                    return BadRequest(new { success = false, message = "Invalid request" });

                var contentItem = await _mongoDb.GetContentByIdAsync(request.MaterialId);
                if (contentItem == null || contentItem.Type != "material")
                    return NotFound(new { success = false, message = "Material not found" });

                // Replace attachments with the new file name
                contentItem.Attachments = new System.Collections.Generic.List<string> { request.FileName };
                contentItem.UpdatedAt = DateTime.UtcNow;
                contentItem.MetaText = GenerateUpdatedMetaText(contentItem);
                await _mongoDb.UpdateContentAsync(contentItem);

                // Link the most recent upload record to this content
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

        public class ReplaceAttachmentRequest
        {
            public string MaterialId { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public string? FileUrl { get; set; }
        }

        [HttpPost("/AdminMaterial/UpdateMaterial")]
        public async Task<IActionResult> UpdateMaterial([FromBody] UpdateMaterialRequest request)
        {
            try
            {
                var contentItem = await _mongoDb.GetContentByIdAsync(request.MaterialId);
                if (contentItem == null || contentItem.Type != "material")
                    return NotFound(new { success = false, message = "Material not found" });

                // Update material properties
                contentItem.Title = request.Title;
                contentItem.Description = request.Description;
                contentItem.UpdatedAt = DateTime.UtcNow;
                contentItem.MetaText = GenerateUpdatedMetaText(contentItem);

                await _mongoDb.UpdateContentAsync(contentItem);

                return Ok(new { success = true, message = "Material updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("/AdminMaterial/DeleteMaterial")]
        public async Task<IActionResult> DeleteMaterial([FromBody] DeleteMaterialRequest request)
        {
            try
            {
                await _mongoDb.DeleteContentAsync(request.MaterialId);
                await _mongoDb.DeleteUploadsByContentIdAsync(request.MaterialId);
                return Ok(new { success = true, message = "Material deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("/AdminMaterial/SetLinkedEbook")]
        public async Task<IActionResult> SetLinkedEbook([FromBody] SetLinkedEbookRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.MaterialId) || string.IsNullOrWhiteSpace(request.BookId))
                    return BadRequest(new { success = false, message = "Missing materialId or bookId." });

                var contentItem = await _mongoDb.GetContentByIdAsync(request.MaterialId);
                if (contentItem == null || contentItem.Type != "material")
                    return NotFound(new { success = false, message = "Material not found." });

                var book = await _libraryService.GetBookByIdAsync(request.BookId);
                if (book == null)
                    return BadRequest(new { success = false, message = "Library item not found." });
                if (!book.EffectiveIsEBook)
                    return BadRequest(new { success = false, message = "Only eBooks can be attached." });

                // Single eBook per material for now (replace behavior).
                contentItem.LinkedLibraryEbookIds = new System.Collections.Generic.List<string> { request.BookId.Trim() };
                contentItem.UpdatedAt = DateTime.UtcNow;
                contentItem.MetaText = GenerateUpdatedMetaText(contentItem);
                await _mongoDb.UpdateContentAsync(contentItem);

                var linked = await _libraryService.GetLinkedEbookDisplaysAsync(contentItem.LinkedLibraryEbookIds);
                return Ok(new { success = true, linkedEbooks = linked });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        public class SetLinkedEbookRequest
        {
            public string MaterialId { get; set; } = string.Empty;
            public string BookId { get; set; } = string.Empty;
        }

        [HttpGet("/AdminMaterial/DownloadFile/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName, string contentId)
        {
            try
            {
                var uploadItem = await _mongoDb.GetUploadByFileNameAsync(fileName, contentId);
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

        [HttpGet("/AdminMaterial/ViewFile/{fileName}")]
        public async Task<IActionResult> ViewFile(string fileName, string contentId)
        {
            try
            {
                var uploadItem = await _mongoDb.GetUploadByFileNameAsync(fileName, contentId);
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

        private static string GetInitials(string fullName)
        {
            var parts = fullName.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Select(p => p[0])).ToUpper();
        }

        /// <summary>
        /// When no eBooks are linked yet, discover SLSHS Library eBooks (is_ebook) from the shared catalog and persist ids on the material.
        /// </summary>
        private async Task TryAutoLinkLibraryEbooksAsync(ContentItem contentItem, ClassItem classItem)
        {
            try
            {
                if (contentItem.LinkedLibraryEbookIds != null && contentItem.LinkedLibraryEbookIds.Count > 0)
                    return;

                var discovered = await _libraryService.DiscoverEbookIdsForMaterialAsync(
                    classItem.SubjectName,
                    classItem.SubjectCode,
                    contentItem.Title);
                if (discovered == null || discovered.Count == 0)
                    return;

                contentItem.LinkedLibraryEbookIds = discovered.ToList();
                contentItem.UpdatedAt = DateTime.UtcNow;
                contentItem.MetaText = GenerateUpdatedMetaText(contentItem);
                await _mongoDb.UpdateContentAsync(contentItem);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdminMaterial] TryAutoLinkLibraryEbooksAsync: {ex.Message}");
            }
        }

        // Request models
        public class UpdateMaterialRequest
        {
            public string MaterialId { get; set; } = "";
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
        }

        public class DeleteMaterialRequest
        {
            public string MaterialId { get; set; } = "";
        }

        private string GenerateUpdatedMetaText(StudentPortal.Models.AdminMaterial.ContentItem content)
        {
            var meta = $"Posted: {content.CreatedAt.ToLocalTime():MMM dd, yyyy}";
            if (content.Deadline.HasValue)
            {
                meta += $" | Deadline: {content.Deadline.Value.ToLocalTime():MMM dd, yyyy}";
            }
            if (content.UpdatedAt > content.CreatedAt)
            {
                meta += $" | Edited: {content.UpdatedAt.ToLocalTime():MMM dd, yyyy}";
            }
            var filesCount = content.Attachments?.Count ?? 0;
            if (filesCount > 0)
            {
                meta += $" | Files: {filesCount}";
            }
            return meta;
        }
    }
}
