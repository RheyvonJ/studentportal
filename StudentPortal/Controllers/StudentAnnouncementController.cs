using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.StudentDb;
using StudentPortal.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers
{
    public class StudentAnnouncementController : Controller
    {
        private readonly MongoDbService _mongoDb;

        public StudentAnnouncementController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        [HttpGet("/StudentAnnouncement/{classCode}/{contentId}")]
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
            if (contentItem == null || contentItem.Type != "announcement")
                return NotFound("Announcement not found.");

            if (contentItem.ClassId != classItem.Id)
                return NotFound("Announcement not found in this class.");

            var recent = (await _mongoDb.GetContentsForClassAsync(classItem.Id, classItem.ClassCode))
                .Where(c => c.Type == "announcement")
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .Select(c => string.IsNullOrWhiteSpace(c.Title) ? "Announcement" : c.Title)
                .ToList();

            var instructorName = !string.IsNullOrWhiteSpace(classItem.InstructorName)
                ? classItem.InstructorName
                : (!string.IsNullOrWhiteSpace(classItem.CreatorName)
                    ? classItem.CreatorName
                    : (await _mongoDb.GetProfessorByEmailAsync(classItem.OwnerEmail))?.GetFullName() ?? "Instructor");
            var initials = !string.IsNullOrWhiteSpace(classItem.CreatorInitials)
                ? classItem.CreatorInitials
                : GetInitials(instructorName);
            var roleLabel = (!string.IsNullOrWhiteSpace(classItem.CreatorRole) && classItem.CreatorRole.ToLower() == "professor") ? "Professor" : "Instructor";

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

            var uploads = await _mongoDb.GetUploadsByContentIdAsync(contentId);
            var files = uploads
                .Select(u => new MaterialFile { FileName = u.FileName ?? string.Empty, FileUrl = u.FileUrl ?? string.Empty })
                .Where(f => !string.IsNullOrWhiteSpace(f.FileName))
                .ToList();
            if (files.Count == 0 && contentItem.Attachments != null)
            {
                foreach (var a in contentItem.Attachments.Where(x => !string.IsNullOrWhiteSpace(x)))
                    files.Add(new MaterialFile { FileName = a.Trim(), FileUrl = string.Empty });
            }

            var vm = new StudentAnnouncementViewModel
            {
                ContentId = contentItem.Id ?? contentId,
                SubjectName = classItem.SubjectName ?? string.Empty,
                SubjectCode = classItem.SubjectCode ?? string.Empty,
                ClassCode = classItem.ClassCode ?? string.Empty,
                InstructorName = instructorName,
                InstructorInitials = string.IsNullOrWhiteSpace(initials) ? "IN" : initials,
                InstructorRole = roleLabel,
                TeacherDepartment = teacherDepartment,
                RoomName = roomName,
                FloorDisplay = floorDisplay,

                Title = string.IsNullOrWhiteSpace(contentItem.Title) ? "Announcement" : contentItem.Title,
                Description = contentItem.Description ?? string.Empty,
                UploadedBy = contentItem.UploadedBy ?? string.Empty,
                CreatedAt = contentItem.CreatedAt,
                EditedDate = contentItem.UpdatedAt > contentItem.CreatedAt ? contentItem.UpdatedAt.ToString("MMM d, yyyy") : null,
                Files = files,
                RecentAnnouncements = recent
            };

            ViewBag.CurrentPage = "subjects";
            return View("~/Views/StudentDb/StudentAnnouncement/Index.cshtml", vm);
        }

        private string GetInitials(string name)
        {
            var parts = (name ?? string.Empty).Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return ($"{parts[0][0]}{parts[^1][0]}").ToUpper();
            if (parts.Length == 1) return parts[0].Substring(0, System.Math.Min(2, parts[0].Length)).ToUpper();
            return "IN";
        }
    }
}
