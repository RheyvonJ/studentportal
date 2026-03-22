using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using StudentPortal.Models.AdminDb;
using StudentPortal.Services;
using StudentPortal.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson;

namespace StudentPortal.Controllers.AdminDb
{
	public class AdminManageGradesController : Controller
	{
        private readonly MongoDbService _mongoDb;

        public AdminManageGradesController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        [HttpGet]
		public async Task<IActionResult> Index(string? classCode = null)
		{
			var professorName = HttpContext.Session.GetString("UserName") ?? "Professor";
			var professorEmail = HttpContext.Session.GetString("UserEmail") ?? string.Empty;
			if (!string.IsNullOrWhiteSpace(professorEmail))
			{
				try
				{
					var prof = await _mongoDb.GetProfessorByEmailAsync(professorEmail);
					var full = prof?.GetFullName();
					if (!string.IsNullOrWhiteSpace(full)) professorName = full;
				}
				catch { }
			}
			string initials = string.Concat((professorName ?? "Professor").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(p => p[0])).ToUpper();

			var viewModel = new AdminManageGradesViewModel
			{
				AdminName = professorName,
				AdminInitials = string.IsNullOrWhiteSpace(initials) ? "PR" : initials,
				ClassCode = classCode ?? string.Empty,
				Students = new List<StudentGradeRecord>()
			};

			ViewBag.AdminName = viewModel.AdminName;
			ViewBag.AdminInitials = viewModel.AdminInitials;
			return View("~/Views/AdminDb/AdminManageGrades/Index.cshtml", viewModel);
		}

