using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace StudentPortal.Models.AdminTask
{
    public class AdminTaskRecentNavItem
    {
        public string Title { get; set; } = "";
        public string ContentId { get; set; } = "";
        public bool IsCurrent { get; set; }
    }

    public class AdminTaskViewModel
    {
        public string TaskId { get; set; } = "";
        public string ClassId { get; set; } = "";
        public string SubjectName { get; set; } = "";
        public string SectionName { get; set; } = "";
        public string SubjectCode { get; set; } = "";
        public string ClassCode { get; set; } = "";
        public string InstructorName { get; set; } = "";
        public string InstructorInitials { get; set; } = "";
        public string TeacherRole { get; set; } = "";
        public string TeacherDepartment { get; set; } = "";
        public string RoomName { get; set; } = "";
        public string FloorDisplay { get; set; } = "";
        public string TaskTitle { get; set; } = "";
        public string TaskDescription { get; set; } = "";
        public List<string> Attachments { get; set; } = new();
        public DateTime PostedDate { get; set; } = DateTime.UtcNow;
        public DateTime? Deadline { get; set; }
        /// <summary>Teacher allowed late submissions without moving the deadline.</summary>
        public bool AllowSubmissionsPastDeadline { get; set; }
        /// <summary>True when past deadline and late submissions are not allowed (students cannot submit).</summary>
        public bool IsSubmissionLockedForStudents { get; set; }
        public DateTime? EditedDate { get; set; }
        public List<TaskSubmission> Submissions { get; set; } = new();
        public int TaskMaxGrade { get; set; } = 100;
        /// <summary>Recent tasks in this class (for sidebar nav).</summary>
        public List<AdminTaskRecentNavItem> RecentTasks { get; set; } = new();
    }

    public class TaskSubmission
    {
        public string Id { get; set; } = "";
        public string FullName { get; set; } = "";
        public string StudentEmail { get; set; } = "";
        public bool Submitted { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public bool IsApproved { get; set; }
        public bool HasPassed { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string Grade { get; set; } = "";
        public string Feedback { get; set; } = "";

        // File properties - these should be strings for display
        public string SubmittedFileName { get; set; } = "";
        public string SubmittedFileUrl { get; set; } = "";
        public string SubmittedFileSize { get; set; } = "";
    }

        // Database model
        public class TaskItem
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string ClassId { get; set; } = "";

        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> Attachments { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? Deadline { get; set; }
        public string CreatedBy { get; set; } = "";
        public int MaxGrade { get; set; } = 100;
    }

    public class TaskReplyItem
    {
        public string AuthorEmail { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public string Role { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class TaskCommentItem
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string TaskId { get; set; } = "";

        [BsonRepresentation(BsonType.ObjectId)]
        public string ClassId { get; set; } = "";

        public string AuthorEmail { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public string Role { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<TaskReplyItem> Replies { get; set; } = new();
    }
}
