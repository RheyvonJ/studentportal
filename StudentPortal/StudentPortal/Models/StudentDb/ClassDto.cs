// StudentPortal.Models.Studentdb.ClassContent.cs
namespace StudentPortal.Models.Studentdb
{
    public class ClassContent
    {
        public string Title { get; set; } = string.Empty;
        public string SubjectCode { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
        public string InstructorRole { get; set; } = string.Empty;
        public string BackgroundImageUrl { get; set; } = string.Empty;

        // NEW: include class code so Student can navigate/join references
        public string ClassCode { get; set; } = string.Empty;
        // NEW: Approved / Pending
        public string Status { get; set; } = "";

        /// <summary>Room from schedule (e.g. "Room 305").</summary>
        public string RoomDisplay { get; set; } = string.Empty;

        /// <summary>Schedule and time (e.g. "Mon & Wed • 10:00 AM – 11:30 AM").</summary>
        public string ScheduleTimeDisplay { get; set; } = string.Empty;
    }
}