		// -------------------------------------------
		// IMPORT GRADES ENDPOINT -> AttendanceCopy collection
		// -------------------------------------------
		[HttpPost]
		public async Task<IActionResult> ImportGrades([FromBody] ImportGradesRequest req)
		{
			if (req?.Rows == null || req.Rows.Count == 0)
				return BadRequest(new { success = false, message = "No rows received" });

			// No server-side computation; import values as provided in Excel

            var docs = new List<BsonDocument>();
			string professorName = string.Empty;
			try
			{
				ClassItem? classItem = null;
				if (!string.IsNullOrWhiteSpace(req.ClassCode))
				{
					classItem = await _mongoDb.GetClassByCodeAsync(req.ClassCode);
				}

				var uploaderName = HttpContext.Session?.GetString("UserName") ?? string.Empty;
				var uploaderEmail = HttpContext.Session?.GetString("UserEmail") ?? string.Empty;

				foreach (var row in req.Rows)
				{
					var doc = new BsonDocument();
					foreach (var kvp in row)
					{
						var key = kvp.Key;
						if (string.Equals(key, "Standing", StringComparison.OrdinalIgnoreCase) ||
							string.Equals(key, "Status", StringComparison.OrdinalIgnoreCase) ||
							string.Equals(key, "Result", StringComparison.OrdinalIgnoreCase) ||
							string.Equals(key, "Final Standing", StringComparison.OrdinalIgnoreCase) ||
							string.Equals(key, "FinalStanding", StringComparison.OrdinalIgnoreCase) ||
							string.Equals(key, "Subject_ID", StringComparison.OrdinalIgnoreCase) ||
							string.Equals(key, "SubjectCode", StringComparison.OrdinalIgnoreCase) ||
							string.Equals(key, "Subject_Code", StringComparison.OrdinalIgnoreCase) ||
							string.Equals(key, "Subject Code", StringComparison.OrdinalIgnoreCase) ||
							string.Equals(key, "Subject_Name", StringComparison.OrdinalIgnoreCase) ||
							string.Equals(key, "SubjectName", StringComparison.OrdinalIgnoreCase) ||
							string.Equals(key, "Subject", StringComparison.OrdinalIgnoreCase))
						{
							continue;
						}
						doc[key] = Convert.ToString(kvp.Value) ?? string.Empty;
					}
					var subjectIdKey = row.Keys.FirstOrDefault(k =>
					{
						var nk = new string(k.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
						return nk == "subjectid" || nk == "subjectcode" || nk == "subject";
					});
					var subjectId = subjectIdKey != null && row.TryGetValue(subjectIdKey, out var subjObj) ? (Convert.ToString(subjObj) ?? string.Empty) : string.Empty;
					if (string.IsNullOrWhiteSpace(subjectId))
					{
						var subjectNameKey = row.Keys.FirstOrDefault(k =>
						{
							var nk = new string(k.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
							return nk == "subjectname" || nk == "subject";
						});
						subjectId = subjectNameKey != null && row.TryGetValue(subjectNameKey, out var snObj) ? (Convert.ToString(snObj) ?? string.Empty) : string.Empty;
					}

					if (classItem == null)
					{
						// Fallback: resolve class by subject code or subject name
						ClassItem? bySubject = null;
						bySubject = await _mongoDb.GetClassBySubjectCodeAsync(subjectId);
						if (bySubject == null)
						{
							bySubject = await _mongoDb.GetClassBySubjectNameAsync(subjectId);
						}
						classItem = bySubject;
					}

					professorName = !string.IsNullOrWhiteSpace(uploaderName) ? uploaderName : (classItem?.InstructorName ?? professorName);
					if (string.IsNullOrWhiteSpace(professorName) && !string.IsNullOrWhiteSpace(uploaderEmail))
					{
						try
						{
							var portalUser = await _mongoDb.GetUserByEmailAsync(uploaderEmail);
							if (portalUser != null && !string.IsNullOrWhiteSpace(portalUser.FullName))
							{
								professorName = portalUser.FullName;
							}
							else
							{
								var professor = await _mongoDb.GetProfessorByEmailAsync(uploaderEmail);
								if (professor != null)
								{
									professorName = professor.GetFullName();
								}
							}
						}
						catch { }
					}
					if (string.IsNullOrWhiteSpace(subjectId) && classItem != null)
					{
						subjectId = classItem.SubjectCode ?? string.Empty;
					}
					if (string.IsNullOrWhiteSpace(subjectId) && classItem == null && !string.IsNullOrWhiteSpace(req.ClassCode))
					{
						var byCode = await _mongoDb.GetClassByCodeAsync(req.ClassCode);
						if (byCode != null)
						{
							subjectId = byCode.SubjectCode ?? string.Empty;
							classItem = byCode;
						}
					}
					var standingKey2 = row.Keys.FirstOrDefault(k =>
						string.Equals(k, "Standing", StringComparison.OrdinalIgnoreCase) ||
						string.Equals(k, "Status", StringComparison.OrdinalIgnoreCase) ||
						string.Equals(k, "Result", StringComparison.OrdinalIgnoreCase) ||
						string.Equals(k, "Final Standing", StringComparison.OrdinalIgnoreCase) ||
						string.Equals(k, "FinalStanding", StringComparison.OrdinalIgnoreCase)
					);
					var standingVal = standingKey2 != null && row.TryGetValue(standingKey2, out var st2Obj) ? (Convert.ToString(st2Obj) ?? string.Empty) : string.Empty;
					var fullNameKey = row.Keys.FirstOrDefault(k =>
					{
						var nk = new string(k.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
						return nk == "fullname" || nk == "studentname" || nk == "name" || nk == "full_name";
					});
					var fullNameVal = fullNameKey != null && row.TryGetValue(fullNameKey, out var fnObj) ? (Convert.ToString(fnObj) ?? string.Empty) : string.Empty;
					var arr = new BsonArray { professorName ?? string.Empty, subjectId ?? string.Empty, standingVal ?? string.Empty };
					doc["StandingInfo"] = arr;
						docs.Add(doc);
						var sidKey = row.Keys.FirstOrDefault(k => new string(k.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant() == "studentid") ?? "Student_ID";
						var sidVal = sidKey != null && row.TryGetValue(sidKey, out var sidObj) ? (Convert.ToString(sidObj) ?? string.Empty) : string.Empty;
						await _mongoDb.UpsertAttendanceCopyStandingAsync(sidVal, fullNameVal, arr, subjectId);
				}

                
            }
			catch (Exception ex)
			{
				Console.WriteLine($"[ImportGrades] Failed to insert into AttendanceCopy: {ex.Message}");
				return StatusCode(500, new { success = false, message = "Failed to save to AttendanceCopy" });
			}

			Console.WriteLine($"[ImportGrades] Inserted {req.Rows.Count} rows into AttendanceCopy");
			return Ok(new { success = true });
		}

		private static double? ParseGradePercent(string? v)
		{
			if (string.IsNullOrWhiteSpace(v)) return null;
			var s = v.Trim();
			if (s.EndsWith("%", StringComparison.Ordinal))
			{
				if (double.TryParse(s.TrimEnd('%'), out var p)) return p;
				return null;
			}
			var slashIdx = s.IndexOf('/');
			if (slashIdx > 0)
			{
				var parts = s.Split('/');
				if (parts.Length == 2 && double.TryParse(parts[0].Trim(), out var num) && double.TryParse(parts[1].Trim(), out var den) && den > 0)
				{
					return (num / den) * 100.0;
				}
				return null;
			}
			if (double.TryParse(s, out var n)) return n;
			return null;
		}
	}

	public class ImportGradesRequest
	{
		public List<Dictionary<string, object>> Rows { get; set; } = new();
		public string? ClassCode { get; set; }
	}
}
