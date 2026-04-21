using System.Collections.Generic;

namespace SIA_IPT.Models.AdminAssessment
{
    public class AdminAssessmentViewModel
    {
        public string AssessmentId { get; set; } = string.Empty;

		public string AdminInitials { get; set; } = string.Empty;
		public string AdminName { get; set; } = string.Empty;

		public string SubjectName { get; set; } = string.Empty;
		public string SectionName { get; set; } = string.Empty;
		public string SubjectCode { get; set; } = string.Empty;
		public string ClassCode { get; set; } = string.Empty;
		public string InstructorName { get; set; } = string.Empty;
		public string InstructorInitials { get; set; } = string.Empty;
        public string TeacherRole { get; set; } = string.Empty;
        public string TeacherDepartment { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string FloorDisplay { get; set; } = string.Empty;
		public List<string> RecentMaterials { get; set; } = new();
		/// <summary>Recent assessments in this class for sidebar navigation (title + id).</summary>
		public List<RecentAssessmentNavItem> RecentAssessments { get; set; } = new();
		/// <summary>Display label for status badge (Draft / Active / Closed).</summary>
		public string StatusLabel { get; set; } = "Active";
		/// <summary>CSS variant: draft | active | closed</summary>
		public string StatusVariant { get; set; } = "active";
		/// <summary>Deadline as yyyy-MM-dd for date input (empty if none).</summary>
		public string DeadlineIso { get; set; } = string.Empty;
		public string AssessmentTitle { get; set; } = string.Empty;
		public string AssessmentDescription { get; set; } = string.Empty;
		public List<string> Attachments { get; set; } = new();
		public string PostedDate { get; set; } = string.Empty;
        public string Deadline { get; set; } = string.Empty;
        public string EditedDate { get; set; } = string.Empty;
        public List<StudentSubmission> Submissions { get; set; } = new();
        public string LinkUrl { get; set; } = string.Empty;

        public int LogCopy { get; set; }
        public int LogPaste { get; set; }
        public int LogInspect { get; set; }
        public int LogTabSwitch { get; set; }
        public int LogOpenPrograms { get; set; }
        public int LogScreenShare { get; set; }
    }

	public class StudentSubmission
	{
		public string StudentName { get; set; } = string.Empty;
		public string Status { get; set; } = string.Empty;
	}

	public class RecentAssessmentNavItem
	{
		public string Title { get; set; } = string.Empty;
		public string ContentId { get; set; } = string.Empty;
		public bool IsCurrent { get; set; }
	}
}
