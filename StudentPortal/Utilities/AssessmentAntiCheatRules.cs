using System.Collections.Generic;
using System.Linq;
using StudentPortal.Models.AdminDb;

namespace StudentPortal.Utilities
{
    /// <summary>
    /// Integrity monitoring: students at or above the event threshold are blocked from the assessment
    /// unless a teacher sets <see cref="AssessmentUnlock.Unlocked"/>.
    /// </summary>
    public static class AssessmentAntiCheatRules
    {
        // Lock when weighted integrity events reach this count (e.g. 20 cheats → locked at the 20th).
        public const int IntegrityLockThreshold = 20;

        private static int Weight(AntiCheatLog l)
        {
            // Only count the integrity events we care about for lockouts:
            // copy/paste, inspect, print screen, tab switching, open programs, and screen share off/on.
            //
            // Many events represent a single action; treat them as 1 even if a buggy client sent cumulative counts.
            var t = (l.EventType ?? string.Empty).ToLowerInvariant();
            if (t == "copy_paste" || t == "inspect" || t == "print_screen" || t == "tab_switch" || t == "open_programs" || t == "screen_share")
                return 1;
            // Ignore other telemetry for lockouts (mouse behavior).
            if (t == "mouse_activity")
                return 0;
            return l.EventCount > 0 ? l.EventCount : 1;
        }

        public static int SumIntegrityEvents(IEnumerable<AntiCheatLog> logs, string? studentId, string? studentEmail)
        {
            return logs
                .Where(l => (!string.IsNullOrEmpty(studentId) && l.StudentId == studentId)
                    || (!string.IsNullOrEmpty(studentEmail)
                        && string.Equals(l.StudentEmail, studentEmail, System.StringComparison.OrdinalIgnoreCase)))
                .Sum(Weight);
        }

        /// <summary>
        /// If a teacher restored access (<see cref="AssessmentUnlock.Unlocked"/>), treat <see cref="AssessmentUnlock.UnlockedAtUtc"/>
        /// as a new "baseline" and only count events that happened after that moment for re-locking.
        /// </summary>
        public static int SumIntegrityEventsForLock(IEnumerable<AntiCheatLog> logs, string? studentId, string? studentEmail, AssessmentUnlock? unlock)
        {
            var filtered = logs
                .Where(l => (!string.IsNullOrEmpty(studentId) && l.StudentId == studentId)
                    || (!string.IsNullOrEmpty(studentEmail)
                        && string.Equals(l.StudentEmail, studentEmail, System.StringComparison.OrdinalIgnoreCase)));

            if (unlock != null && unlock.Unlocked && unlock.UnlockedAtUtc.HasValue)
            {
                var since = unlock.UnlockedAtUtc.Value;
                filtered = filtered.Where(l => l.LogTimeUtc >= since);
            }

            return filtered.Sum(Weight);
        }

        public static bool IsIntegrityLockActive(int summedWeightedEventsForLock) =>
            summedWeightedEventsForLock >= IntegrityLockThreshold;

        /// <summary>
        /// Prefer the content document id for Mongo joins. If <paramref name="contentItemId"/> is null or whitespace
        /// (including legacy empty strings), use the route id — otherwise <c>Eq(ContentId, "")</c> matches every
        /// legacy anti-cheat row and falsely locks unrelated assessments.
        /// </summary>
        public static string ResolveAssessmentContentId(string? contentItemId, string routeContentId)
        {
            if (!string.IsNullOrWhiteSpace(contentItemId)) return contentItemId.Trim();
            if (!string.IsNullOrWhiteSpace(routeContentId)) return routeContentId.Trim();
            return string.Empty;
        }
    }
}
