using Microsoft.AspNetCore.Mvc;
using SIA_IPT.Models.AdminAssessmentList;
using StudentPortal.Services;
using System.Linq;

namespace SIA_IPT.Controllers
{
    public class AdminAssessmentListController : Controller
    {
        private readonly MongoDbService _mongoDb;
        public AdminAssessmentListController(MongoDbService mongoDb) { _mongoDb = mongoDb; }

        [HttpGet("/AdminAssessmentList")]
        public async Task<IActionResult> Index([FromQuery] string? classCode)
        {
            var items = new List<AssessmentItem>();
            if (!string.IsNullOrEmpty(classCode))
            {
                var contents = await _mongoDb.GetContentsByClassCodeAsync(classCode);
                items = contents.Where(c => c.Type == "assessment")
                                .Select(c => new AssessmentItem { Id = c.Id ?? string.Empty, Title = c.Title })
                                .ToList();
            }

            var model = new AdminAssessmentListViewModel { Assessments = items };
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
            ViewBag.AdminName = professorName;
            ViewBag.AdminInitials = string.IsNullOrWhiteSpace(initials) ? "PR" : initials;
            return View("~/Views/AdminDb/AdminAssessmentList/Index.cshtml", model);
        }
    }
}
