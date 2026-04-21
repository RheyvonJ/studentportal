using System.Collections.Generic;

namespace StudentPortal.Models.StudentDb
{
	public class StudentAssessmentViewModel
	{
		public string AssessmentTitle { get; set; } = "";
		public string Description { get; set; } = "";
		public string PostedDate { get; set; } = "";
		public string Deadline { get; set; } = "";
		public string StudentName { get; set; } = "";
		public string StudentInitials { get; set; } = "";
		public string InstructorName { get; set; } = "";
		public string InstructorInitials { get; set; } = "";
		public string InstructorRole { get; set; } = "";
		public string TeacherDepartment { get; set; } = "";
		public string RoomName { get; set; } = "";
		public string FloorDisplay { get; set; } = "";
		public List<TaskAttachment> Attachments { get; set; } = new();

		public string ClassCode { get; set; } = "";
		public string SectionName { get; set; } = "";
		public string SubjectName { get; set; } = "";
		public string SubjectCode { get; set; } = "";
		public string ContentId { get; set; } = "";
		public bool IsAnswered { get; set; } = false;
		public double? Score { get; set; }
		public double? MaxScore { get; set; }
		public bool IsSubmissionLocked { get; set; }
	}
}
	
