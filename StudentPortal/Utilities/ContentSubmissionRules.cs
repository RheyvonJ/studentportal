using System;
using StudentPortal.Models.AdminMaterial;

namespace StudentPortal.Utilities
{
    /// <summary>
    /// Submission window for tasks and assessments stored as <see cref="ContentItem"/>.
    /// Past deadline, submissions are blocked unless the teacher allows late submissions or extends the deadline.
    /// </summary>
    public static class ContentSubmissionRules
    {
        public static bool IsSubmissionLocked(ContentItem? content, DateTime utcNow)
        {
            if (content == null) return true;
            if (content.AllowSubmissionsPastDeadline) return false;
            if (!content.Deadline.HasValue) return false;
            return utcNow > content.Deadline.Value;
        }
    }
}
