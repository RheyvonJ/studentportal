using System;

namespace StudentPortal.Models.AdminDb
{
    /// <summary>
    /// Student who reached the integrity event threshold for an assessment (may still be blocked or teacher-unlocked).
    /// </summary>
    public class CheatLockedStudentRow
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public int IntegrityEventTotal { get; set; }
        public bool TeacherRestoredAccess { get; set; }
        public string? RestoredByEmail { get; set; }
        public DateTime? RestoredAtUtc { get; set; }
        /// <summary>
        /// False after the teacher has already used their one allowed integrity restore for this student on this assessment.
        /// </summary>
        public bool CanTeacherRestoreIntegrity { get; set; } = true;
    }
}
