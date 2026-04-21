using System.Collections.Generic;
using StudentPortal.Models.Library;

namespace StudentPortal.Models.StudentDb
{
	public class StudentRecentUploadItem
	{
		public string Title { get; set; } = string.Empty;
		public string TargetUrl { get; set; } = string.Empty;
		public string IconClass { get; set; } = "fa-solid fa-book-open-reader";
		public string ContentId { get; set; } = string.Empty;
	}

	public class StudentMaterialViewModel
	{
		public string MaterialId { get; set; } = string.Empty;
		public string SubjectName { get; set; } = string.Empty;
		public string SectionName { get; set; } = string.Empty;
		public string SubjectCode { get; set; } = string.Empty;
		public string ClassCode { get; set; } = string.Empty;
		public string InstructorName { get; set; } = string.Empty;
		public string InstructorInitials { get; set; } = string.Empty;
		public string InstructorRole { get; set; } = string.Empty;
		public string TeacherDepartment { get; set; } = string.Empty;
		public string RoomName { get; set; } = string.Empty;
		public string FloorDisplay { get; set; } = string.Empty;

		public string MaterialTitle { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public string UploadedBy { get; set; } = string.Empty;
		public DateTime UploadDate { get; set; }

		public List<string> RecentMaterials { get; set; } = new();
		public List<StudentRecentUploadItem> RecentMaterialRows { get; set; } = new();
		public List<MaterialFile> Files { get; set; } = new();
		public List<MaterialLinkedEbookDisplay> LinkedEbooks { get; set; } = new();

	}

	public class MaterialFile
    {
		public string FileName { get; set; }
		public string FileUrl { get; set; }
	}
}
