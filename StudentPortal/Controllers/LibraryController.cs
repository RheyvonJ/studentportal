using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using StudentPortal.Services;
using StudentPortal.Models.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace StudentPortal.Controllers
{
    public class LibraryController : Controller
    {
        private readonly LibraryService _libraryService;
        private readonly MongoDbService _mongoService;
        private readonly IConfiguration _configuration;

        public LibraryController(LibraryService libraryService, MongoDbService mongoService, IConfiguration configuration)
        {
            _libraryService = libraryService;
            _mongoService = mongoService;
            _configuration = configuration;
        }

        [HttpGet]   
        public async Task<IActionResult> Search()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userEmail) || userRole?.ToLower() != "student")
            {
                return RedirectToAction("Login", "Account");
            }

            // Get available books by default
            var books = await _libraryService.GetAvailableBooksAsync();
            
            ViewBag.SearchTerm = "";
            ViewBag.UserEmail = userEmail;
            ViewBag.LibraryPortalBaseUrl = _configuration["LibraryPortal:BaseUrl"]
                ?? "https://slshslibrary-production-1346.up.railway.app";
            
            return View(books);
        }

        [HttpPost]
        public async Task<IActionResult> Search(string searchTerm)
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userEmail) || userRole?.ToLower() != "student")
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.UserEmail = userEmail;
            ViewBag.SearchTerm = searchTerm ?? "";
            ViewBag.LibraryPortalBaseUrl = _configuration["LibraryPortal:BaseUrl"]
                ?? "https://slshslibrary-production-1346.up.railway.app";

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                var availableBooks = await _libraryService.GetAvailableBooksAsync();
                return View(availableBooks);
            }

            var books = await _libraryService.SearchBooksAsync(searchTerm);
            return View(books);
        }

        // Lightweight JSON search API for in-page library modals (StudentTask/StudentMaterial/Admin material eBook pickers)
        [HttpGet]
        public async Task<IActionResult> ApiSearch(string q, bool ebooksOnly = false)
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userRole = HttpContext.Session.GetString("UserRole");

            var role = userRole?.ToLowerInvariant() ?? "";
            if (string.IsNullOrEmpty(userEmail) || (role != "student" && role != "professor"))
            {
                return Json(new { success = false, message = "Please log in." });
            }

            try
            {
                var term = q ?? string.Empty;
                Console.WriteLine($"[LibraryController.ApiSearch] Searching for: '{term}'");
                
                List<Book> books;
                
                if (string.IsNullOrWhiteSpace(term))
                {
                    if (ebooksOnly)
                    {
                        Console.WriteLine($"[LibraryController.ApiSearch] No search term - getting ALL eBooks...");
                        books = await _libraryService.GetEbooksAsync();
                    }
                    else
                    {
                        // When no search term, get ALL books from database (no filters - includes unavailable books)
                        Console.WriteLine($"[LibraryController.ApiSearch] No search term - getting ALL books (including unavailable)...");
                        books = await _libraryService.GetAllBooksAsync();
                    }
                }
                else
                {
                    // When there's a search term, search for ALL matching books (no availability filters)
                    Console.WriteLine($"[LibraryController.ApiSearch] Search term provided - searching ALL books (including unavailable)...");
                    books = await _libraryService.SearchBooksAsync(term);
                }
                
                Console.WriteLine($"[LibraryController.ApiSearch] Found {books?.Count ?? 0} total books (all statuses)");

                if (books == null || books.Count == 0)
                {
                    Console.WriteLine($"[LibraryController.ApiSearch] WARNING: No books found in database");
                    return Json(new { success = true, books = new List<object>() });
                }

                if (ebooksOnly)
                    books = books.Where(b => b.EffectiveIsEBook).ToList();

                // Include ALL books in result - don't filter by availability
                var result = books.Select(b => new
                {
                    id = b._id.ToString(),
                    title = b.Title ?? "",
                    author = b.Author ?? "",
                    subject = b.Subject ?? "",
                    category = b.Subject ?? "",
                    description = $"{b.Publisher}".Trim(),
                    publisher = b.Publisher ?? "",
                    isbn = b.ISBN ?? "",
                    classificationNo = b.ClassificationNo ?? "",
                    isAvailable = b.IsAvailable, // This is just for display - we still include unavailable books
                    availableCopies = b.AvailableCopies,
                    totalCopies = b.TotalCopies,
                    isReferenceOnly = b.IsReferenceOnly,
                    isEBook = b.EffectiveIsEBook,
                    ebookFileName = b.EBookFileName ?? "",
                    ebookContentType = b.EBookContentType ?? ""
                }).ToList();

                var availableCount = result.Count(b => b.isAvailable);
                var unavailableCount = result.Count - availableCount;
                Console.WriteLine($"[LibraryController.ApiSearch] Returning {result.Count} books ({availableCount} available, {unavailableCount} unavailable)");
                return Json(new { success = true, books = result });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LibraryController.ApiSearch] Error: {ex.Message}");
                Console.WriteLine($"[LibraryController.ApiSearch] Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Error searching library: {ex.Message}" });
            }
        }

        /// <summary>
        /// Sends the user to the eBook file on the library website (same as opening from the library catalog).
        /// Server-side HTTP proxying often fails from Railway/containers (TLS/DNS/outbound); the browser can load static files reliably.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> LibraryEbookFile(string bookId)
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userRole = HttpContext.Session.GetString("UserRole");
            var role = userRole?.ToLowerInvariant() ?? "";
            if (string.IsNullOrEmpty(userEmail) || (role != "student" && role != "professor"))
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(bookId))
                return NotFound("eBook not found.");

            var book = await _libraryService.GetBookByIdAsync(bookId.Trim());
            if (book == null || !book.EffectiveIsEBook)
                return NotFound("eBook not found.");

            var path = (book.EBookFilePath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(path))
                return NotFound("eBook file is unavailable.");

            var url = BuildLibraryEbookFetchUrl(path);
            if (string.IsNullOrWhiteSpace(url))
                return NotFound("eBook file is unavailable.");

            Console.WriteLine($"[LibraryController.LibraryEbookFile] Redirect bookId={bookId.Trim()} url={url}");
            return Redirect(url);
        }

        /// <inheritdoc cref="LibraryEbookFile"/>
        /// <summary>Legacy route name — redirects the same way as <see cref="LibraryEbookFile"/>.</summary>
        [HttpGet]
        public Task<IActionResult> ViewEbookPdf(string bookId) => LibraryEbookFile(bookId);

        /// <summary>
        /// Mongo often stores <c>/uploads/ebooks/...</c> or an absolute URL from the machine that uploaded the file.
        /// Always fetch from the configured library site origin when the path is under uploads/ebooks.
        /// </summary>
        private string BuildLibraryEbookFetchUrl(string pathOrUrl)
        {
            var baseUrl = (_configuration["LibraryPortal:BaseUrl"]
                ?? "https://slshslibrary-production-1346.up.railway.app").Trim().TrimEnd('/');
            var raw = (pathOrUrl ?? string.Empty).Trim().Replace('\\', '/');
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            static bool IsLoopbackHost(string host) =>
                string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);

            if (Uri.TryCreate(raw, UriKind.Absolute, out var absolute))
            {
                var pq = absolute.PathAndQuery;
                if (pq.StartsWith("/uploads/ebooks/", StringComparison.OrdinalIgnoreCase))
                    return $"{baseUrl}{pq}";
                // Wrong host but same static path pattern — still serve from configured library deployment
                if (!IsLoopbackHost(absolute.Host) && pq.Contains("/uploads/ebooks/", StringComparison.OrdinalIgnoreCase))
                    return $"{baseUrl}{pq}";
                return absolute.ToString();
            }

            if (!raw.StartsWith("/", StringComparison.Ordinal))
                raw = "/" + raw;
            return $"{baseUrl}{raw}";
        }

        /// <summary>
        /// Redirects to the Library System with a signed SSO token so the student is logged in automatically,
        /// then lands on BrowseBooks (book modal opens when bookId is valid).
        /// </summary>
        [HttpGet]
        public IActionResult OpenLibraryBook(string? bookId, string? q)
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userRole = HttpContext.Session.GetString("UserRole");

            var role = userRole?.ToLowerInvariant() ?? "";
            if (string.IsNullOrEmpty(userEmail) || (role != "student" && role != "professor"))
                return RedirectToAction("Login", "Account");

            var libraryBase = (_configuration["LibraryPortal:BaseUrl"]
                ?? "https://slshslibrary-production-1346.up.railway.app").TrimEnd('/');

            var returnPath = "/Student/BrowseBooks";
            var queryParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(bookId))
            {
                var id = bookId.Trim();
                if (Regex.IsMatch(id, "^[a-fA-F0-9]{24}$"))
                    queryParts.Add($"bookId={Uri.EscapeDataString(id)}");
            }
            if (!string.IsNullOrWhiteSpace(q))
                queryParts.Add($"q={Uri.EscapeDataString(q)}");

            var returnUrl = queryParts.Count > 0 ? $"{returnPath}?{string.Join("&", queryParts)}" : returnPath;

            var secret = _configuration["LmsLibrarySso:SharedSecret"];
            var lifetimeSeconds = 120;
            if (int.TryParse(_configuration["LmsLibrarySso:TokenLifetimeSeconds"], out var parsed))
                lifetimeSeconds = Math.Clamp(parsed, 30, 600);

            var token = LmsLibrarySsoTokenBuilder.TryCreateToken(secret, userEmail, TimeSpan.FromSeconds(lifetimeSeconds));
            if (string.IsNullOrEmpty(token))
            {
                var fallback = $"{libraryBase}{returnUrl}";
                return Redirect(fallback);
            }

            var ssoUrl = $"{libraryBase}/Account/SsoFromLms?token={Uri.EscapeDataString(token)}&returnUrl={Uri.EscapeDataString(returnUrl)}";
            return Redirect(ssoUrl);
        }

        [HttpPost]
        public async Task<IActionResult> ReserveBook(string bookId)
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userId = HttpContext.Session.GetString("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userEmail) || userRole?.ToLower() != "student")
            {
                return Json(new { success = false, message = "Please log in as a student to reserve books." });
            }

            if (string.IsNullOrEmpty(bookId))
            {
                return Json(new { success = false, message = "Book ID is required." });
            }

            try
            {
                // Get student number from library system
                var studentNumber = await _libraryService.GetStudentNumberAsync(userEmail);
                
                // If not found in library system, try to get from enrollment system
                if (string.IsNullOrEmpty(studentNumber))
                {
                    var enrollmentStudent = await _mongoService.GetEnrollmentStudentByEmailAsync(userEmail);
                    if (enrollmentStudent != null)
                    {
                        // EnrollmentStudent doesn't have StudentNumber, use Username or Email
                        studentNumber = !string.IsNullOrEmpty(enrollmentStudent.Username) 
                            ? enrollmentStudent.Username 
                            : enrollmentStudent.Email;
                    }
                    else
                    {
                        studentNumber = userEmail; // Fallback to email
                    }
                }

                // Get user ID from library system (require a valid ObjectId)
                var libraryUserId = await _libraryService.GetLibraryUserIdAsync(userEmail);
                if (string.IsNullOrEmpty(libraryUserId))
                {
                    return Json(new { success = false, message = "We couldn't find your library account. Please contact the librarian." });
                }

                var result = await _libraryService.CreateReservationAsync(libraryUserId, bookId, studentNumber);

                if (result.Success)
                {
                    return Json(new { success = true, message = result.Message });
                }
                else
                {
                    return Json(new { success = false, message = result.Message });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LibraryController] Error reserving book: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // Debug endpoint to test database connection and count books
        [HttpGet]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                var allBooks = await _libraryService.GetAllBooksAsync();
                var availableBooks = await _libraryService.GetAvailableBooksAsync();
                
                // Get diagnostic info
                var diagnostics = await _libraryService.GetDiagnosticsAsync();
                
                return Json(new 
                { 
                    success = true, 
                    totalBooks = allBooks?.Count ?? 0,
                    availableBooks = availableBooks?.Count ?? 0,
                    message = $"Found {allBooks?.Count ?? 0} total books, {availableBooks?.Count ?? 0} available",
                    diagnostics = diagnostics
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}", stackTrace = ex.StackTrace });
            }
        }

        [HttpGet]
        public async Task<IActionResult> MyReservations()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userId = HttpContext.Session.GetString("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userEmail) || userRole?.ToLower() != "student")
            {
                return RedirectToAction("Login", "Account");
            }

            var libraryUserId = userId ?? userEmail;
            var reservations = await _libraryService.GetUserReservationsAsync(libraryUserId);
            
            return View(reservations);
        }
    }
}

    
