using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models;
using StudentPortal.Models.AdminMaterial;
using StudentPortal.Models.StudentDb;
using StudentPortal.Services;
using StudentPortal.Models.AdminDb;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using StudentPortal.Utilities;
using Microsoft.AspNetCore.Http;

namespace SIA_IPT.Controllers
{
    public class StudentAnswerAssessmentController : Controller
    {
        private readonly MongoDbService _mongoDb;

        public StudentAnswerAssessmentController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        private async Task<(ClassItem? classItem, ContentItem? contentItem, User? user)> ResolveStudentAssessmentContextAsync(string classCode, string contentId)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email)) return (null, null, null);

            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            var user = await _mongoDb.GetUserByEmailAsync(email);
            return (classItem, contentItem, user);
        }

        private async Task<bool> IsIntegrityLockedForStudentAsync(string classCode, string contentId)
        {
            var (classItem, contentItem, user) = await ResolveStudentAssessmentContextAsync(classCode, contentId);
            if (classItem == null || contentItem == null || user == null) return false;

            var logs = await _mongoDb.GetAntiCheatLogsAsync(classItem.Id, contentItem.Id!);
            var relevant = logs ?? new List<AntiCheatLog>();
            var unlock = await _mongoDb.GetAssessmentUnlockAsync(classItem.Id, contentItem.Id!, user.Id ?? string.Empty);
            var total = AssessmentAntiCheatRules.SumIntegrityEventsForLock(relevant, user.Id, user.Email, unlock);
            return AssessmentAntiCheatRules.IsIntegrityLockActive(total);
        }

        [HttpGet("/StudentAnswerAssessment/{classCode}/{contentId}")]
        public async Task<IActionResult> Index(string classCode, string contentId)
        {
            if (string.IsNullOrEmpty(classCode) || string.IsNullOrEmpty(contentId))
                return NotFound("Class code or Content ID not provided.");

            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Index", "StudentDb");

            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null)
                return NotFound("Class not found.");

            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            if (contentItem == null || contentItem.Type != "assessment")
                return NotFound("Assessment not found.");

            if (contentItem.ClassId != classItem.Id)
                return NotFound("Assessment not found in this class.");

            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (!string.IsNullOrEmpty(user?.Id))
            {
                var result = await _mongoDb.GetAssessmentResultAsync(classItem.Id, contentItem.Id, user.Id);
                if (result != null && (result.SubmittedAt.HasValue || string.Equals(result.Status, "done", System.StringComparison.OrdinalIgnoreCase) || string.Equals(result.Status, "submitted", System.StringComparison.OrdinalIgnoreCase)))
                {
                    TempData["ToastMessage"] = "Assessment already submitted.";
                    return RedirectToAction("Index", "StudentAssessment", new { classCode, contentId, submitted = 1 });
                }
            }

            try
            {
                var logs = await _mongoDb.GetAntiCheatLogsAsync(classItem.Id, contentItem.Id);
                var relevant = logs ?? new System.Collections.Generic.List<StudentPortal.Models.AdminDb.AntiCheatLog>();
                var unlock = !string.IsNullOrEmpty(user?.Id)
                    ? await _mongoDb.GetAssessmentUnlockAsync(classItem.Id, contentItem.Id!, user.Id)
                    : null;
                var studentTotalForLock = AssessmentAntiCheatRules.SumIntegrityEventsForLock(relevant, user?.Id, user?.Email, unlock);
                if (AssessmentAntiCheatRules.IsIntegrityLockActive(studentTotalForLock))
                {
                    TempData["ToastMessage"] = "Assessment locked for your account.";
                    return RedirectToAction("Index", "StudentAssessment", new { classCode, contentId, flag = "void" });
                }
            }
            catch { }

            var vm = new StudentAnswerAssessmentViewModel
            {
                StudentName = user?.FullName ?? "Student",
                StudentInitials = GetInitials(user?.FullName ?? "ST"),
                SubjectName = classItem.SubjectName ?? string.Empty,
                AssessmentTitle = contentItem.Title ?? "Assessment",
                FormUrl = !string.IsNullOrWhiteSpace(contentItem.LinkUrl) ? NormalizeEmbedUrl(contentItem.LinkUrl) : string.Empty,
                Questions = new System.Collections.Generic.List<AssessmentQuestion>()
            };

            return View("~/Views/StudentDb/StudentAnswerAssessment/Index.cshtml", vm);
        }


        [HttpPost("/StudentAnswerAssessment/{classCode}/{contentId}/log-event")]
        public async Task<IActionResult> LogEvent(string classCode, string contentId, [FromBody] AntiCheatLogRequest req)
        {
            if (string.IsNullOrEmpty(classCode) || string.IsNullOrEmpty(contentId))
                return BadRequest("Missing identifiers.");

            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
                return Unauthorized();

            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (classItem == null || contentItem == null || user == null)
                return NotFound();

            var log = new AntiCheatLog
            {
                ClassId = classItem.Id,
                ClassCode = classItem.ClassCode,
                ContentId = contentItem.Id,
                StudentId = user.Id ?? string.Empty,
                StudentEmail = user.Email ?? string.Empty,
                StudentName = user.FullName ?? string.Empty,
                ResultId = req.ResultId ?? string.Empty,
                EventType = req.EventType ?? string.Empty,
                Details = req.Details ?? string.Empty,
                EventCount = req.EventCount,
                EventDuration = req.EventDuration,
                Severity = string.IsNullOrWhiteSpace(req.Severity) ? "low" : req.Severity,
                Flagged = req.Flagged,
                LogTimeUtc = DateTime.UtcNow
            };

            var isDuplicate = await _mongoDb.HasRecentDuplicateAntiCheatLogAsync(
                log.ClassId,
                log.ContentId,
                log.StudentId,
                log.EventType,
                log.Details,
                withinSeconds: 2);

            if (isDuplicate)
            {
                return Ok(new { status = "duplicate_skipped" });
            }

            await _mongoDb.AddAntiCheatLogAsync(log);
            return Ok(new { status = "event_logged" });
        }

        [HttpGet("/StudentAnswerAssessment/{classCode}/{contentId}/anti-cheat-totals")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> AntiCheatTotals(string classCode, string contentId)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
                return Unauthorized(new { success = false });

            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (classItem == null || contentItem == null || user == null)
                return NotFound(new { success = false });

            var logs = await _mongoDb.GetAntiCheatLogsAsync(classItem.Id, contentItem.Id);
            var relevant = (logs ?? new List<AntiCheatLog>())
                .Where(l => (!string.IsNullOrEmpty(user.Id) && l.StudentId == user.Id)
                    || (!string.IsNullOrEmpty(user.Email) && string.Equals(l.StudentEmail, user.Email, System.StringComparison.OrdinalIgnoreCase)))
                .ToList();

            int copy = 0, paste = 0, inspect = 0, print = 0, mouse = 0, tabSwitch = 0, openPrograms = 0, screenShareOff = 0;

            foreach (var l in relevant)
            {
                var c = l.EventCount > 0 ? l.EventCount : 1;
                var t = (l.EventType ?? string.Empty).ToLowerInvariant();
                if (t == "copy_paste")
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(l.Details))
                        {
                            using var doc = JsonDocument.Parse(l.Details);
                            if (doc.RootElement.TryGetProperty("action", out var actEl))
                            {
                                var act = (actEl.GetString() ?? string.Empty).ToLowerInvariant();
                                if (act == "copy") copy += c;
                                else if (act == "paste") paste += c;
                                else copy += c;
                                continue;
                            }
                        }
                    }
                    catch { }
                    copy += c;
                }
                else if (t == "inspect") inspect += c;
                else if (t == "print_screen") print += c;
                else if (t == "mouse_activity") mouse += c;
                else if (t == "tab_switch") tabSwitch += c;
                else if (t == "open_programs") openPrograms += c;
                else if (t == "screen_share")
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(l.Details))
                        {
                            using var doc = JsonDocument.Parse(l.Details);
                            if (doc.RootElement.TryGetProperty("on", out var onEl) && onEl.ValueKind == JsonValueKind.False)
                                screenShareOff += c;
                        }
                    }
                    catch { }
                }
            }

            var total = AssessmentAntiCheatRules.SumIntegrityEvents(relevant, user.Id, user.Email);
            return Ok(new
            {
                success = true,
                totals = new
                {
                    total,
                    copy,
                    paste,
                    inspect,
                    print,
                    mouse,
                    tabSwitch,
                    openPrograms,
                    screenShareOff
                }
            });
        }

        [HttpPost("/StudentAnswerAssessment/{classCode}/{contentId}/mark-answered")]
        public async Task<IActionResult> MarkAnswered(string classCode, string contentId)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email)) return Unauthorized();
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (classItem == null || contentItem == null || user == null) return NotFound();
            if (ContentSubmissionRules.IsSubmissionLocked(contentItem, DateTime.UtcNow))
                return Unauthorized();
            var logsMa = await _mongoDb.GetAntiCheatLogsAsync(classItem.Id, contentItem.Id!);
            var relevantMa = logsMa ?? new System.Collections.Generic.List<AntiCheatLog>();
            var unlockMa = await _mongoDb.GetAssessmentUnlockAsync(classItem.Id, contentItem.Id!, user.Id ?? string.Empty);
            var totalMaForLock = AssessmentAntiCheatRules.SumIntegrityEventsForLock(relevantMa, user.Id, user.Email, unlockMa);
            if (AssessmentAntiCheatRules.IsIntegrityLockActive(totalMaForLock))
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { success = false, message = "Assessment locked due to repeated integrity alerts. Submission is not allowed." });
            await _mongoDb.UpsertAssessmentSubmittedAsync(classItem.Id, classItem.ClassCode, contentItem.Id, user.Id ?? string.Empty, user.Email ?? string.Empty);
            return Ok(new { status = "marked_submitted" });
        }

        private string NormalizeEmbedUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            try
            {
                var u = new System.Uri(url);
                var host = u.Host.ToLowerInvariant();
                var path = u.AbsolutePath;

                // Google Forms
                if (host.Contains("docs.google.com") && path.Contains("/forms/"))
                {
                    var baseUrl = url.Split('#')[0];
                    if (!baseUrl.Contains("embedded=true"))
                    {
                        baseUrl += (baseUrl.Contains("?") ? "&" : "?") + "embedded=true";
                    }
                    return baseUrl;
                }

                // Google Docs Document
                if (host.Contains("docs.google.com") && path.Contains("/document/d/"))
                {
                    if (!path.EndsWith("/preview"))
                    {
                        return $"https://docs.google.com{path}/preview";
                    }
                    return url;
                }

                // Google Sheets
                if (host.Contains("docs.google.com") && path.Contains("/spreadsheets/d/"))
                {
                    // Use pubhtml widget for embeddable view
                    var docId = path.Split('/').FirstOrDefault(p => p.Length > 20);
                    if (!string.IsNullOrEmpty(docId))
                    {
                        return $"https://docs.google.com/spreadsheets/d/{docId}/pubhtml?widget=true&headers=false";
                    }
                }

                // Google Slides
                if (host.Contains("docs.google.com") && path.Contains("/presentation/d/"))
                {
                    var docId = path.Split('/').FirstOrDefault(p => p.Length > 20);
                    if (!string.IsNullOrEmpty(docId))
                    {
                        return $"https://docs.google.com/presentation/d/{docId}/embed?start=false&loop=false&delayms=3000";
                    }
                }

                // Google Drive file preview
                if (host.Contains("drive.google.com") && path.Contains("/file/d/"))
                {
                    var segments = path.Split('/');
                    var idIndex = System.Array.IndexOf(segments, "d");
                    if (idIndex >= 0 && idIndex + 1 < segments.Length)
                    {
                        var id = segments[idIndex + 1];
                        return $"https://drive.google.com/file/d/{id}/preview";
                    }
                }

                return url;
            }
            catch
            {
                return url;
            }
        }

        private string GetInitials(string name)
        {
            var parts = (name ?? string.Empty).Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return ($"{parts[0][0]}{parts[^1][0]}").ToUpper();
            if (parts.Length == 1) return parts[0].Substring(0, System.Math.Min(2, parts[0].Length)).ToUpper();
            return "ST";
        }

        // POST: /StudentAnswerAssessment/SubmitAssessment  (form post)
        [HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> SubmitAssessment([FromForm] StudentAnswerSubmission submission)
		{
			if (submission == null)
			{
				TempData["ToastMessage"] = "Submission failed: payload missing.";
				return RedirectToAction("Index");
			}

            var classCode = (submission.ClassCode ?? string.Empty).Trim();
            var contentId = (submission.ContentId ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(classCode) && !string.IsNullOrWhiteSpace(contentId)
                && await IsIntegrityLockedForStudentAsync(classCode, contentId))
            {
                TempData["ToastMessage"] = "Assessment locked due to repeated integrity alerts. Submission is not allowed.";
                return RedirectToAction("Index", "StudentAssessment", new { classCode, contentId, flag = "void" });
            }

			// TODO: replace with real persistence logic (DB, queue, etc.)
			Console.WriteLine($"[Assessment Submit] StudentId: {submission.StudentId ?? "anonymous"} - Received {submission.Answers?.Count ?? 0} answers at {DateTime.UtcNow:o}");
			if (!string.IsNullOrEmpty(submission.AntiCheatSummary))
			{
				Console.WriteLine($"[AntiCheat Summary] {submission.AntiCheatSummary}");
			}

			TempData["ToastMessage"] = "? Assessment submitted successfully!";
			return RedirectToAction("Index");
		}

		// POST: /StudentAnswerAssessment/SubmitJson  (ajax JSON)
		[HttpPost("StudentAnswerAssessment/SubmitJson")]
		[Consumes("application/json")]
		public async Task<IActionResult> SubmitAssessmentJson([FromBody] StudentAnswerSubmission submission)
		{
			if (submission == null)
			{
				return BadRequest(new { success = false, message = "payload missing" });
			}

            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
                return Unauthorized(new { success = false, message = "Not authenticated." });

            var classCode = (submission.ClassCode ?? string.Empty).Trim();
            var contentId = (submission.ContentId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(classCode) || string.IsNullOrWhiteSpace(contentId))
                return BadRequest(new { success = false, message = "classCode and contentId are required." });

            if (await IsIntegrityLockedForStudentAsync(classCode, contentId))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { success = false, message = "Assessment locked due to repeated integrity alerts. Submission is not allowed." });
            }

			// TODO: persist submission (DB, storage). For now log to console.
			Console.WriteLine($"[Assessment SubmitJson] StudentId: {submission.StudentId ?? "anonymous"} - {submission.Answers?.Count ?? 0} answers");
			if (!string.IsNullOrEmpty(submission.AntiCheatSummary))
			{
				Console.WriteLine($"[AntiCheat Summary] {submission.AntiCheatSummary}");
			}

			// Return a JSON success payload
			return Ok(new { success = true, message = "Assessment received" });
		}

		// POST: /StudentAnswerAssessment/ReportAntiCheat
		// Accepts larger anti-cheat logs if the client wants to offload them to server
		[HttpPost("StudentAnswerAssessment/ReportAntiCheat")]
		[Consumes("application/json")]
		public IActionResult ReportAntiCheat([FromBody] AntiCheatReport report)
		{
			if (report == null)
			{
				return BadRequest(new { success = false, message = "report missing" });
			}

			// TODO: persist or forward the anti-cheat log; for now log to console
			Console.WriteLine($"[AntiCheat Report] source={report.Source} received {report.Events?.Count ?? 0} events at {DateTime.UtcNow:o}");
			return Ok(new { success = true });
		}
	}
}
