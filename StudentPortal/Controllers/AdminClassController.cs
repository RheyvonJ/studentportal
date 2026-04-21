using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.AdminClass;
using StudentPortal.Models.AdminDb;
using StudentPortal.Models.AdminMaterial;
using MongoDB.Bson;
using StudentPortal.Models.AdminTask;
using StudentPortal.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace StudentPortal.Controllers
{
    public class AdminClassController : Controller
    {
        private readonly MongoDbService _mongoDb;
        private readonly IWebHostEnvironment _env;
        private readonly EmailService _email;
        private readonly LibraryService _libraryService;
        private readonly IConfiguration _configuration;

        public AdminClassController(MongoDbService mongoDb, IWebHostEnvironment env, EmailService email, LibraryService libraryService, IConfiguration configuration)
        {
            _mongoDb = mongoDb;
            _env = env;
            _email = email;
            _libraryService = libraryService;
            _configuration = configuration;
        }

        private string? ResolveSchoolLogoAbsoluteUrl()
        {
            // Prefer inline-embedded logo for email clients (cid:school-logo).
            if (string.Equals(_configuration["Portal:EmailEmbedLogo"], "true", StringComparison.OrdinalIgnoreCase))
                return "cid:school-logo";

            var path = _configuration["Portal:SchoolLogo"] ?? "~/images/SLSHS.png";
            var publicBase = (_configuration["Portal:PublicSiteUrl"] ?? string.Empty).Trim().TrimEnd('/');
            var localPath = path.StartsWith("~/", StringComparison.Ordinal) ? path[1..]
                : (path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path);
            if (!string.IsNullOrWhiteSpace(publicBase))
                return publicBase + localPath;
            if (Request?.Host.HasValue == true)
                return $"{Request.Scheme}://{Request.Host}{Url.Content(path)}";
            return null;
        }

        [HttpGet("/AdminClass/{id}")]
        public async Task<IActionResult> Index(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound("Class code not provided.");

            var classItem = await _mongoDb.GetClassByCodeAsync(id);
            if (classItem == null)
                return NotFound("Class not found.");

            var contents = await _mongoDb.GetContentsForClassAsync(classItem.Id, classItem.ClassCode);

            var professorName = HttpContext.Session.GetString("UserName") ?? "Professor";
            var professorEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
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

                // Override using StudentDB Teachers collection if present
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
            var professorInitials = GetInitials(professorName);

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

            // Recent uploads: material / task / assessment content only (not raw files); max 3
            var recentUploads = contents
                .Where(c => c.Type == "material" || c.Type == "task" || c.Type == "assessment")
                .Select(c => new
                {
                    ContentId = c.Id,
                    IconClass = GetIconByType(c.Type),
                    Title = string.IsNullOrWhiteSpace(c.Title) ? "(Untitled)" : c.Title,
                    SortAt = c.UpdatedAt > c.CreatedAt ? c.UpdatedAt : c.CreatedAt,
                    TargetUrl = BuildRecentUploadUrlForContent(c, classItem.ClassCode)
                })
                .OrderByDescending(x => x.SortAt)
                .GroupBy(x => x.ContentId)
                .Select(g => g.First())
                .Take(3)
                .Select(x => new AdminClassRecentUpload
                {
                    IconClass = x.IconClass,
                    Title = x.Title,
                    TargetUrl = x.TargetUrl,
                    ContentId = x.ContentId ?? ""
                })
                .ToList();

            var vm = new AdminClassViewModel
            {
                ClassId = classItem.Id,
                SubjectName = classItem.SubjectName,
                SectionName = !string.IsNullOrWhiteSpace(classItem.SectionLabel) ? classItem.SectionLabel : (classItem.Section ?? string.Empty),
                SubjectCode = classItem.SubjectCode,
                ClassCode = classItem.ClassCode,
                AdminName = professorName,
                AdminInitials = professorInitials,
                TeacherRole = teacherRole,
                TeacherDepartment = teacherDepartment,
                RoomName = roomName,
                FloorDisplay = floorDisplay,
                RecentUploads = recentUploads,
                Contents = contents.Select(c => new AdminClassContent
                {
                    ContentId = c.Id,
                    Type = c.Type,
                    Title = c.Type == "announcement"
                        ? (string.IsNullOrWhiteSpace(c.Description) ? "(No announcement text)" : c.Description)
                        : c.Title,
                    IconClass = GetIconByType(c.Type),
                    MetaText = c.Type == "announcement"
                        ? $"Posted: {c.CreatedAt.ToLocalTime():MMM d, yyyy h:mm tt}"
                        : BuildMetaForCard(c),
                    TargetUrl = c.Type == "announcement"
                        ? null
                        : c.Type == "meeting" && !string.IsNullOrWhiteSpace(c.LinkUrl)
                            ? c.LinkUrl
                            : Url.Action("Index", $"Admin{Capitalize(c.Type)}", new { classCode = classItem.ClassCode, contentId = c.Id }),
                    HasUrgency = c.HasUrgency,
                    UrgencyColor = c.UrgencyColor
                }).ToList()
            };

            return View("~/Views/AdminDb/AdminClass/Index.cshtml", vm);
        }

        [HttpPost("/AdminClass/AddAnnouncement")]
        public async Task<IActionResult> AddAnnouncement([FromBody] AnnouncementRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest("Announcement text cannot be empty.");

            var classItem = await _mongoDb.GetClassByCodeAsync(request.ClassId);
            if (classItem == null)
                return NotFound("Class not found.");

            var announcement = new ContentItem
            {
                ClassId = classItem.Id,
                Type = "announcement",
                Title = "Announcement",
                Description = request.Text,
                UploadedBy = User?.Identity?.Name ?? "Admin",
                CreatedAt = DateTime.UtcNow,
                HasUrgency = false
            };

            await _mongoDb.InsertContentAsync(announcement);

            _ = Task.Run(async () =>
            {
                try
                {
                    var teacherEmail = HttpContext?.Session?.GetString("UserEmail") ?? string.Empty;
                    var recipients = await _mongoDb.GetStudentEmailsForClassAsync(classItem);
                    if (!string.IsNullOrWhiteSpace(teacherEmail))
                        recipients = recipients.Where(e => !string.Equals(e, teacherEmail, StringComparison.OrdinalIgnoreCase)).ToList();

                    var schoolName = _configuration["Portal:SchoolName"] ?? "Sta. Lucia Senior High School";
                    var logoUrl = ResolveSchoolLogoAbsoluteUrl();
                    var subject = $"{JoinClassEmailTemplate.NewAnnouncementDefaultSubjectPrefix}: {classItem.SubjectName}";

                    foreach (var r in recipients)
                    {
                        var bodyHtml = JoinClassEmailTemplate.BuildNewAnnouncementHtml(
                            studentDisplayName: "Student",
                            courseName: classItem.SubjectName,
                            classCode: classItem.ClassCode,
                            announcementText: request.Text,
                            schoolName: schoolName,
                            logoAbsoluteUrl: logoUrl);
                        await _email.SendEmailAsync(r, subject, bodyHtml, isHtml: true);
                    }
                }
                catch { }
            });

            return Ok(new
            {
                contentId = announcement.Id,
                announcement.Description,
                announcement.UploadedBy,
                announcement.CreatedAt
            });
        }

        [HttpPost("/AdminClass/DeleteAnnouncement")]
        public async Task<IActionResult> DeleteAnnouncement([FromBody] DeleteAnnouncementRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ContentId))
                    return BadRequest(new { success = false, message = "Content id is required." });

                var content = await _mongoDb.GetContentByIdAsync(request.ContentId);
                if (content == null || !string.Equals(content.Type, "announcement", StringComparison.OrdinalIgnoreCase))
                    return NotFound(new { success = false, message = "Announcement not found." });

                await _mongoDb.DeleteUploadsByContentIdAsync(request.ContentId);
                await _mongoDb.DeleteContentAsync(request.ContentId);

                return Ok(new { success = true, message = "Announcement deleted." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("/AdminClass/CreateContent")]
        public async Task<IActionResult> CreateContent([FromBody] CreateContentRequest request)
        {
            try
            {
                var classItem = await _mongoDb.GetClassByCodeAsync(request.ClassId);
                if (classItem == null)
                    return NotFound(new { success = false, message = "Class not found" });

                if (string.Equals(request.Type, "assessment", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(request.Link))
                        return BadRequest(new { success = false, message = "Assessment link is required." });
                    if (!Uri.TryCreate(request.Link.Trim(), UriKind.Absolute, out var assessmentUri)
                        || (assessmentUri.Scheme != Uri.UriSchemeHttp && assessmentUri.Scheme != Uri.UriSchemeHttps))
                    {
                        return BadRequest(new { success = false, message = "Assessment link must be a valid http or https URL." });
                    }
                }

                // Create content item
                var contentItem = new ContentItem
                {
                    ClassId = classItem.Id,
                    Title = request.Title,
                    Type = request.Type,
                    Description = request.Description,
                    MetaText = GenerateMetaText(request),
                    IconClass = GetIconClass(request.Type),
                    HasUrgency = !string.IsNullOrEmpty(request.Deadline),
                    UrgencyColor = GetUrgencyColor(request.Deadline),
                    CreatedAt = DateTime.UtcNow,
                    UploadedBy = User.Identity?.Name ?? "Admin",
                    Deadline = string.IsNullOrEmpty(request.Deadline) ? null : DateTime.Parse(request.Deadline),
                    LinkUrl = request.Link ?? string.Empty,
                    MaxGrade = request.MaxGrade
                };

                // Link SLSHS Library eBook (eBooks only) when provided (materials only).
                if (string.Equals(request.Type, "material", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(request.EbookId))
                {
                    var book = await _libraryService.GetBookByIdAsync(request.EbookId.Trim());
                    if (book == null)
                        return BadRequest(new { success = false, message = "Selected eBook was not found in the library catalog." });
                    if (!book.EffectiveIsEBook)
                        return BadRequest(new { success = false, message = "Only eBooks can be attached to learning materials." });
                    contentItem.LinkedLibraryEbookIds = new System.Collections.Generic.List<string> { request.EbookId.Trim() };
                }

                // Ensure content has an Id before linking uploads to it
                if (string.IsNullOrEmpty(contentItem.Id))
                {
                    contentItem.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
                }

                if (request.FileNames != null && request.FileNames.Count > 0)
                {
                    var uploads = await _mongoDb.GetUploadsByClassIdAsync(classItem.Id);
                    foreach (var fname in request.FileNames)
                    {
                        var recentUpload = uploads
                            .Where(u => u.FileName == fname && string.IsNullOrEmpty(u.ContentId))
                            .OrderByDescending(u => u.UploadedAt)
                            .FirstOrDefault();
                        if (recentUpload != null)
                        {
                            recentUpload.ContentId = contentItem.Id;
                            await _mongoDb.UpdateUploadAsync(recentUpload);
                            contentItem.Attachments.Add(fname);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(request.FileName))
                {
                    var uploads = await _mongoDb.GetUploadsByClassIdAsync(classItem.Id);
                    var recentUpload = uploads
                        .Where(u => u.FileName == request.FileName && string.IsNullOrEmpty(u.ContentId))
                        .OrderByDescending(u => u.UploadedAt)
                        .FirstOrDefault();
                    if (recentUpload != null)
                    {
                        recentUpload.ContentId = contentItem.Id;
                        await _mongoDb.UpdateUploadAsync(recentUpload);
                    }
                    contentItem.Attachments.Add(request.FileName);
                }

                await _mongoDb.InsertContentAsync(contentItem);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var teacherEmail = HttpContext?.Session?.GetString("UserEmail") ?? string.Empty;
                        var recipients = await _mongoDb.GetStudentEmailsForClassAsync(classItem);
                        if (!string.IsNullOrWhiteSpace(teacherEmail))
                            recipients = recipients.Where(e => !string.Equals(e, teacherEmail, StringComparison.OrdinalIgnoreCase)).ToList();

                        var typeLabel = string.IsNullOrWhiteSpace(request.Type) ? "content" : request.Type.Trim();
                        var title = string.IsNullOrWhiteSpace(request.Title) ? "(Untitled)" : request.Title.Trim();

                        var schoolName = _configuration["Portal:SchoolName"] ?? "Sta. Lucia Senior High School";
                        var logoUrl = ResolveSchoolLogoAbsoluteUrl();
                        var subject = $"{JoinClassEmailTemplate.NewUploadDefaultSubjectPrefix}: {classItem.SubjectName}";

                        foreach (var r in recipients)
                        {
                            var bodyHtml = JoinClassEmailTemplate.BuildNewUploadHtml(
                                studentDisplayName: "Student",
                                courseName: classItem.SubjectName,
                                classCode: classItem.ClassCode,
                                contentTypeLabel: typeLabel,
                                contentTitle: title,
                                schoolName: schoolName,
                                logoAbsoluteUrl: logoUrl);
                            await _email.SendEmailAsync(r, subject, bodyHtml, isHtml: true);
                        }
                    }
                    catch { }
                });

                return Ok(new
                {
                    success = true,
                    message = $"{request.Type} created successfully",
                    contentId = contentItem.Id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Helper methods
        private static string GetIconByType(string type)
        {
            return type switch
            {
                "material" => "fa-solid fa-book-open-reader",
                "task" => "fa-solid fa-file-pen",
                "assessment" => "fa-solid fa-circle-question",
                "announcement" => "fa-solid fa-bullhorn",
                "meeting" => "fa-solid fa-chalkboard-user",
                _ => "fa-solid fa-file"
            };
        }

        private static string Capitalize(string str) =>
            string.IsNullOrEmpty(str) ? str : char.ToUpper(str[0]) + str.Substring(1);

        /// <summary>Same destination as main content cards (task / material / assessment / meeting).</summary>
        private string BuildRecentUploadUrlForContent(ContentItem c, string classCode)
        {
            if (c.Type == "meeting" && !string.IsNullOrWhiteSpace(c.LinkUrl))
                return c.LinkUrl;
            if (string.Equals(c.Type, "announcement", StringComparison.OrdinalIgnoreCase))
            {
                var ann = Url.Action("Index", "StudentAnnouncement", new { classCode, contentId = c.Id });
                return string.IsNullOrEmpty(ann) ? "#" : ann;
            }
            var action = Url.Action("Index", $"Admin{Capitalize(c.Type)}", new { classCode, contentId = c.Id });
            return string.IsNullOrEmpty(action) ? "#" : action;
        }

        private static string GetInitials(string fullName)
        {
            var parts = fullName.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Select(p => p[0])).ToUpper();
        }

        private static string BuildMetaForCard(ContentItem c)
        {
            var meta = $"Posted: {c.CreatedAt.ToLocalTime():MMM dd, yyyy}";
            if (c.Deadline.HasValue)
            {
                meta += $" | Deadline: {c.Deadline.Value.ToLocalTime():MMM dd, yyyy}";
            }
            if (c.UpdatedAt > c.CreatedAt)
            {
                meta += $" | Edited: {c.UpdatedAt.ToLocalTime():MMM dd, yyyy}";
            }
            var files = c.Attachments?.Count ?? 0;
            if (files > 0)
            {
                meta += $" | Files: {files}";
            }
            return meta;
        }

        private string GenerateMetaText(CreateContentRequest request)
        {
            var meta = $"Posted: {DateTime.UtcNow.ToLocalTime():MMM dd, yyyy}";

            if (!string.IsNullOrEmpty(request.Deadline))
                meta += $" | Deadline: {DateTime.Parse(request.Deadline).ToLocalTime():MMM dd, yyyy}";

            var filesCount = 0;
            if (request.FileNames != null && request.FileNames.Count > 0)
                filesCount = request.FileNames.Count;
            else if (!string.IsNullOrEmpty(request.FileName))
                filesCount = 1;

            if (filesCount > 0)
                meta += $" | Files: {filesCount}";

            return meta;
        }

        private string GetIconClass(string type) => type?.ToLower() switch
        {
            "material" => "fa-solid fa-book-open-reader",
            "task" => "fa-solid fa-file-pen",
            "assessment" => "fa-solid fa-circle-question",
            "meeting" => "fa-solid fa-chalkboard-user",
            _ => "fa-solid fa-file"
        };

        private string GetUrgencyColor(string deadline)
        {
            if (string.IsNullOrEmpty(deadline)) return "yellow";

            var deadlineDate = DateTime.Parse(deadline);
            var daysUntil = (deadlineDate - DateTime.UtcNow).TotalDays;

            return daysUntil <= 2 ? "red" : daysUntil <= 7 ? "yellow" : "green";
        }

        // Request models
        public class AnnouncementRequest
        {
            public string ClassId { get; set; }
            public string Text { get; set; }
        }

        public class DeleteAnnouncementRequest
        {
            public string ContentId { get; set; } = "";
        }

        public class CreateContentRequest
        {
            public string Type { get; set; } = "";
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public string Deadline { get; set; } = "";
            public string Link { get; set; } = "";
            public string ClassId { get; set; } = "";
            public string EbookId { get; set; } = "";

            public string FileName { get; set; } = "";
            public string FileUrl { get; set; } = "";
            public long FileSize { get; set; }
            public int MaxGrade { get; set; } = 100;
            public System.Collections.Generic.List<string> FileNames { get; set; } = new System.Collections.Generic.List<string>();
        }

        [HttpPost("/AdminClass/UploadFile")]
        public async Task<IActionResult> UploadFile([FromForm] UploadFileRequest request)
        {
            try
            {
                if (request.File == null || request.File.Length == 0)
                    return BadRequest(new { success = false, message = "No file uploaded" });

                // Get class information
                var classItem = await _mongoDb.GetClassByCodeAsync(request.ClassCode);
                if (classItem == null)
                    return NotFound(new { success = false, message = "Class not found" });

                // Generate a unique file name
                var fileName = $"{Guid.NewGuid()}_{request.File.FileName}";
                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
                var filePath = Path.Combine(uploadsDir, fileName);
                if (!Directory.Exists(uploadsDir))
                    Directory.CreateDirectory(uploadsDir);

                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.File.CopyToAsync(stream);
                }

                // Create file URL
                var fileUrl = $"/uploads/{fileName}";

                // Create upload record in database
                var uploadItem = new UploadItem
                {
                    ClassId = classItem.Id,
                    FileName = request.File.FileName,
                    FileUrl = fileUrl,
                    FileType = Path.GetExtension(request.File.FileName),
                    FileSize = request.File.Length,
                    UploadedBy = HttpContext.Session.GetString("UserEmail") ?? User?.Identity?.Name ?? "Admin",
                    UploadedAt = DateTime.UtcNow
                };

                await _mongoDb.InsertUploadAsync(uploadItem);

                return Ok(new
                {
                    success = true,
                    message = "File uploaded successfully",
                    fileName = request.File.FileName,
                    fileUrl = fileUrl,
                    fileSize = request.File.Length,
                    uploadId = uploadItem.Id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Add this request model to your AdminClassController
        public class UploadFileRequest
        {
            public string ClassCode { get; set; } = "";
            public string Type { get; set; } = "";
            public IFormFile File { get; set; }
        }
    }
}
