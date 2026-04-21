using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using StudentPortal.Utilities;

namespace StudentPortal.Models.AdminDb
{
    [BsonIgnoreExtraElements]
    public class AdminDashboardViewModel
    {
        public string AdminName { get; set; } = "Admin";
        public string AdminInitials { get; set; } = "AD";
        public List<ClassItem> Classes { get; set; } = new();

    }

    [BsonIgnoreExtraElements]
    public class ClassItem
    {
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString(); // ✅ Automatically generate a new ObjectId
        public string SubjectName { get; set; } = "";
        public string SubjectCode { get; set; } = "";
        public string ScheduleId { get; set; } = "";

        /// <summary>Enrollment DB section document id (e.g. SHSSections._id). Used for roster and invite recipient resolution.</summary>
        public string EnrollmentSectionId { get; set; } = "";

        public string Section { get; set; } = "";
        public string SchoolYear { get; set; } = "";
        public string Course { get; set; } = "";
        public string Year { get; set; } = "";
        public string Semester { get; set; } = "";
        public string ClassCode { get; set; } = "";

        /// <summary>
        /// Optional seating chart ordering for Manage Class.
        /// Keys are typically portal user ids (preferred) with email fallback for roster rows without a portal id.
        /// Values are 0-based seat indices in row-major order for the configured rows×cols grid.
        /// </summary>
        public Dictionary<string, int> SeatAssignmentsByStudentKey { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public string BackgroundImageUrl { get; set; } = "";
        public string InstructorName { get; set; } = string.Empty;

        // Owner email used to filter classes per professor
        public string OwnerEmail { get; set; } = string.Empty;

        // optional: keep old properties for backwards compat/future code that used them
        public string CreatorName { get; set; } = string.Empty;
        public string CreatorInitials { get; set; } = string.Empty;
        public string CreatorRole { get; set; } = "Creator";

        [BsonIgnore]
        public string SectionLabel => SectionFormatter.Format(Course, Year, Section);

        /// <summary>Display only: room from schedule (e.g. "Room 305").</summary>
        [BsonIgnore]
        public string RoomDisplay { get; set; } = string.Empty;

        /// <summary>Display only: schedule days (e.g. "Mon & Wed").</summary>
        [BsonIgnore]
        public string ScheduleDisplay { get; set; } = string.Empty;

        /// <summary>Display only: time range (e.g. "10:00 AM – 11:30 AM").</summary>
        [BsonIgnore]
        public string TimeDisplay { get; set; } = string.Empty;

        /// <summary>Display only: combined schedule and time for compact display.</summary>
        [BsonIgnore]
        public string ScheduleTimeDisplay { get; set; } = string.Empty;

        /// <summary>Whether the class is archived (for status badge).</summary>
        [BsonIgnore]
        public bool IsArchived { get; set; }
    }
}
