namespace StudentPortal.Models.StudentDb
{
    public class StudentAnnouncementViewModel
    {
        public string ContentId { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string SubjectCode { get; set; } = string.Empty;
        public string ClassCode { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
        public string InstructorInitials { get; set; } = string.Empty;
        public string InstructorRole { get; set; } = string.Empty;
        /// <summary>Optional program/department line under the teacher name (e.g. STEM).</summary>
        public string TeacherDepartment { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string FloorDisplay { get; set; } = string.Empty;

        public string Title { get; set; } = "Announcement";
        public string Description { get; set; } = string.Empty;
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? EditedDate { get; set; }

        public List<MaterialFile> Files { get; set; } = new();

        public List<string> RecentAnnouncements { get; set; } = new();

        /// <summary>True when the signed-in user owns this class (can delete announcements).</summary>
        public bool CanManageAnnouncement { get; set; }
    }
}
