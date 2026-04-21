namespace StudentPortal.Models.StudentDb
{
    public class StudentTaskRecentNavItem
    {
        public string Title { get; set; } = "";
        public string ContentId { get; set; } = "";
        public bool IsCurrent { get; set; }
    }

    public class StudentTaskViewModel
    {
        public string TaskId { get; set; } = "";
        public string ClassCode { get; set; } = "";
        public string SubjectName { get; set; } = "";
        public string SectionName { get; set; } = "";
        public string SubjectCode { get; set; } = "";
        public string InstructorName { get; set; } = "";
        public string InstructorInitials { get; set; } = "";
        public string InstructorRole { get; set; } = "";
        public string TeacherDepartment { get; set; } = "";
        public string RoomName { get; set; } = "";
        public string FloorDisplay { get; set; } = "";
        public string TaskTitle { get; set; } = "";
        public string Description { get; set; } = "";
        public string PostedDate { get; set; } = "";
        public string Deadline { get; set; } = "";
        public DateTime? DeadlineDate { get; set; }
        public string StudentName { get; set; } = "";
        public string StudentEmail { get; set; } = "";
        public string StudentInitials { get; set; } = "";
        public List<TaskAttachment> Attachments { get; set; } = new();
        public List<StudentTaskRecentNavItem> RecentTasks { get; set; } = new();

        // Submission properties
        public bool IsSubmitted { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public bool IsApproved { get; set; }
        public bool HasPassed { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string Grade { get; set; } = "";
        public string Feedback { get; set; } = "";
        public string PrivateComment { get; set; } = "";

        // Submitted file info
        public string SubmittedFileName { get; set; } = "";
        public string SubmittedFileUrl { get; set; } = "";
        public string SubmittedFileSize { get; set; } = "";
        /// <summary>True when the deadline has passed and the teacher has not allowed late submissions.</summary>
        public bool IsSubmissionLocked { get; set; }
    }

    public class TaskAttachment
    {
        public string FileName { get; set; } = "";
        public string FileUrl { get; set; } = "";
    }
}
