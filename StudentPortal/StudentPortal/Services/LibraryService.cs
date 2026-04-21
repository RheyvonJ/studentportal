using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using StudentPortal.Models.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Services
{
    public class LibraryService
    {
        private readonly IMongoDatabase _libraryDatabase;
        private readonly IMongoCollection<Book> _books;
        private readonly IMongoCollection<Reservation> _reservations;
        private readonly IMongoCollection<BsonDocument> _users;
        private readonly IMongoCollection<BsonDocument> _studentProfiles;

        public LibraryService(IConfiguration config)
        {
            var libraryConnectionString = config["LibraryDb:ConnectionString"] ?? "";
            var libraryDatabaseName = config["LibraryDb:Database"] ?? "LibraDB";
            
            if (string.IsNullOrEmpty(libraryConnectionString))
            {
                throw new InvalidOperationException("LibraryDb connection string is not configured.");
            }

            var libraryClient = new MongoClient(libraryConnectionString);
            
            // Try to find the correct database
            try
            {
                var databases = libraryClient.ListDatabaseNames().ToList();
                var dbNameVariations = new[] { libraryDatabaseName, "LibraDB", "libradb", "LIBRADB", "LibraryDB" };
                IMongoDatabase? foundDatabase = null;
                
                foreach (var dbName in dbNameVariations)
                {
                    if (databases.Contains(dbName))
                    {
                        var testDb = libraryClient.GetDatabase(dbName);
                        var collections = testDb.ListCollectionNames().ToList();
                        
                        // Check for Books collection
                        if (collections.Contains("Books") || collections.Contains("books"))
                        {
                            foundDatabase = testDb;
                            Console.WriteLine($"[LibraryService] Found library database: {dbName}");
                            break;
                        }
                    }
                }
                
                if (foundDatabase == null)
                {
                    // Fallback to configured database name
                    _libraryDatabase = libraryClient.GetDatabase(libraryDatabaseName);
                    Console.WriteLine($"[LibraryService] Using configured database: {libraryDatabaseName}");
                }
                else
                {
                    _libraryDatabase = foundDatabase;
                }
                
                // Verify connection
                _libraryDatabase.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
                Console.WriteLine($"[LibraryService] LibraryDB connection verified.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LibraryService] Error connecting to LibraryDB: {ex.Message}");
                // Fallback to configured database name
                _libraryDatabase = libraryClient.GetDatabase(libraryDatabaseName);
            }

            // Initialize collections - try different case variations
            var booksCollectionName = GetCollectionName(_libraryDatabase, new[] { "Books", "books", "BOOKS" });
            var reservationsCollectionName = GetCollectionName(_libraryDatabase, new[] { "Reservations", "reservations", "RESERVATIONS" });
            var usersCollectionName = GetCollectionName(_libraryDatabase, new[] { "Users", "users", "USERS" });
            var studentProfilesCollectionName = GetCollectionName(_libraryDatabase, new[] { "StudentProfiles", "studentprofiles", "STUDENTPROFILES" });

            _books = _libraryDatabase.GetCollection<Book>(booksCollectionName);
            _reservations = _libraryDatabase.GetCollection<Reservation>(reservationsCollectionName);
            _users = _libraryDatabase.GetCollection<BsonDocument>(usersCollectionName);
            _studentProfiles = _libraryDatabase.GetCollection<BsonDocument>(studentProfilesCollectionName);
            
            Console.WriteLine($"[LibraryService] Initialized - Using collection '{booksCollectionName}'");
        }

        private string GetCollectionName(IMongoDatabase database, string[] variations)
        {
            var collections = database.ListCollectionNames().ToList();
            foreach (var variation in variations)
            {
                if (collections.Contains(variation))
                {
                    return variation;
                }
            }
            return variations[0]; // Default to first variation
        }

        // Get all books - returns ALL books regardless of availability, IsActive, or any other status
        public async Task<List<Book>> GetAllBooksAsync()
        {
            try
            {
                // First, try to get raw BsonDocuments to see what's actually in the database
                var collectionName = _books.CollectionNamespace.CollectionName;
                var bsonCollection = _libraryDatabase.GetCollection<BsonDocument>(collectionName);
                var rawCount = await bsonCollection.CountDocumentsAsync(_ => true);
                Console.WriteLine($"[LibraryService.GetAllBooksAsync] Collection: '{collectionName}', Raw BsonDocument count: {rawCount}");
                
                if (rawCount == 0)
                {
                    Console.WriteLine($"[LibraryService.GetAllBooksAsync] WARNING: No documents found in collection '{collectionName}'");
                    // Try to list all collections to see what's available
                    var allCollections = _libraryDatabase.ListCollectionNames().ToList();
                    Console.WriteLine($"[LibraryService.GetAllBooksAsync] Available collections: {string.Join(", ", allCollections)}");
                    return new List<Book>();
                }
                
                // Try to get a sample document to see its structure
                var sampleDoc = await bsonCollection.Find(_ => true).FirstOrDefaultAsync();
                if (sampleDoc != null)
                {
                    Console.WriteLine($"[LibraryService.GetAllBooksAsync] Sample document fields: {string.Join(", ", sampleDoc.Names)}");
                    Console.WriteLine($"[LibraryService.GetAllBooksAsync] Sample document (first 500 chars): {sampleDoc.ToJson().Substring(0, Math.Min(500, sampleDoc.ToJson().Length))}");
                }
                
                // Try to deserialize using BsonIgnoreExtraElements approach
                // First, let's try the normal way
                var allBooks = await _books.Find(_ => true).ToListAsync();
                Console.WriteLine($"[LibraryService.GetAllBooksAsync] Deserialized {allBooks?.Count ?? 0} books from {rawCount} documents");
                
                // If deserialization failed, try manual deserialization
                if ((allBooks == null || allBooks.Count == 0) && rawCount > 0)
                {
                    Console.WriteLine($"[LibraryService.GetAllBooksAsync] ERROR: Found {rawCount} documents but deserialized 0 books. Trying manual deserialization...");
                    
                    var rawDocs = await bsonCollection.Find(_ => true).Limit(10).ToListAsync();
                    var manuallyDeserialized = new List<Book>();
                    
                    foreach (var doc in rawDocs)
                    {
                        try
                        {
                            var book = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<Book>(doc);
                            manuallyDeserialized.Add(book);
                        }
                        catch (Exception deserEx)
                        {
                            Console.WriteLine($"[LibraryService.GetAllBooksAsync] Failed to deserialize document: {deserEx.Message}");
                        }
                    }
                    
                    if (manuallyDeserialized.Count > 0)
                    {
                        Console.WriteLine($"[LibraryService.GetAllBooksAsync] Manually deserialized {manuallyDeserialized.Count} books successfully");
                        allBooks = manuallyDeserialized;
                    }
                    else
                    {
                        Console.WriteLine($"[LibraryService.GetAllBooksAsync] ERROR: Manual deserialization also failed. Schema mismatch likely.");
                    }
                }
                
                if (allBooks != null && allBooks.Count > 0)
                {
                    var availableCount = allBooks.Count(b => b.AvailableCopies > 0);
                    var unavailableCount = allBooks.Count - availableCount;
                    Console.WriteLine($"[LibraryService.GetAllBooksAsync] Breakdown: {availableCount} available, {unavailableCount} unavailable");
                }
                
                return allBooks ?? new List<Book>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LibraryService.GetAllBooksAsync] Error: {ex.Message}");
                Console.WriteLine($"[LibraryService.GetAllBooksAsync] Stack trace: {ex.StackTrace}");
                return new List<Book>();
            }
        }

        // Search books by title, author, ISBN, subject, etc.
        // Returns ALL matching books regardless of availability, IsActive, or any other status
        public async Task<List<Book>> SearchBooksAsync(string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    // When no search term, return ALL books (no filters)
                    var allBooks = await _books.Find(_ => true).ToListAsync();
                    Console.WriteLine($"[LibraryService.SearchBooksAsync] Empty search term - found {allBooks?.Count ?? 0} total books (no filters)");
                    return allBooks ?? new List<Book>();
                }

                // With search term, search across ALL books (no availability or IsActive filters)
                // Only filter by the search term itself
                var filter = Builders<Book>.Filter.Or(
                    Builders<Book>.Filter.Regex(b => b.Title, new BsonRegularExpression(searchTerm, "i")),
                    Builders<Book>.Filter.Regex(b => b.Author, new BsonRegularExpression(searchTerm, "i")),
                    Builders<Book>.Filter.Regex(b => b.ISBN, new BsonRegularExpression(searchTerm, "i")),
                    Builders<Book>.Filter.Regex(b => b.Subject, new BsonRegularExpression(searchTerm, "i")),
                    Builders<Book>.Filter.Regex(b => b.ClassificationNo, new BsonRegularExpression(searchTerm, "i")),
                    Builders<Book>.Filter.Regex(b => b.Publisher, new BsonRegularExpression(searchTerm, "i"))
                );

                // Find ALL matching books - no additional filters for availability or IsActive
                var results = await _books.Find(filter).ToListAsync();
                Console.WriteLine($"[LibraryService.SearchBooksAsync] Search term '{searchTerm}' - found {results?.Count ?? 0} books (all statuses)");
                
                if (results != null && results.Count > 0)
                {
                    var availableCount = results.Count(b => b.AvailableCopies > 0);
                    var unavailableCount = results.Count - availableCount;
                    Console.WriteLine($"[LibraryService.SearchBooksAsync] Breakdown: {availableCount} available, {unavailableCount} unavailable");
                }
                
                return results ?? new List<Book>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LibraryService.SearchBooksAsync] Error: {ex.Message}");
                Console.WriteLine($"[LibraryService.SearchBooksAsync] Stack trace: {ex.StackTrace}");
                return new List<Book>();
            }
        }

        // Get book by ID
        public async Task<Book?> GetBookByIdAsync(string bookId)
        {
            if (ObjectId.TryParse(bookId, out ObjectId objectId))
            {
                return await _books.Find(b => b._id == objectId).FirstOrDefaultAsync();
            }
            return null;
        }

        // Get available books only
        public async Task<List<Book>> GetAvailableBooksAsync()
        {
            try
            {
                // Try with IsActive filter first
                var books = await _books.Find(b => b.IsActive && b.AvailableCopies > 0 && !b.IsReferenceOnly).ToListAsync();
                Console.WriteLine($"[LibraryService.GetAvailableBooksAsync] Found {books?.Count ?? 0} active available books");
                
                // If no results, try without IsActive filter (in case IsActive field is not set)
                if (books == null || books.Count == 0)
                {
                    Console.WriteLine($"[LibraryService.GetAvailableBooksAsync] No active books found, trying without IsActive filter...");
                    books = await _books.Find(b => b.AvailableCopies > 0 && !b.IsReferenceOnly).ToListAsync();
                    Console.WriteLine($"[LibraryService.GetAvailableBooksAsync] Found {books?.Count ?? 0} available books (without IsActive filter)");
                }
                
                // If still no results, try getting all books with available copies regardless of reference status
                if (books == null || books.Count == 0)
                {
                    Console.WriteLine($"[LibraryService.GetAvailableBooksAsync] No available books found, trying all books with copies > 0...");
                    books = await _books.Find(b => b.AvailableCopies > 0).ToListAsync();
                    Console.WriteLine($"[LibraryService.GetAvailableBooksAsync] Found {books?.Count ?? 0} books with available copies");
                }
                
                return books ?? new List<Book>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LibraryService.GetAvailableBooksAsync] Error: {ex.Message}");
                Console.WriteLine($"[LibraryService.GetAvailableBooksAsync] Stack trace: {ex.StackTrace}");
                return new List<Book>();
            }
        }

        // Get or find user ID in library system by email
        public async Task<string?> GetLibraryUserIdAsync(string email)
        {
            try
            {
                // Try to find user by email in Users collection
                var userFilter = Builders<BsonDocument>.Filter.Regex("email", new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(email)}$", "i"));
                var user = await _users.Find(userFilter).FirstOrDefaultAsync();

                if (user != null && user.Contains("_id"))
                {
                    return user["_id"].AsObjectId.ToString();
                }

                // Try StudentProfiles collection
                var profileFilter = Builders<BsonDocument>.Filter.Regex("email", new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(email)}$", "i"));
                var profile = await _studentProfiles.Find(profileFilter).FirstOrDefaultAsync();

                if (profile != null && profile.Contains("_id"))
                {
                    return profile["_id"].AsObjectId.ToString();
                }

                // Auto-provision minimal library user if none exists
                var newId = ObjectId.GenerateNewId();
                var newUser = new BsonDocument
                {
                    { "_id", newId },
                    { "email", email },
                    { "isActive", true },
                    { "createdAt", DateTime.UtcNow }
                };
                await _users.InsertOneAsync(newUser);
                return newId.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LibraryService] Error getting library user ID: {ex.Message}");
                return null;
            }
        }

        // Create a reservation
        public async Task<(bool Success, string Message)> CreateReservationAsync(string userIdOrEmail, string bookId, string studentNumber)
        {
            try
            {
                // Check if book exists and is available
                var book = await GetBookByIdAsync(bookId);
                if (book == null)
                {
                    return (false, "Book not found.");
                }

                if (book.IsReferenceOnly)
                {
                    return (false, "This book is for reference only and cannot be reserved.");
                }

                // Resolve user ID; must be a valid ObjectId string
                string libraryUserId = userIdOrEmail;
                if (userIdOrEmail.Contains("@"))
                {
                    var foundUserId = await GetLibraryUserIdAsync(userIdOrEmail);
                    libraryUserId = foundUserId ?? string.Empty;
                }
                if (!ObjectId.TryParse(libraryUserId, out _))
                {
                    return (false, "We couldn't find your library account. Please contact the librarian.");
                }

                // Check if user already has a pending or approved reservation for this book
                var existingReservation = await _reservations.Find(r =>
                    r.UserId == libraryUserId &&
                    r.BookId == bookId &&
                    (r.Status == "Pending" || r.Status == "Approved" || r.Status == "Borrowed")
                ).FirstOrDefaultAsync();

                if (existingReservation != null)
                {
                    return (false, "You already have an active reservation for this book.");
                }

                // Check if book has available copies
                if (book.AvailableCopies <= 0)
                {
                    // Add to waitlist instead
                    var waitlistReservation = new Reservation
                    {
                        _id = ObjectId.GenerateNewId().ToString(),
                        UserId = libraryUserId,
                        BookId = bookId,
                        BookTitle = book.Title,
                        StudentNumber = studentNumber,
                        ReservationDate = DateTime.UtcNow,
                        Status = "Pending",
                        ReservationType = "Waitlist"
                    };

                    await _reservations.InsertOneAsync(waitlistReservation);
                    return (true, "Book is currently unavailable. You have been added to the waitlist.");
                }

                // Create reservation
                var reservation = new Reservation
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    UserId = libraryUserId,
                    BookId = bookId,
                    BookTitle = book.Title,
                    StudentNumber = studentNumber,
                    ReservationDate = DateTime.UtcNow,
                    Status = "Pending",
                    ReservationType = "Reserve"
                };

                await _reservations.InsertOneAsync(reservation);

                // Decrease available copies (hold the book)
                var update = Builders<Book>.Update
                    .Inc(b => b.AvailableCopies, -1)
                    .Set(b => b.UpdatedAt, DateTime.UtcNow);

                await _books.UpdateOneAsync(b => b._id == book._id, update);

                return (true, "Book reservation submitted successfully! Please wait for librarian approval.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LibraryService] Error creating reservation: {ex.Message}");
                return (false, $"Error creating reservation: {ex.Message}");
            }
        }

        // Get user's reservations
        public async Task<List<Reservation>> GetUserReservationsAsync(string userId)
        {
            return await _reservations.Find(r => r.UserId == userId)
                .SortByDescending(r => r.ReservationDate)
                .ToListAsync();
        }

        // Get student number from user email (check StudentProfiles collection)
        public async Task<string?> GetStudentNumberAsync(string userEmail)
        {
            try
            {
                // Try to find student profile by email
                var filter = Builders<BsonDocument>.Filter.Regex("email", new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(userEmail)}$", "i"));
                var studentProfile = await _studentProfiles.Find(filter).FirstOrDefaultAsync();

                if (studentProfile != null)
                {
                    if (studentProfile.Contains("student_number"))
                        return studentProfile["student_number"].AsString;
                    if (studentProfile.Contains("studentNumber"))
                        return studentProfile["studentNumber"].AsString;
                }

                // Fallback: try Users collection
                var userFilter = Builders<BsonDocument>.Filter.Regex("email", new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(userEmail)}$", "i"));
                var user = await _users.Find(userFilter).FirstOrDefaultAsync();

                if (user != null)
                {
                    if (user.Contains("student_number"))
                        return user["student_number"].AsString;
                    if (user.Contains("studentNumber"))
                        return user["studentNumber"].AsString;
                    if (user.Contains("username"))
                        return user["username"].AsString;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LibraryService] Error getting student number: {ex.Message}");
                return null;
            }
        }

        // Diagnostic method to get database information
        public async Task<object> GetDiagnosticsAsync()
        {
            try
            {
                var collectionName = _books.CollectionNamespace.CollectionName;
                var bsonCollection = _libraryDatabase.GetCollection<BsonDocument>(collectionName);
                var rawCount = await bsonCollection.CountDocumentsAsync(_ => true);
                
                var allCollections = _libraryDatabase.ListCollectionNames().ToList();
                
                BsonDocument? sampleDoc = null;
                if (rawCount > 0)
                {
                    sampleDoc = await bsonCollection.Find(_ => true).FirstOrDefaultAsync();
                }
                
                return new
                {
                    databaseName = _libraryDatabase.DatabaseNamespace.DatabaseName,
                    collectionName = collectionName,
                    rawDocumentCount = rawCount,
                    allCollections = allCollections,
                    sampleDocumentFields = sampleDoc != null ? sampleDoc.Names.ToList() : new List<string>(),
                    sampleDocumentPreview = sampleDoc != null ? sampleDoc.ToJson().Substring(0, Math.Min(500, sampleDoc.ToJson().Length)) : null
                };
            }
            catch (Exception ex)
            {
                return new { error = ex.Message, stackTrace = ex.StackTrace };
            }
        }

        public async Task<(List<string> approvedTitles, List<string> rejectedTitles, List<string> cancelledTitles)> GetReservationStatusChangesAsync(string userEmail, DateTime sinceUtc)
        {
            var approved = new List<string>();
            var rejected = new List<string>();
            var cancelled = new List<string>();

            string? libraryUserId = await GetLibraryUserIdAsync(userEmail);
            if (string.IsNullOrEmpty(libraryUserId))
            {
                return (approved, rejected, cancelled);
            }

            var collectionName = _reservations.CollectionNamespace.CollectionName;
            var bsonCollection = _libraryDatabase.GetCollection<BsonDocument>(collectionName);

            var baseOrFilters = new List<FilterDefinition<BsonDocument>>
            {
                Builders<BsonDocument>.Filter.Eq("user_id", new ObjectId(libraryUserId)),
                Builders<BsonDocument>.Filter.Eq("UserId", libraryUserId),
                Builders<BsonDocument>.Filter.Regex("email", new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(userEmail)}$", "i"))
            };
            var baseFilter = Builders<BsonDocument>.Filter.Or(baseOrFilters);
            var statusApproved = Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Eq("status", "Approved"),
                Builders<BsonDocument>.Filter.Eq("Status", "Approved")
            );
            var statusRejected = Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Eq("status", "Rejected"),
                Builders<BsonDocument>.Filter.Eq("Status", "Rejected")
            );
            var statusCancelled = Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Eq("status", "Cancelled"),
                Builders<BsonDocument>.Filter.Eq("Status", "Cancelled"),
                Builders<BsonDocument>.Filter.Eq("status", "Canceled"),
                Builders<BsonDocument>.Filter.Eq("Status", "Canceled")
            );
            var statusFilter = Builders<BsonDocument>.Filter.Or(statusApproved, statusRejected, statusCancelled);
            var combined = Builders<BsonDocument>.Filter.And(baseFilter, statusFilter);

            var docs = await bsonCollection.Find(combined).Limit(200).ToListAsync();

            foreach (var doc in docs)
            {
                string status = string.Empty;
                if (doc.Contains("status") && doc["status"].IsString) status = doc["status"].AsString;
                else if (doc.Contains("Status") && doc["Status"].IsString) status = doc["Status"].AsString;
                DateTime? changedAt = null;

                if (doc.Contains("approval_date") && doc["approval_date"].IsBsonDateTime)
                {
                    changedAt = doc["approval_date"].ToUniversalTime();
                }
                else if (doc.Contains("approvalDate") && doc["approvalDate"].IsBsonDateTime)
                {
                    changedAt = doc["approvalDate"].ToUniversalTime();
                }
                else if (doc.Contains("updated_at") && doc["updated_at"].IsBsonDateTime)
                {
                    changedAt = doc["updated_at"].ToUniversalTime();
                }
                else if (doc.Contains("ReservationDate") && doc["ReservationDate"].IsBsonDateTime)
                {
                    changedAt = doc["ReservationDate"].ToUniversalTime();
                }
                else if (doc.Contains("reservation_date") && doc["reservation_date"].IsBsonDateTime)
                {
                    changedAt = doc["reservation_date"].ToUniversalTime();
                }

                // Fallback: use ObjectId creation time if date fields are missing
                if (!changedAt.HasValue && doc.Contains("_id") && doc["_id"].IsObjectId)
                {
                    try { changedAt = doc["_id"].AsObjectId.CreationTime.ToUniversalTime(); } catch { }
                }

                if (changedAt.HasValue && changedAt.Value >= sinceUtc)
                {
                    string title = string.Empty;
                    if (doc.Contains("book_title")) title = doc["book_title"].IsString ? doc["book_title"].AsString : title;
                    if (string.IsNullOrWhiteSpace(title) && doc.Contains("BookTitle")) title = doc["BookTitle"].IsString ? doc["BookTitle"].AsString : title;
                    if (string.IsNullOrWhiteSpace(title) && doc.Contains("title")) title = doc["title"].IsString ? doc["title"].AsString : title;

                    if (status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
                    {
                        approved.Add(string.IsNullOrWhiteSpace(title) ? "Book reservation" : title);
                    }
                    else if (status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
                    {
                        rejected.Add(string.IsNullOrWhiteSpace(title) ? "Book reservation" : title);
                    }
                    else if (status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                    {
                        cancelled.Add(string.IsNullOrWhiteSpace(title) ? "Book reservation" : title);
                    }
                }
            }

            return (approved.Distinct().ToList(), rejected.Distinct().ToList(), cancelled.Distinct().ToList());
        }

        public async Task<List<(string Title, DateTime DueDate)>> GetDueSoonReservationsAsync(string userEmail, DateTime nowUtc, TimeSpan leadTime)
        {
            var dueSoon = new List<(string Title, DateTime DueDate)>();

            string? libraryUserId = await GetLibraryUserIdAsync(userEmail);
            if (string.IsNullOrEmpty(libraryUserId))
            {
                return dueSoon;
            }

            var windowEnd = nowUtc.Add(leadTime);
            var filter = Builders<Reservation>.Filter.And(
                Builders<Reservation>.Filter.Eq(r => r.UserId, libraryUserId),
                Builders<Reservation>.Filter.In(r => r.Status, new[] { "Borrowed", "Approved" }),
                Builders<Reservation>.Filter.Ne(r => r.DueDate, null),
                Builders<Reservation>.Filter.Gte(r => r.DueDate, nowUtc),
                Builders<Reservation>.Filter.Lte(r => r.DueDate, windowEnd)
            );

            var reservations = await _reservations.Find(filter).Limit(50).ToListAsync();
            foreach (var r in reservations)
            {
                if (r.DueDate.HasValue)
                {
                    dueSoon.Add((string.IsNullOrWhiteSpace(r.BookTitle) ? "Book" : r.BookTitle, r.DueDate.Value));
                }
            }

            return dueSoon;
        }

        public async Task<List<(string Message, DateTime CreatedAt)>> GetRecentPenaltiesAsync(string userEmail, DateTime sinceUtc)
        {
            var notifications = new List<(string Message, DateTime CreatedAt)>();

            string? libraryUserId = await GetLibraryUserIdAsync(userEmail);
            if (string.IsNullOrEmpty(libraryUserId))
            {
                return notifications;
            }

            var allCollections = _libraryDatabase.ListCollectionNames().ToList();
            var possible = new[] { "Penalties", "penalties", "Penalty", "penalty", "Fines", "fines", "LibraryFines", "libraryfines" };
            string? penaltiesCollectionName = possible.FirstOrDefault(n => allCollections.Contains(n));
            if (string.IsNullOrEmpty(penaltiesCollectionName))
            {
                return notifications;
            }

            var coll = _libraryDatabase.GetCollection<BsonDocument>(penaltiesCollectionName);
            var baseOrFilters = new List<FilterDefinition<BsonDocument>>
            {
                Builders<BsonDocument>.Filter.Eq("user_id", new ObjectId(libraryUserId)),
                Builders<BsonDocument>.Filter.Eq("UserId", libraryUserId),
                Builders<BsonDocument>.Filter.Regex("email", new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(userEmail)}$", "i"))
            };
            var baseFilter = Builders<BsonDocument>.Filter.Or(baseOrFilters);

            var docs = await coll.Find(baseFilter).Limit(200).ToListAsync();
            foreach (var doc in docs)
            {
                DateTime? createdAt = null;
                if (doc.Contains("created_at") && doc["created_at"].IsBsonDateTime)
                {
                    createdAt = doc["created_at"].ToUniversalTime();
                }
                else if (doc.Contains("createdAt") && doc["createdAt"].IsBsonDateTime)
                {
                    createdAt = doc["createdAt"].ToUniversalTime();
                }
                else if (doc.Contains("issued_at") && doc["issued_at"].IsBsonDateTime)
                {
                    createdAt = doc["issued_at"].ToUniversalTime();
                }
                else if (doc.Contains("date") && doc["date"].IsBsonDateTime)
                {
                    createdAt = doc["date"].ToUniversalTime();
                }

                // Fallback: use ObjectId creation time
                if (!createdAt.HasValue && doc.Contains("_id") && doc["_id"].IsObjectId)
                {
                    try { createdAt = doc["_id"].AsObjectId.CreationTime.ToUniversalTime(); } catch { }
                }

                if (createdAt.HasValue && createdAt.Value >= sinceUtc)
                {
                    string reason = string.Empty;
                    string amountStr = string.Empty;

                    if (doc.Contains("reason") && doc["reason"].IsString) reason = doc["reason"].AsString;
                    else if (doc.Contains("remarks") && doc["remarks"].IsString) reason = doc["remarks"].AsString;

                    if (doc.Contains("amount"))
                    {
                        try { amountStr = doc["amount"].ToString(); } catch { amountStr = string.Empty; }
                    }
                    else if (doc.Contains("fine") && doc["fine"].IsNumeric)
                    {
                        amountStr = doc["fine"].ToString();
                    }

                    string statusStr = string.Empty;
                    if (doc.Contains("status") && doc["status"].IsString) statusStr = doc["status"].AsString;
                    else if (doc.Contains("Status") && doc["Status"].IsString) statusStr = doc["Status"].AsString;

                    string paymentStatus = string.Empty;
                    if (doc.Contains("payment_status") && doc["payment_status"].IsString) paymentStatus = doc["payment_status"].AsString;
                    else if (doc.Contains("PaymentStatus") && doc["PaymentStatus"].IsString) paymentStatus = doc["PaymentStatus"].AsString;

                    bool paidFlag = (doc.Contains("isPaid") && doc["isPaid"].IsBoolean && doc["isPaid"].AsBoolean)
                                     || (doc.Contains("paid") && doc["paid"].IsBoolean && doc["paid"].AsBoolean);

                    var reasonLower = (reason ?? string.Empty).ToLowerInvariant();
                    if (statusStr.Equals("Paid", StringComparison.OrdinalIgnoreCase)
                        || statusStr.Equals("Settled", StringComparison.OrdinalIgnoreCase)
                        || paymentStatus.Equals("Verified", StringComparison.OrdinalIgnoreCase)
                        || paymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase)
                        || paidFlag
                        || reasonLower.Contains("payment verified")
                        || reasonLower.Contains("paid"))
                    {
                        continue;
                    }

                    string normalizedAmount = amountStr;
                    try
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(amountStr ?? string.Empty, "[0-9]+(?:\\.[0-9]+)?");
                        normalizedAmount = m.Success ? m.Value : (amountStr ?? string.Empty);
                    }
                    catch { normalizedAmount = amountStr ?? string.Empty; }

                    string message = string.Empty;
                    if (!string.IsNullOrWhiteSpace(reason) && !string.IsNullOrWhiteSpace(normalizedAmount))
                    {
                        message = $"Penalty {normalizedAmount} - {reason}";
                    }
                    else if (!string.IsNullOrWhiteSpace(reason))
                    {
                        message = $"Penalty - {reason}";
                    }
                    else if (!string.IsNullOrWhiteSpace(normalizedAmount))
                    {
                        message = $"Penalty {normalizedAmount}";
                    }
                    else
                    {
                        message = "Penalty incurred";
                    }

                    var created = createdAt ?? DateTime.UtcNow;
                    notifications.Add((message, created));
                }
            }

            return notifications;
        }
    }
}

