using System.Collections.Generic;

namespace StudentPortal.Models.AdminClass
{
    public class AdminClassViewModel
    {
        public string ClassId { get; set; } = "";
        public string SubjectName { get; set; } = "";
        public string SubjectCode { get; set; } = "";
        public string ClassCode { get; set; } = "";
        public string AdminName { get; set; } = "";
        public string AdminInitials { get; set; } = "";
        public string TeacherRole { get; set; } = "";
        public string TeacherDepartment { get; set; } = "";
        public string RoomName { get; set; } = "";
        public string FloorDisplay { get; set; } = "";
        public List<AdminClassRecentUpload> RecentUploads { get; set; } = new();
        public List<AdminClassContent> Contents { get; set; } = new();
    }

    public class AdminClassRecentUpload
    {
        public string IconClass { get; set; } = "";
        public string Title { get; set; } = "";
    }

    public class AdminClassContent
    {
        public string ContentId { get; set; } = "";
        public string Type { get; set; } = "";
        public string Title { get; set; } = "";
        public string IconClass { get; set; } = "";
        public string MetaText { get; set; } = "";
        public string TargetUrl { get; set; } = "";
        public bool HasUrgency { get; set; } = false;
        public string UrgencyColor { get; set; } = "";
    }
}
