namespace StudentPortal.Models.StudentDb
{
    public class AssessmentResult
    {
        public string Id { get; set; } = string.Empty;
        public string ClassId { get; set; } = string.Empty;
        public string ClassCode { get; set; } = string.Empty;
        public string ContentId { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public System.DateTime? SubmittedAt { get; set; }
        /// <summary>
        /// Set when the student hits the integrity lock threshold; survives navigation so they cannot re-enter
        /// the assessment until a teacher restores access (unlock + reset flow).
        /// </summary>
        public System.DateTime? IntegrityLockedAtUtc { get; set; }
        /// <summary>
        /// Number of times a teacher used &quot;Restore access&quot; for this assessment. Only one restore is allowed per student per assessment.
        /// </summary>
        public int TeacherIntegrityRestoreCount { get; set; }
        public double? Score { get; set; }
        public double? MaxScore { get; set; }
        public string Status { get; set; } = "pending";
    }
}
