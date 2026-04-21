using System.Collections.Generic;

namespace StudentPortal.Models.StudentDb
{
	public class StudentTodoViewModel
	{
		public string StudentName { get; set; } = "Student";
		public string StudentInitials { get; set; } = "ST";
		public List<SubjectTodo> Subjects { get; set; } = new List<SubjectTodo>();
		/// <summary>
		/// Optional filter (subject title) applied from query string.
		/// Empty/null means "All subjects".
		/// </summary>
		public string? SelectedSubject { get; set; }
	}

	public class SubjectTodo
	{
		public string Title { get; set; } = "";
		public List<TaskItem> Tasks { get; set; } = new List<TaskItem>();
	}

	public class TaskItem
	{
		public string Name { get; set; } = "";
		public string Deadline { get; set; } = "";
		/// <summary>
		/// Link to open the task (e.g. /StudentTask/{classCode}/{contentId}).
		/// </summary>
		public string TargetUrl { get; set; } = "";
		/// <summary>
		/// "todo" or "pastdue" - used to filter on the client
		/// </summary>
		public string Status { get; set; } = "todo";
		/// <summary>
		/// Optional class for color: green, yellow, red, etc.
		/// </summary>
		public string ColorClass { get; set; } = "";
	}
}
