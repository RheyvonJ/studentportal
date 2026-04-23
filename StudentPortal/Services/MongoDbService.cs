using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using StudentPortal.Models;
using StudentPortal.Models.AdminAssessment;
using StudentPortal.Models.AdminClass;
using StudentPortal.Models.AdminDb;
using StudentPortal.Models.AdminMaterial;
using StudentPortal.Models.AdminTask;
using StudentPortal.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StudentPortal.Services
{
    public class MongoDbService
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoDatabase _enrollmentDatabase;
        private readonly IMongoDatabase _professorDatabase;
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<ClassItem> _classes;
        private readonly IMongoCollection<StudentRecord> _students;
        private readonly IMongoCollection<UploadItem> _uploadCollection;
        private readonly IMongoCollection<ContentItem> _contentCollection;
        private readonly IMongoCollection<JoinRequest> _joinRequestsCollection;
        private readonly IMongoCollection<TaskItem> _taskCollection;
        private readonly IMongoCollection<StudentPortal.Models.AdminDb.AttendanceRecord> _attendanceCollection;
        private readonly IMongoCollection<BsonDocument> _attendanceCopyCollection;
        private readonly IMongoCollection<Submission> _submissionsCollection;
        private readonly IMongoCollection<StudentPortal.Models.AdminTask.TaskCommentItem> _taskCommentsCollection;
        private readonly IMongoCollection<StudentPortal.Models.AdminDb.AntiCheatLog> _antiCheatLogsCollection;
        private readonly IMongoCollection<StudentPortal.Models.AdminDb.AssessmentUnlock> _assessmentUnlocksCollection;
        private readonly IMongoCollection<StudentPortal.Models.StudentDb.AssessmentResult> _assessmentResultsCollection;
        private readonly IMongoCollection<StudentPortal.Models.StudentDb.UserNotification> _userNotifications;
        private readonly IMongoCollection<EnrollmentStudent> _enrollmentStudents;
        private readonly IMongoCollection<Professor> _professors;
        private readonly string _professorCollectionName;

        public IMongoDatabase Database => _database;

        public MongoDbService(IConfiguration config)
        {
            // Student Portal database connection
            var client = new MongoClient(config["MongoDb:ConnectionString"]);
            _database = client.GetDatabase(config["MongoDb:Database"]);

            // Enrollment database connection (Enrollment system)
            var enrollmentConnectionString = config["EnrollmentDb:ConnectionString"] ?? config["MongoDb:ConnectionString"];
            var enrollmentDatabaseName = config["EnrollmentDb:Database"] ?? "EnrollmentSystem";
            var enrollmentClient = new MongoClient(enrollmentConnectionString);
            
            // Test enrollment database connection and find the right database
            try
            {
                var databases = enrollmentClient.ListDatabaseNames().ToList();
                
                // Try different case variations of the configured database name
                // Note: MongoDB database names are case-sensitive
                var dbNameVariations = new[]
                {
                    enrollmentDatabaseName,
                    enrollmentDatabaseName.ToLowerInvariant(),
                    enrollmentDatabaseName.ToUpperInvariant()
                };
                IMongoDatabase? foundDatabase = null;
                string? foundDbName = null;
                
                foreach (var dbName in dbNameVariations)
                {
                    // Check if database exists (case-sensitive check)
                    if (databases.Contains(dbName))
                    {
                        var testDb = enrollmentClient.GetDatabase(dbName);
                        
                        try
                        {
                        var collections = testDb.ListCollectionNames().ToList();
                            
                            // Check for SHSStudents / students collection (case-sensitive)
                        if (collections.Contains("SHSStudents"))
                        {
                            var studentsCollection = testDb.GetCollection<BsonDocument>("SHSStudents");
                            var studentCount = studentsCollection.CountDocuments(FilterDefinition<BsonDocument>.Empty);
                                
                                // Use this database if it has students
                                foundDatabase = testDb;
                                foundDbName = dbName;
                                break;
                            }
                            else
                            {
                                // Also try case variations of collection name
                                var collectionVariations = new[] { "SHSStudents", "shsstudents", "Students", "students", "STUDENTS" };
                                foreach (var collName in collectionVariations)
                                {
                                    if (collections.Contains(collName))
                                    {
                                        var studentsCollection = testDb.GetCollection<BsonDocument>(collName);
                                        var studentCount = studentsCollection.CountDocuments(FilterDefinition<BsonDocument>.Empty);
                                        
                                foundDatabase = testDb;
                                foundDbName = dbName;
                                        // Update the collection reference to use the correct case
                                        _enrollmentStudents = foundDatabase.GetCollection<EnrollmentStudent>(collName);
                                break;
                            }
                                }
                                
                                if (foundDatabase != null) break;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
                
                if (foundDatabase == null)
                {
                    // Use the configured database name directly
                    foundDatabase = enrollmentClient.GetDatabase(enrollmentDatabaseName);
                    foundDbName = enrollmentDatabaseName;
                }
                
                _enrollmentDatabase = foundDatabase;
                
                // Verify connection
                _enrollmentDatabase.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
            }
            catch (Exception)
            {
                // Fallback to configured database name
                _enrollmentDatabase = enrollmentClient.GetDatabase(enrollmentDatabaseName);
            }

            _users = _database.GetCollection<User>("Users");
            _classes = _database.GetCollection<ClassItem>("Classes");
            _students = _database.GetCollection<StudentRecord>("Students");
            _uploadCollection = _database.GetCollection<UploadItem>("Uploads");
            _contentCollection = _database.GetCollection<ContentItem>("Contents");
            _joinRequestsCollection = _database.GetCollection<JoinRequest>("JoinRequests");
            _taskCollection = _database.GetCollection<TaskItem>("Tasks");
            _attendanceCollection = _database.GetCollection<StudentPortal.Models.AdminDb.AttendanceRecord>("AttendanceRecords");
            _attendanceCopyCollection = _database.GetCollection<BsonDocument>("AttendanceCopy");
            _submissionsCollection = _database.GetCollection<Submission>("Submissions");
            _taskCommentsCollection = _database.GetCollection<StudentPortal.Models.AdminTask.TaskCommentItem>("TaskComments");
            _antiCheatLogsCollection = _database.GetCollection<StudentPortal.Models.AdminDb.AntiCheatLog>("AntiCheatLogs");
            _assessmentUnlocksCollection = _database.GetCollection<StudentPortal.Models.AdminDb.AssessmentUnlock>("AssessmentUnlocks");
            _assessmentResultsCollection = _database.GetCollection<StudentPortal.Models.StudentDb.AssessmentResult>("AssessmentResults");
            _userNotifications = _database.GetCollection<StudentPortal.Models.StudentDb.UserNotification>("UserNotifications");
            
            // Initialize enrollment students collection - try different case variations
            try
            {
                var collectionNames = new[] { "SHSStudents", "shsstudents", "Students", "students", "STUDENTS" };
                bool collectionFound = false;
                
                foreach (var collName in collectionNames)
                {
            try
            {
                        var testCollection = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                        var count = testCollection.CountDocuments(FilterDefinition<BsonDocument>.Empty);
                        if (count >= 0) // Collection exists (even if empty)
                        {
                            _enrollmentStudents = _enrollmentDatabase.GetCollection<EnrollmentStudent>(collName);
                            collectionFound = true;
                            break;
                        }
            }
                    catch
                    {
                        continue;
                    }
                }
                
                if (!collectionFound)
                {
                    // Default to SHSStudents
                    _enrollmentStudents = _enrollmentDatabase.GetCollection<EnrollmentStudent>("SHSStudents");
                }
            }
            catch
            {
                _enrollmentStudents = _enrollmentDatabase.GetCollection<EnrollmentStudent>("SHSStudents");
            }

            // Professor database connection (enrollment system)
            _professorDatabase = _enrollmentDatabase;
            try
            {
                var collections = _professorDatabase.ListCollectionNames().ToList();
                var collectionVariations = new[] { "Professors", "professors", "Users", "users", "USERS" };
                string? foundCollectionName = null;
                foreach (var collName in collectionVariations)
                {
                    if (collections.Contains(collName))
                    {
                        foundCollectionName = collName;
                        break;
                    }
                }
                _professorCollectionName = foundCollectionName ?? "professors";
                _professors = _professorDatabase.GetCollection<Professor>(_professorCollectionName);
                _professorDatabase.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
                Console.WriteLine($"[MongoDbService] Enrollment professor connection verified. Using collection: {_professorCollectionName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MongoDbService] Exception connecting to enrollment system for professors: {ex.Message}");
                _professorCollectionName = "professors";
                _professors = _professorDatabase.GetCollection<Professor>(_professorCollectionName);
                Console.WriteLine($"[MongoDbService] Fallback: Using collection: {_professorCollectionName}");
            }
        }

        public async Task<List<StudentPortal.Models.StudentDb.UserNotification>> GetNotificationsByEmailAsync(string email, int limit = 100)
        {
            var filter = Builders<StudentPortal.Models.StudentDb.UserNotification>.Filter.And(
                Builders<StudentPortal.Models.StudentDb.UserNotification>.Filter.Eq(n => n.Email, email),
                Builders<StudentPortal.Models.StudentDb.UserNotification>.Filter.Eq(n => n.Deleted, false)
            );
            return await _userNotifications.Find(filter).SortByDescending(n => n.CreatedAt).Limit(limit).ToListAsync();
        }

            public async Task CleanupOldNotificationsAsync(string email, DateTime olderThanUtc)
        {
            var filter = Builders<StudentPortal.Models.StudentDb.UserNotification>.Filter.And(
                Builders<StudentPortal.Models.StudentDb.UserNotification>.Filter.Eq(n => n.Email, email),
                Builders<StudentPortal.Models.StudentDb.UserNotification>.Filter.Lt(n => n.CreatedAt, olderThanUtc)
            );
            var update = Builders<StudentPortal.Models.StudentDb.UserNotification>.Update.Set(n => n.Deleted, true);
            await _userNotifications.UpdateManyAsync(filter, update);
        }

        public async Task AddNotificationAsync(StudentPortal.Models.StudentDb.UserNotification n)
        {
            // Only dedupe bursts that happen within the last minute for the same email/type/text/code.
            // Longer-term dedupe was preventing fresh notifications from being created.
            var now = DateTime.UtcNow;
            var since = now.AddMinutes(-1);
            var filter = Builders<StudentPortal.Models.StudentDb.UserNotification>.Filter.And(
                Builders<StudentPortal.Models.StudentDb.UserNotification>.Filter.Eq(x => x.Email, n.Email),
                Builders<StudentPortal.Models.StudentDb.UserNotification>.Filter.Eq(x => x.Type, n.Type),
                Builders<StudentPortal.Models.StudentDb.UserNotification>.Filter.Eq(x => x.Text, n.Text),
                Builders<StudentPortal.Models.StudentDb.UserNotification>.Filter.Eq(x => x.Code, n.Code),
                Builders<StudentPortal.Models.StudentDb.UserNotification>.Filter.Gte(x => x.CreatedAt, since),
                Builders<StudentPortal.Models.StudentDb.UserNotification>.Filter.Eq(x => x.Deleted, false)
            );

            var existing = await _userNotifications.Find(filter).FirstOrDefaultAsync();
            if (existing != null) return;

            if (n.CreatedAt == default) n.CreatedAt = now;
            await _userNotifications.InsertOneAsync(n);
        }

        public async Task MarkNotificationReadAsync(string id)
        {
            if (!ObjectId.TryParse(id, out _)) return;
            var update = Builders<StudentPortal.Models.StudentDb.UserNotification>.Update.Set(x => x.Read, true);
            await _userNotifications.UpdateOneAsync(n => n.Id == id, update);
        }

        public async Task DeleteNotificationAsync(string id)
        {
            if (!ObjectId.TryParse(id, out _)) return;
            var update = Builders<StudentPortal.Models.StudentDb.UserNotification>.Update.Set(x => x.Deleted, true);
            await _userNotifications.UpdateOneAsync(n => n.Id == id, update);
        }

        public async Task<DateTime?> GetLatestNotificationCreatedAtAsync(string email)
        {
            var filter = Builders<StudentPortal.Models.StudentDb.UserNotification>.Filter.Eq(n => n.Email, email);
            var projection = Builders<StudentPortal.Models.StudentDb.UserNotification>.Projection.Include(n => n.CreatedAt);
            var latest = await _userNotifications
                .Find(filter)
                .Project<StudentPortal.Models.StudentDb.UserNotification>(projection)
                .SortByDescending(n => n.CreatedAt)
                .FirstOrDefaultAsync();
            return latest?.CreatedAt;
        }

        // ---------------- PROFESSORS ----------------
        public async Task<Professor?> GetProfessorByEmailAsync(string email)
        {
            if (string.IsNullOrEmpty(email)) return null;
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var originalEmail = email.Trim();
            
            Console.WriteLine($"[GetProfessorByEmailAsync] Searching for professor with email: {originalEmail}");
            Console.WriteLine($"[GetProfessorByEmailAsync] Using collection: {_professorCollectionName}");
            
            try
            {
                // Use BsonDocument collection directly for more reliable querying
                var bsonCollection = _professorDatabase.GetCollection<BsonDocument>(_professorCollectionName);
                
                // First, let's check if the collection exists and has documents
                var documentCount = await bsonCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
                Console.WriteLine($"[GetProfessorByEmailAsync] Collection '{_professorCollectionName}' has {documentCount} documents");
                
                // Try multiple field name variations: "email" (actual DB field) first, then "Email" (legacy)
                var emailFieldVariations = new[] { "email", "Email" };
                
                foreach (var emailField in emailFieldVariations)
                {
                    // Try case-insensitive regex search first (most reliable)
                    var bsonFilter = Builders<BsonDocument>.Filter.Regex(emailField, new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(normalizedEmail)}$", "i"));
                    var bsonProfessor = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                    
                    if (bsonProfessor != null)
                    {
                        try
                        {
                            // Convert BsonDocument to Professor
                            var professor = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<Professor>(bsonProfessor);
                            
                            // Ensure email is populated from any field variation
                            if (string.IsNullOrEmpty(professor.Email) && string.IsNullOrEmpty(professor.EmailLegacy))
                            {
                                // Try to get email from BsonDocument directly
                                if (bsonProfessor.Contains("email"))
                                    professor.Email = bsonProfessor["email"].AsString;
                                else if (bsonProfessor.Contains("Email"))
                                    professor.EmailLegacy = bsonProfessor["Email"].AsString;
                            }
                            
                            // Ensure passwordHash is populated
                            if (string.IsNullOrEmpty(professor.PasswordHash) && string.IsNullOrEmpty(professor.PasswordHashLegacy) && string.IsNullOrEmpty(professor.Password))
                            {
                                if (bsonProfessor.Contains("passwordHash"))
                                    professor.PasswordHash = bsonProfessor["passwordHash"].AsString;
                                else if (bsonProfessor.Contains("PasswordHash"))
                                    professor.PasswordHashLegacy = bsonProfessor["PasswordHash"].AsString;
                                else if (bsonProfessor.Contains("Password"))
                                    professor.Password = bsonProfessor["Password"].AsString;
                            }
                            
                            Console.WriteLine($"[GetProfessorByEmailAsync] Found professor: Email={professor.GetEmail()}, HasPasswordHash={!string.IsNullOrEmpty(professor.GetPasswordHash())}, FullName={professor.GetFullName()}");
                            return professor;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[GetProfessorByEmailAsync] Error deserializing professor: {ex.Message}");
                            // Continue to try other methods
                        }
                    }
                    
                    // Fallback: Try exact match with original email (case-sensitive)
                    bsonFilter = Builders<BsonDocument>.Filter.Eq(emailField, originalEmail);
                    bsonProfessor = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                    
                    if (bsonProfessor != null)
                    {
                        try
                        {
                            var professor = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<Professor>(bsonProfessor);
                            
                            // Ensure email is populated
                            if (string.IsNullOrEmpty(professor.Email) && string.IsNullOrEmpty(professor.EmailLegacy))
                            {
                                if (bsonProfessor.Contains("email"))
                                    professor.Email = bsonProfessor["email"].AsString;
                                else if (bsonProfessor.Contains("Email"))
                                    professor.EmailLegacy = bsonProfessor["Email"].AsString;
                            }
                            
                            // Ensure passwordHash is populated
                            if (string.IsNullOrEmpty(professor.PasswordHash) && string.IsNullOrEmpty(professor.PasswordHashLegacy) && string.IsNullOrEmpty(professor.Password))
                            {
                                if (bsonProfessor.Contains("passwordHash"))
                                    professor.PasswordHash = bsonProfessor["passwordHash"].AsString;
                                else if (bsonProfessor.Contains("PasswordHash"))
                                    professor.PasswordHashLegacy = bsonProfessor["PasswordHash"].AsString;
                            }
                            
                            Console.WriteLine($"[GetProfessorByEmailAsync] Found professor (exact match): Email={professor.GetEmail()}");
                            return professor;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[GetProfessorByEmailAsync] Error deserializing professor (exact match): {ex.Message}");
                        }
                    }
                    
                    // Fallback: Try normalized lowercase
                    bsonFilter = Builders<BsonDocument>.Filter.Eq(emailField, normalizedEmail);
                    bsonProfessor = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                    
                    if (bsonProfessor != null)
                    {
                        try
                        {
                            var professor = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<Professor>(bsonProfessor);
                            
                            // Ensure email is populated
                            if (string.IsNullOrEmpty(professor.Email) && string.IsNullOrEmpty(professor.EmailLegacy))
                            {
                                if (bsonProfessor.Contains("email"))
                                    professor.Email = bsonProfessor["email"].AsString;
                                else if (bsonProfessor.Contains("Email"))
                                    professor.EmailLegacy = bsonProfessor["Email"].AsString;
                            }
                            
                            // Ensure passwordHash is populated
                            if (string.IsNullOrEmpty(professor.PasswordHash) && string.IsNullOrEmpty(professor.PasswordHashLegacy) && string.IsNullOrEmpty(professor.Password))
                            {
                                if (bsonProfessor.Contains("passwordHash"))
                                    professor.PasswordHash = bsonProfessor["passwordHash"].AsString;
                                else if (bsonProfessor.Contains("PasswordHash"))
                                    professor.PasswordHashLegacy = bsonProfessor["PasswordHash"].AsString;
                            }
                            
                            Console.WriteLine($"[GetProfessorByEmailAsync] Found professor (normalized): Email={professor.GetEmail()}");
                            return professor;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[GetProfessorByEmailAsync] Error deserializing professor (normalized): {ex.Message}");
                        }
                    }
                }
                
                Console.WriteLine($"[GetProfessorByEmailAsync] Professor not found for email: {originalEmail} in collection: {_professorCollectionName}");

                // Fallback: try to resolve from StudentDB \"Teachers\" collection
                try
                {
                    var fromTeachers = await GetProfessorFromTeachersByEmailAsync(originalEmail);
                    if (fromTeachers != null)
                    {
                        Console.WriteLine($"[GetProfessorByEmailAsync] Resolved professor from StudentDB Teachers for email: {originalEmail}");
                        return fromTeachers;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GetProfessorByEmailAsync] Error resolving from Teachers collection: {ex.Message}");
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetProfessorByEmailAsync] Exception searching for professor: {ex.Message}");
                Console.WriteLine($"[GetProfessorByEmailAsync] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private async Task<Professor?> GetProfessorFromTeachersByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;

            var normalized = email.Trim().ToLowerInvariant();
            var collectionNames = new[] { "Teachers", "teachers", "TEACHERS" };

            foreach (var name in collectionNames)
            {
                try
                {
                    var coll = _database.GetCollection<BsonDocument>(name);

                    // Match Email/email in a case-insensitive way
                    var regex = new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(normalized)}$", "i");
                    var filter = Builders<BsonDocument>.Filter.Or(
                        Builders<BsonDocument>.Filter.Regex("Email", regex),
                        Builders<BsonDocument>.Filter.Regex("email", regex)
                    );

                    var doc = await coll.Find(filter).FirstOrDefaultAsync();
                    if (doc == null) continue;

                    var professor = new Professor();

                    // Email
                    if (doc.Contains("email") && doc["email"].IsString)
                        professor.Email = doc["email"].AsString;
                    else if (doc.Contains("Email") && doc["Email"].IsString)
                        professor.EmailLegacy = doc["Email"].AsString;
                    else
                        professor.Email = email.Trim();

                    // Password / hash
                    //
                    // IMPORTANT:
                    // The StudentDB "Teachers" collection may store an ASP.NET Identity PasswordHasher hash.
                    // StudentPortal login verifies professors using BCrypt. If we propagate the Teachers hash here,
                    // the login will always fail with "wrong password" even if a correct BCrypt hash exists in Users.
                    //
                    // Prefer the portal Users.Password (BCrypt) when available; otherwise, fall back to whatever
                    // the Teachers document contains.
                    try
                    {
                        var portalUser = await GetUserByEmailAsync(professor.GetEmail());
                        if (portalUser != null && !string.IsNullOrWhiteSpace(portalUser.Password))
                        {
                            professor.PasswordHash = portalUser.Password;
                        }
                    }
                    catch
                    {
                        // Best-effort only; proceed with legacy fields below.
                    }

                    if (string.IsNullOrWhiteSpace(professor.GetPasswordHash()))
                    {
                        if (doc.Contains("passwordHash") && doc["passwordHash"].IsString)
                            professor.PasswordHash = doc["passwordHash"].AsString;
                        else if (doc.Contains("PasswordHash") && doc["PasswordHash"].IsString)
                            professor.PasswordHashLegacy = doc["PasswordHash"].AsString;
                        else if (doc.Contains("Password") && doc["Password"].IsString)
                            professor.Password = doc["Password"].AsString;
                        else if (doc.Contains("password") && doc["password"].IsString)
                            professor.Password = doc["password"].AsString;
                    }

                    // Name fields
                    string given = string.Empty;
                    string middle = string.Empty;
                    string last = string.Empty;

                    if (doc.Contains("GivenName") && doc["GivenName"].IsString)
                        given = doc["GivenName"].AsString;
                    else if (doc.Contains("givenName") && doc["givenName"].IsString)
                        given = doc["givenName"].AsString;
                    else if (doc.Contains("FirstName") && doc["FirstName"].IsString)
                        given = doc["FirstName"].AsString;
                    else if (doc.Contains("firstName") && doc["firstName"].IsString)
                        given = doc["firstName"].AsString;

                    if (doc.Contains("MiddleName") && doc["MiddleName"].IsString)
                        middle = doc["MiddleName"].AsString;
                    else if (doc.Contains("middleName") && doc["middleName"].IsString)
                        middle = doc["middleName"].AsString;

                    if (doc.Contains("LastName") && doc["LastName"].IsString)
                        last = doc["LastName"].AsString;
                    else if (doc.Contains("lastName") && doc["lastName"].IsString)
                        last = doc["lastName"].AsString;

                    professor.GivenName = given ?? string.Empty;
                    professor.MiddleName = string.IsNullOrWhiteSpace(middle) ? null : middle;
                    professor.LastName = last ?? string.Empty;

                    // FullName fallback
                    if (doc.Contains("FullName") && doc["FullName"].IsString)
                        professor.FullName = doc["FullName"].AsString;
                    else if (doc.Contains("fullName") && doc["fullName"].IsString)
                        professor.FullName = doc["fullName"].AsString;

                    // Role from Teachers if present, else default to \"Professor\"
                    if (doc.Contains("Role") && doc["Role"].IsString)
                        professor.Role = doc["Role"].AsString;
                    else if (doc.Contains("role") && doc["role"].IsString)
                        professor.Role = doc["role"].AsString;
                    else
                        professor.Role = "Professor";

                    return professor;
                }
                catch
                {
                    continue;
                }
            }

            return null;
        }

        // Try to derive the professor's department from various possible fields
        public async Task<string> GetProfessorDepartmentByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return string.Empty;
            try
            {
                var normalizedEmail = email.Trim().ToLowerInvariant();
                var bsonCollection = _professorDatabase.GetCollection<BsonDocument>(_professorCollectionName);
                var regex = new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(normalizedEmail)}$", "i");
                var filter = Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Regex("email", regex),
                    Builders<BsonDocument>.Filter.Regex("Email", regex)
                );
                var doc = await bsonCollection.Find(filter).FirstOrDefaultAsync();
                if (doc == null) return string.Empty;

                // Common department field variants
                var keys = new[]
                {
                    "Department","department","Dept","dept","DepartmentName","departmentName","DEPARTMENT"
                };
                foreach (var k in keys)
                {
                    if (doc.Contains(k) && !doc[k].IsBsonNull)
                    {
                        var val = doc[k].ToString();
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
                // Programs array as department source
                if (doc.Contains("programs") && doc["programs"].IsBsonArray)
                {
                    var arr = doc["programs"].AsBsonArray;
                    foreach (var v in arr)
                    {
                        var val = v?.ToString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
                if (doc.Contains("Programs") && doc["Programs"].IsBsonArray)
                {
                    var arr = doc["Programs"].AsBsonArray;
                    foreach (var v in arr)
                    {
                        var val = v?.ToString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        // ---------------- ENROLLMENT STUDENTS ----------------
        public async Task<EnrollmentStudent?> GetEnrollmentStudentByEmailAsync(string email)
        {
            if (string.IsNullOrEmpty(email)) return null;
            var normalizedEmail = email.Trim().ToLowerInvariant();
            
            try
            {
                // Use BsonDocument collection directly for more reliable querying
                var collectionName = _enrollmentStudents?.CollectionNamespace.CollectionName ?? "SHSStudents";
                var bsonCollection = _enrollmentDatabase.GetCollection<BsonDocument>(collectionName);
                
                // Try case-insensitive regex search on common email/username fields
                var regex = new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(normalizedEmail)}$", "i");
                var bsonFilter = Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Regex("Email", regex),
                    Builders<BsonDocument>.Filter.Regex("email", regex),
                    Builders<BsonDocument>.Filter.Regex("Student.Email", regex),
                    Builders<BsonDocument>.Filter.Regex("Student.email", regex),
                    Builders<BsonDocument>.Filter.Regex("Username", regex),
                    Builders<BsonDocument>.Filter.Regex("username", regex)
                );
                var bsonStudent = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                
                if (bsonStudent != null)
                {
                    // Convert BsonDocument to EnrollmentStudent
                    var student = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<EnrollmentStudent>(bsonStudent);
                    return student;
                }
                
                // Fallback: Try exact match with original email (case-sensitive)
                var emailTrim = email.Trim();
                bsonFilter = Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Eq("Email", emailTrim),
                    Builders<BsonDocument>.Filter.Eq("email", emailTrim),
                    Builders<BsonDocument>.Filter.Eq("Student.Email", emailTrim),
                    Builders<BsonDocument>.Filter.Eq("Student.email", emailTrim),
                    Builders<BsonDocument>.Filter.Eq("Username", emailTrim),
                    Builders<BsonDocument>.Filter.Eq("username", emailTrim)
                );
                bsonStudent = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                
                if (bsonStudent != null)
                {
                    return MongoDB.Bson.Serialization.BsonSerializer.Deserialize<EnrollmentStudent>(bsonStudent);
                }
                
                // Fallback: Try normalized lowercase
                bsonFilter = Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Eq("Email", normalizedEmail),
                    Builders<BsonDocument>.Filter.Eq("email", normalizedEmail),
                    Builders<BsonDocument>.Filter.Eq("Student.Email", normalizedEmail),
                    Builders<BsonDocument>.Filter.Eq("Student.email", normalizedEmail),
                    Builders<BsonDocument>.Filter.Eq("Username", normalizedEmail),
                    Builders<BsonDocument>.Filter.Eq("username", normalizedEmail)
                );
                bsonStudent = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                
                if (bsonStudent != null)
                {
                    return MongoDB.Bson.Serialization.BsonSerializer.Deserialize<EnrollmentStudent>(bsonStudent);
                }
                
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Get student ExtraFields from enrollment students collection or enrollmentRequests collection
        public async Task<Dictionary<string, string>?> GetStudentExtraFieldsByEmailAsync(string email)
        {
            if (string.IsNullOrEmpty(email)) return null;
            var normalizedEmail = email.Trim().ToLowerInvariant();
            
            try
            {
                // First, try the main enrollment students collection
                var collectionName = _enrollmentStudents?.CollectionNamespace.CollectionName ?? "SHSStudents";
                var bsonCollection = _enrollmentDatabase.GetCollection<BsonDocument>(collectionName);
                
                // Try case-insensitive regex search
                var emailRegex = new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(normalizedEmail)}$", "i");
                var bsonFilter = Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Regex("Email", emailRegex),
                    Builders<BsonDocument>.Filter.Regex("email", emailRegex),
                    Builders<BsonDocument>.Filter.Regex("Student.Email", emailRegex),
                    Builders<BsonDocument>.Filter.Regex("Student.email", emailRegex),
                    Builders<BsonDocument>.Filter.Regex("Username", emailRegex),
                    Builders<BsonDocument>.Filter.Regex("username", emailRegex)
                );
                var bsonStudent = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                
                if (bsonStudent == null)
                {
                    // Fallback: Try exact match
                    var exact = email.Trim();
                    bsonFilter = Builders<BsonDocument>.Filter.Or(
                        Builders<BsonDocument>.Filter.Eq("Email", exact),
                        Builders<BsonDocument>.Filter.Eq("email", exact),
                        Builders<BsonDocument>.Filter.Eq("Student.Email", exact),
                        Builders<BsonDocument>.Filter.Eq("Student.email", exact),
                        Builders<BsonDocument>.Filter.Eq("Username", exact),
                        Builders<BsonDocument>.Filter.Eq("username", exact)
                    );
                    bsonStudent = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                }
                
                if (bsonStudent == null)
                {
                    // Fallback: Try normalized lowercase
                    bsonFilter = Builders<BsonDocument>.Filter.Or(
                        Builders<BsonDocument>.Filter.Eq("Email", normalizedEmail),
                        Builders<BsonDocument>.Filter.Eq("email", normalizedEmail),
                        Builders<BsonDocument>.Filter.Eq("Student.Email", normalizedEmail),
                        Builders<BsonDocument>.Filter.Eq("Student.email", normalizedEmail),
                        Builders<BsonDocument>.Filter.Eq("Username", normalizedEmail),
                        Builders<BsonDocument>.Filter.Eq("username", normalizedEmail)
                    );
                    bsonStudent = await bsonCollection.Find(bsonFilter).FirstOrDefaultAsync();
                }
                
                if (bsonStudent != null && bsonStudent.Contains("ExtraFields"))
                {
                    var extraFields = bsonStudent["ExtraFields"].AsBsonDocument;
                    var result = new Dictionary<string, string>();
                    foreach (var field in extraFields)
                    {
                        result[field.Name] = field.Value?.ToString() ?? string.Empty;
                    }
                    return result;
                }

                // Some enrollment DBs store name fields at the root (or nested) instead of ExtraFields.
                if (bsonStudent != null)
                {
                    var result = new Dictionary<string, string>();
                    string GetStr(BsonDocument d, string key)
                        => d.TryGetValue(key, out var v) && v.BsonType != BsonType.Null ? v.ToString() : string.Empty;

                    // Root variants
                    var fn = GetStr(bsonStudent, "FirstName");
                    var mn = GetStr(bsonStudent, "MiddleName");
                    var ln = GetStr(bsonStudent, "LastName");
                    var full = GetStr(bsonStudent, "FullName");

                    // Nested variants: Student: { FirstName, ... }
                    if (bsonStudent.TryGetValue("Student", out var stuVal) && stuVal.IsBsonDocument)
                    {
                        var stu = stuVal.AsBsonDocument;
                        if (string.IsNullOrWhiteSpace(fn)) fn = GetStr(stu, "FirstName");
                        if (string.IsNullOrWhiteSpace(mn)) mn = GetStr(stu, "MiddleName");
                        if (string.IsNullOrWhiteSpace(ln)) ln = GetStr(stu, "LastName");
                        if (string.IsNullOrWhiteSpace(full)) full = GetStr(stu, "FullName");
                    }

                    // Also accept lowercase keys
                    if (string.IsNullOrWhiteSpace(fn)) fn = GetStr(bsonStudent, "firstName");
                    if (string.IsNullOrWhiteSpace(mn)) mn = GetStr(bsonStudent, "middleName");
                    if (string.IsNullOrWhiteSpace(ln)) ln = GetStr(bsonStudent, "lastName");
                    if (string.IsNullOrWhiteSpace(full)) full = GetStr(bsonStudent, "fullName");

                    if (!string.IsNullOrWhiteSpace(fn)) result["Student.FirstName"] = fn;
                    if (!string.IsNullOrWhiteSpace(mn)) result["Student.MiddleName"] = mn;
                    if (!string.IsNullOrWhiteSpace(ln)) result["Student.LastName"] = ln;
                    if (!string.IsNullOrWhiteSpace(full)) result["Student.FullName"] = full;

                    if (result.Count > 0) return result;
                }
                
                // If not found in "students" collection, try "enrollmentRequests" collection
                var enrollmentCollection = _enrollmentDatabase.GetCollection<BsonDocument>("enrollmentRequests");
                
                // Try case-insensitive regex search
                bsonFilter = Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Regex("Email", emailRegex),
                    Builders<BsonDocument>.Filter.Regex("email", emailRegex),
                    Builders<BsonDocument>.Filter.Regex("Student.Email", emailRegex),
                    Builders<BsonDocument>.Filter.Regex("Student.email", emailRegex),
                    Builders<BsonDocument>.Filter.Regex("Username", emailRegex),
                    Builders<BsonDocument>.Filter.Regex("username", emailRegex)
                );
                var bsonEnrollment = await enrollmentCollection.Find(bsonFilter).FirstOrDefaultAsync();
                
                if (bsonEnrollment == null)
                {
                    // Fallback: Try exact match
                    var exact = email.Trim();
                    bsonFilter = Builders<BsonDocument>.Filter.Or(
                        Builders<BsonDocument>.Filter.Eq("Email", exact),
                        Builders<BsonDocument>.Filter.Eq("email", exact),
                        Builders<BsonDocument>.Filter.Eq("Student.Email", exact),
                        Builders<BsonDocument>.Filter.Eq("Student.email", exact),
                        Builders<BsonDocument>.Filter.Eq("Username", exact),
                        Builders<BsonDocument>.Filter.Eq("username", exact)
                    );
                    bsonEnrollment = await enrollmentCollection.Find(bsonFilter).FirstOrDefaultAsync();
                }
                
                if (bsonEnrollment == null)
                {
                    // Fallback: Try normalized lowercase
                    bsonFilter = Builders<BsonDocument>.Filter.Or(
                        Builders<BsonDocument>.Filter.Eq("Email", normalizedEmail),
                        Builders<BsonDocument>.Filter.Eq("email", normalizedEmail),
                        Builders<BsonDocument>.Filter.Eq("Student.Email", normalizedEmail),
                        Builders<BsonDocument>.Filter.Eq("Student.email", normalizedEmail),
                        Builders<BsonDocument>.Filter.Eq("Username", normalizedEmail),
                        Builders<BsonDocument>.Filter.Eq("username", normalizedEmail)
                    );
                    bsonEnrollment = await enrollmentCollection.Find(bsonFilter).FirstOrDefaultAsync();
                }
                
                if (bsonEnrollment != null && bsonEnrollment.Contains("ExtraFields"))
                {
                    var extraFields = bsonEnrollment["ExtraFields"].AsBsonDocument;
                    var result = new Dictionary<string, string>();
                    foreach (var field in extraFields)
                    {
                        result[field.Name] = field.Value?.ToString() ?? string.Empty;
                    }
                    return result;
                }

                // Same fallback extraction for enrollmentRequests if no ExtraFields exist.
                if (bsonEnrollment != null)
                {
                    var result = new Dictionary<string, string>();
                    string GetStr(BsonDocument d, string key)
                        => d.TryGetValue(key, out var v) && v.BsonType != BsonType.Null ? v.ToString() : string.Empty;

                    var fn = GetStr(bsonEnrollment, "FirstName");
                    var mn = GetStr(bsonEnrollment, "MiddleName");
                    var ln = GetStr(bsonEnrollment, "LastName");
                    var full = GetStr(bsonEnrollment, "FullName");

                    if (bsonEnrollment.TryGetValue("Student", out var stuVal) && stuVal.IsBsonDocument)
                    {
                        var stu = stuVal.AsBsonDocument;
                        if (string.IsNullOrWhiteSpace(fn)) fn = GetStr(stu, "FirstName");
                        if (string.IsNullOrWhiteSpace(mn)) mn = GetStr(stu, "MiddleName");
                        if (string.IsNullOrWhiteSpace(ln)) ln = GetStr(stu, "LastName");
                        if (string.IsNullOrWhiteSpace(full)) full = GetStr(stu, "FullName");
                    }

                    if (string.IsNullOrWhiteSpace(fn)) fn = GetStr(bsonEnrollment, "firstName");
                    if (string.IsNullOrWhiteSpace(mn)) mn = GetStr(bsonEnrollment, "middleName");
                    if (string.IsNullOrWhiteSpace(ln)) ln = GetStr(bsonEnrollment, "lastName");
                    if (string.IsNullOrWhiteSpace(full)) full = GetStr(bsonEnrollment, "fullName");

                    if (!string.IsNullOrWhiteSpace(fn)) result["Student.FirstName"] = fn;
                    if (!string.IsNullOrWhiteSpace(mn)) result["Student.MiddleName"] = mn;
                    if (!string.IsNullOrWhiteSpace(ln)) result["Student.LastName"] = ln;
                    if (!string.IsNullOrWhiteSpace(full)) result["Student.FullName"] = full;

                    if (result.Count > 0) return result;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetStudentExtraFieldsByEmailAsync] Error: {ex.Message}");
                return null;
            }
        }

        // ---------------- USERS ----------------
        public async Task<User?> GetUserByEmailAsync(string email)
        {
            if (string.IsNullOrEmpty(email)) return null;
            var normalizedEmail = email.Trim().ToLowerInvariant();
            
            // Try exact match first
            var user = await _users.Find(u => u.Email == email.Trim()).FirstOrDefaultAsync();
            
            // If not found, try normalized lowercase
            if (user == null)
            {
                user = await _users.Find(u => u.Email == normalizedEmail).FirstOrDefaultAsync();
            }
            
            // If still not found, try case-insensitive regex
            if (user == null)
            {
                var filter = Builders<User>.Filter.Regex(
                    u => u.Email,
                    new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(normalizedEmail)}$", "i"));
                user = await _users.Find(filter).FirstOrDefaultAsync();
            }
            
            return user;
        }

        private static User MapUserFromBson(BsonDocument doc)
        {
            string GetString(string k)
            {
                return doc.TryGetValue(k, out var v) && v.BsonType != BsonType.Null ? v.ToString() : string.Empty;
            }
            int? GetInt(string k)
            {
                return doc.TryGetValue(k, out var v) && v.IsInt32 ? (int?)v.AsInt32 : (doc.TryGetValue(k, out var v2) && v2.IsInt64 ? (int?)(int)v2.AsInt64 : null);
            }
            DateTime? GetDate(string k)
            {
                return doc.TryGetValue(k, out var v) && v.IsValidDateTime ? (DateTime?)v.ToUniversalTime() : null;
            }
            bool GetBool(string k)
            {
                return doc.TryGetValue(k, out var v) && v.IsBoolean && v.AsBoolean;
            }
            List<string> GetStringList(string k)
            {
                if (doc.TryGetValue(k, out var v) && v.IsBsonArray)
                {
                    return v.AsBsonArray.Select(x => x.ToString()).Where(s => !string.IsNullOrEmpty(s)).ToList();
                }
                return new List<string>();
            }

            var id = doc.TryGetValue("_id", out var idVal) ? idVal.ToString() : null;

            return new User
            {
                Id = id,
                Email = string.IsNullOrEmpty(GetString("Email")) ? GetString("email") : GetString("Email"),
                Password = GetString("Password"),
                OTP = GetString("OTP"),
                IsVerified = GetBool("IsVerified"),
                FullName = GetString("FullName"),
                LastName = GetString("LastName"),
                FirstName = GetString("FirstName"),
                MiddleName = GetString("MiddleName"),
                Role = string.IsNullOrEmpty(GetString("Role")) ? "Student" : GetString("Role"),
                FailedLoginAttempts = GetInt("FailedLoginAttempts"),
                LockoutEndTime = GetDate("LockoutEndTime"),
                JoinedClasses = GetStringList("JoinedClasses")
            };
        }

        public async Task<User> CreateUserFromEnrollmentStudentAsync(EnrollmentStudent enrollmentStudent)
        {
            // Get ExtraFields from enrollment students collection to get name fields
            var extraFields = await GetStudentExtraFieldsByEmailAsync(enrollmentStudent.Email);
            
            string lastName = string.Empty;
            string firstName = string.Empty;
            string middleName = string.Empty;
            string fullName = string.Empty;
            
            if (extraFields != null)
            {
                static string GetAny(Dictionary<string, string> dict, params string[] keys)
                {
                    foreach (var k in keys)
                    {
                        if (dict.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                            return v.Trim();
                    }
                    return string.Empty;
                }

                // Support multiple field conventions across Enrollment/ExtraFields payloads
                firstName = GetAny(extraFields,
                    "Student.FirstName", "FirstName", "firstName",
                    "Student.GivenName", "GivenName", "givenName");

                middleName = GetAny(extraFields,
                    "Student.MiddleName", "MiddleName", "middleName",
                    "Student.MI", "MI", "mi");

                lastName = GetAny(extraFields,
                    "Student.LastName", "LastName", "lastName",
                    "Student.Surname", "Surname", "surname",
                    "Student.FamilyName", "FamilyName", "familyName");
                
                // Build FullName from parts
                var nameParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(firstName)) nameParts.Add(firstName);
                if (!string.IsNullOrWhiteSpace(middleName)) nameParts.Add(middleName);
                if (!string.IsNullOrWhiteSpace(lastName)) nameParts.Add(lastName);
                fullName = string.Join(" ", nameParts);
            }
            else
            {
                // Fallback: EnrollmentSystem SHSStudents stores name at the root.
                lastName = enrollmentStudent.LastName ?? string.Empty;
                firstName = enrollmentStudent.FirstName ?? string.Empty;
                middleName = enrollmentStudent.MiddleName ?? string.Empty;

                var nameParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(firstName)) nameParts.Add(firstName);
                if (!string.IsNullOrWhiteSpace(middleName) && !string.Equals(middleName.Trim(), "NA", System.StringComparison.OrdinalIgnoreCase))
                    nameParts.Add(middleName);
                if (!string.IsNullOrWhiteSpace(lastName)) nameParts.Add(lastName);
                fullName = string.Join(" ", nameParts);
            }

            // If ExtraFields lookup succeeded but didn't contain name keys, still fall back to SHSStudents root fields.
            if (string.IsNullOrWhiteSpace(fullName) && (enrollmentStudent.FirstName != null || enrollmentStudent.LastName != null))
            {
                lastName = string.IsNullOrWhiteSpace(lastName) ? (enrollmentStudent.LastName ?? string.Empty) : lastName;
                firstName = string.IsNullOrWhiteSpace(firstName) ? (enrollmentStudent.FirstName ?? string.Empty) : firstName;
                middleName = string.IsNullOrWhiteSpace(middleName) ? (enrollmentStudent.MiddleName ?? string.Empty) : middleName;

                var nameParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(firstName)) nameParts.Add(firstName);
                if (!string.IsNullOrWhiteSpace(middleName) && !string.Equals(middleName.Trim(), "NA", System.StringComparison.OrdinalIgnoreCase))
                    nameParts.Add(middleName);
                if (!string.IsNullOrWhiteSpace(lastName)) nameParts.Add(lastName);
                fullName = string.Join(" ", nameParts);
            }
            
            // Check if user already exists (case-insensitive)
            var normalizedEmail = enrollmentStudent.Email.Trim().ToLowerInvariant();
            var existingUser = await GetUserByEmailAsync(enrollmentStudent.Email);
            
            if (existingUser != null)
            {
                // User already exists, always sync password hash and ensure correct role/status
                var filter = Builders<User>.Filter.Eq(u => u.Email, existingUser.Email);
                var update = Builders<User>.Update
                    .Set(u => u.Password, enrollmentStudent.PasswordHash) // Always sync password hash from enrollment
                    .Set(u => u.IsVerified, true) // Ensure verified status
                    .Set(u => u.Role, "Student") // Ensure role is Student
                    .Set(u => u.EnrollmentId, enrollmentStudent.Id)
                    .Set(u => u.EnrollmentUsername, enrollmentStudent.Username);
                
                // Update name fields if available
                if (!string.IsNullOrWhiteSpace(lastName))
                    update = update.Set(u => u.LastName, lastName);
                if (!string.IsNullOrWhiteSpace(firstName))
                    update = update.Set(u => u.FirstName, firstName);
                if (!string.IsNullOrWhiteSpace(middleName))
                    update = update.Set(u => u.MiddleName, middleName);
                if (!string.IsNullOrWhiteSpace(fullName))
                    update = update.Set(u => u.FullName, fullName);
                
                await _users.UpdateOneAsync(filter, update);
                
                // Update local object for return
                existingUser.Password = enrollmentStudent.PasswordHash;
                existingUser.IsVerified = true;
                existingUser.Role = "Student";
                existingUser.EnrollmentId = enrollmentStudent.Id ?? string.Empty;
                existingUser.EnrollmentUsername = enrollmentStudent.Username ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(lastName)) existingUser.LastName = lastName;
                if (!string.IsNullOrWhiteSpace(firstName)) existingUser.FirstName = firstName;
                if (!string.IsNullOrWhiteSpace(middleName)) existingUser.MiddleName = middleName;
                if (!string.IsNullOrWhiteSpace(fullName)) existingUser.FullName = fullName;
                
                return existingUser;
            }

            // Create new user from enrollment student data
            var newUser = new User
            {
                Email = enrollmentStudent.Email,
                Password = enrollmentStudent.PasswordHash, // Use the same password hash
                OTP = "",
                IsVerified = true, // Enrollment students are already verified
                LastName = lastName ?? string.Empty,
                FirstName = firstName ?? string.Empty,
                MiddleName = middleName ?? string.Empty,
                FullName = fullName,
                Role = "Student",
                FailedLoginAttempts = 0,
                LockoutEndTime = null,
                JoinedClasses = new List<string>(),
                EnrollmentId = enrollmentStudent.Id ?? string.Empty,
                EnrollmentUsername = enrollmentStudent.Username ?? string.Empty
            };

            await _users.InsertOneAsync(newUser);
            return newUser;
        }

        public async Task<bool> LinkEnrollmentToUserByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var enrollment = await GetEnrollmentStudentByEmailAsync(email);
            if (enrollment == null) return false;
            var filter = Builders<User>.Filter.Eq(u => u.Email, email);
            var update = Builders<User>.Update
                .Set(u => u.EnrollmentId, enrollment.Id)
                .Set(u => u.EnrollmentUsername, enrollment.Username)
                .Set(u => u.IsVerified, true)
                .Set(u => u.Role, "Student");
            var result = await _users.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<User> CreateUserFromProfessorAsync(Professor professor, string passwordHash)
        {
            // Get professor email and name using helper methods
            var professorEmail = professor.GetEmail();
            var professorName = professor.GetFullName();
            
            if (string.IsNullOrEmpty(professorEmail))
            {
                throw new ArgumentException("Professor email is required.");
            }

            // Check if user already exists (case-insensitive)
            var existingUser = await GetUserByEmailAsync(professorEmail);
            
            if (existingUser != null)
            {
                // User already exists, always sync password hash and ensure correct role/status
                var filter = Builders<User>.Filter.Eq(u => u.Email, existingUser.Email);
                var update = Builders<User>.Update
                    .Set(u => u.Password, passwordHash) // Always sync password hash from ProfessorDB
                    .Set(u => u.IsVerified, true) // Ensure verified status
                    .Set(u => u.Role, "Professor"); // Ensure role is Professor
                
                // Update FullName if available
                if (!string.IsNullOrEmpty(professorName))
                {
                    update = update.Set(u => u.FullName, professorName);
                }
                
                await _users.UpdateOneAsync(filter, update);
                
                // Update local object for return
                existingUser.Password = passwordHash;
                existingUser.IsVerified = true;
                existingUser.Role = "Professor";
                if (!string.IsNullOrEmpty(professorName))
                {
                    existingUser.FullName = professorName;
                }
                
                return existingUser;
            }

            // Create new user from professor data in StudentDB Users collection
            var newUser = new User
            {
                Email = professorEmail,
                Password = passwordHash, // Use the password hash (hashed if needed)
                OTP = "",
                IsVerified = true, // Professors from ProfessorDB are already verified
                FullName = !string.IsNullOrEmpty(professorName) ? professorName : "",
                Role = "Professor", // Set role as Professor
                FailedLoginAttempts = 0,
                LockoutEndTime = null,
                JoinedClasses = new List<string>()
            };

            await _users.InsertOneAsync(newUser);
            return newUser;
        }

        public async Task<User?> GetUserByIdAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return null;
            return await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        }

        public async Task<User?> GetFirstStudentAsync()
        {
            return await _users.Find(u => u.Role == "Student").FirstOrDefaultAsync();
        }

        public async Task<bool> PushJoinedClassAsync(string email, string classCode)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Email, email);
            var update = Builders<User>.Update.Push(u => u.JoinedClasses, classCode);
            var result = await _users.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateUserAsync(User user)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Email, user.Email);
            var result = await _users.ReplaceOneAsync(filter, user);
            return result.IsAcknowledged && result.ModifiedCount > 0;
        }

        public async Task CreateUserAsync(string email, string hashedPassword, string otp, string role = "Student", bool markVerified = false)
        {
            var newUser = new User
            {
                Email = email,
                Password = hashedPassword,
                OTP = otp,
                IsVerified = markVerified,
                Role = role,
                JoinedClasses = new List<string>()
            };
            await _users.InsertOneAsync(newUser);
        }

        public async Task<bool> VerifyOtpAsync(string email, string otp)
        {
            var user = await GetUserByEmailAsync(email);
            return user != null && user.OTP == otp;
        }

        public async Task UpdateOtpAsync(string email, string newOtp)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Email, email);
            var update = Builders<User>.Update.Set(u => u.OTP, newOtp);
            await _users.UpdateOneAsync(filter, update);
        }

        public async Task UpdateUserPasswordAsync(string email, string hashedPassword)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Email, email);
            var update = Builders<User>.Update.Set(u => u.Password, hashedPassword);
            await _users.UpdateOneAsync(filter, update);
        }

        public async Task UpdateUserLoginStatusAsync(string email, int? failedAttempts, DateTime? lockoutEndTime)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Email, email);
            var update = Builders<User>.Update
                .Set(u => u.FailedLoginAttempts, failedAttempts)
                .Set(u => u.LockoutEndTime, lockoutEndTime);
            await _users.UpdateOneAsync(filter, update);
        }

        public async Task MarkUserAsVerifiedAsync(string email)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Email, email);
            var update = Builders<User>.Update.Set(u => u.IsVerified, true).Set(u => u.OTP, "");
            await _users.UpdateOneAsync(filter, update);
        }

        // ---------------- CLASSES ----------------
        public async Task<List<ClassItem>> GetAllClassesAsync()
        {
            return await _classes.Find(_ => true).ToListAsync();
        }

        /// <summary>
        /// Get classes owned by a specific professor (by email).
        /// </summary>
        public async Task<List<ClassItem>> GetClassesByOwnerEmailAsync(string ownerEmail)
        {
            if (string.IsNullOrWhiteSpace(ownerEmail))
                return new List<ClassItem>();

            var normalized = ownerEmail.Trim().ToLowerInvariant();
            return await _classes.Find(c => c.OwnerEmail.ToLower() == normalized).ToListAsync();
        }

        public async Task<ClassItem?> GetClassByCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var trimmed = code.Trim();
            var exact = await _classes.Find( c => c.ClassCode == trimmed).FirstOrDefaultAsync();
            if (exact != null)
                return exact;

            // Case-insensitive match (URLs or manual entry may differ in casing)
            return await _classes
                .Find(Builders<ClassItem>.Filter.Regex(
                    nameof(ClassItem.ClassCode),
                    new BsonRegularExpression($"^{Regex.Escape(trimmed)}$", "i")))
                .FirstOrDefaultAsync();
        }
      
        public async Task<ClassItem?> GetClassBySubjectCodeAsync(string subjectCode)
        {
            if (string.IsNullOrWhiteSpace(subjectCode)) return null;
            return await _classes.Find(c => c.SubjectCode == subjectCode).FirstOrDefaultAsync();
        }

        public async Task<ClassItem?> GetClassBySubjectNameAsync(string subjectName)
        {
            if (string.IsNullOrWhiteSpace(subjectName)) return null;
            var normalized = subjectName.Trim();
            return await _classes.Find(c => c.SubjectName == normalized).FirstOrDefaultAsync();
        }

        public async Task<ClassItem?> GetClassByIdAsync(string classId)
        {
            return await _classes.Find(c => c.Id == classId).FirstOrDefaultAsync();
        }

        public async Task<bool> ClassExistsAsync(string subjectName, string section, string year, string course, string semester)
        {
            return await _classes.Find(c =>
                c.SubjectName.ToLower() == subjectName.ToLower() &&
                c.Section.ToLower() == section.ToLower() &&
                c.Year.ToLower() == year.ToLower() &&
                c.Course.ToLower() == course.ToLower() &&
                c.Semester.ToLower() == semester.ToLower()).AnyAsync();
        }

        public async Task<List<ClassItem>> GetClassesByCodesAsync(List<string> classCodes)
        {
            if (classCodes == null || classCodes.Count == 0)
                return new List<ClassItem>();

            return await _classes.Find(c => classCodes.Contains(c.ClassCode)).ToListAsync();
        }

        public async Task<List<ClassItem>> GetClassesByIdsAsync(List<string> ids)
        {
            return await _classes.Find(c => ids.Contains(c.Id)).ToListAsync();
        }

        public async Task AddClassToStudentAsync(string studentEmail, string classCode)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Email, studentEmail);
            var update = Builders<User>.Update.AddToSet(u => u.JoinedClasses, classCode);
            await _users.UpdateOneAsync(filter, update);
        }

        public async Task CreateClassAsync(ClassItem newClass)
        {
            if (string.IsNullOrEmpty(newClass.Id))
                newClass.Id = ObjectId.GenerateNewId().ToString();

            await _classes.InsertOneAsync(newClass);
        }

        public async Task<bool> UpdateClassSeatAssignmentsAsync(string classCode, Dictionary<string, int> assignmentsByStudentKey)
        {
            if (string.IsNullOrWhiteSpace(classCode)) return false;

            var map = assignmentsByStudentKey ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            // Normalize keys + values
            var cleaned = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in map)
            {
                var k = (kv.Key ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(k)) continue;
                cleaned[k] = kv.Value;
            }

            var filter = Builders<ClassItem>.Filter.Eq(c => c.ClassCode, classCode.Trim());
            var update = Builders<ClassItem>.Update
                .Set(c => c.SeatAssignmentsByStudentKey, cleaned);

            var result = await _classes.UpdateOneAsync(filter, update);
            return result.MatchedCount > 0;
        }

        private static List<StudentRecord> OrderRosterBySeatAssignments(List<StudentRecord> roster, Dictionary<string, int>? seatMap)
        {
            if (roster == null || roster.Count == 0) return roster ?? new List<StudentRecord>();
            if (seatMap == null || seatMap.Count == 0)
                return roster.OrderBy(r => r.StudentName, StringComparer.OrdinalIgnoreCase).ToList();

            int SeatRank(StudentRecord r)
            {
                var id = (r.Id ?? string.Empty).Trim();
                var email = (r.StudentEmail ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(id) && seatMap.TryGetValue(id, out var idxId)) return idxId;
                if (!string.IsNullOrWhiteSpace(email) && seatMap.TryGetValue(email, out var idxEm)) return idxEm;
                return int.MaxValue - 1; // keep unassigned students after assigned seats, stable by name
            }

            return roster
                .OrderBy(SeatRank)
                .ThenBy(r => r.StudentName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<bool> DeleteClassByIdAsync(string classId)
        {
            var result = await _classes.DeleteOneAsync(c => c.Id == classId);
            return result.DeletedCount > 0;
        }

        public string GenerateClassCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            string code;
            do
            {
                code = new string(Enumerable.Repeat(chars, 6)
                    .Select(s => s[random.Next(s.Length)]).ToArray());
            } while (_classes.Find(c => c.ClassCode == code).AnyAsync().Result);
            return code;
        }

        public async Task<string?> GetSubjectNameByCodeAsync(string subjectCode)
        {
            try
            {
                var localCandidates = new[] { "admindb.subjects", "subjects", "Subjects" };
                foreach (var coll in localCandidates)
                {
                    try
                    {
                        var collection = _database.GetCollection<BsonDocument>(coll);
                        var fields = new[] { "subjectCode", "SubjectCode", "code", "Code" };
                        foreach (var f in fields)
                        {
                            var filter = Builders<BsonDocument>.Filter.Eq(f, subjectCode);
                            var doc = await collection.Find(filter).FirstOrDefaultAsync();
                            if (doc != null)
                            {
                                if (doc.Contains("title")) return doc["title"].ToString();
                                if (doc.Contains("Title")) return doc["Title"].ToString();
                                if (doc.Contains("subjectName")) return doc["subjectName"].ToString();
                                if (doc.Contains("SubjectName")) return doc["SubjectName"].ToString();
                                if (doc.Contains("name")) return doc["name"].ToString();
                                if (doc.Contains("Name")) return doc["Name"].ToString();
                                return string.Empty;
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                var esysCandidates = new[]
                {
                    "subject_ent", "Subject_Ent", "subjects_ent", "Subjects_Ent",
                    "subject_it",  "Subject_IT",  "subjects_it",  "Subjects_IT"
                };
                foreach (var coll in esysCandidates)
                {
                    try
                    {
                        var collection = _enrollmentDatabase.GetCollection<BsonDocument>(coll);
                        var codeFields = new[] { "subjectCode", "SubjectCode", "code", "Code" };
                        foreach (var f in codeFields)
                        {
                            var filter = Builders<BsonDocument>.Filter.Eq(f, subjectCode);
                            var doc = await collection.Find(filter).FirstOrDefaultAsync();
                            if (doc != null)
                            {
                                if (doc.Contains("title")) return doc["title"].ToString();
                                if (doc.Contains("Title")) return doc["Title"].ToString();
                                if (doc.Contains("subjectName")) return doc["subjectName"].ToString();
                                if (doc.Contains("SubjectName")) return doc["SubjectName"].ToString();
                                if (doc.Contains("name")) return doc["name"].ToString();
                                if (doc.Contains("Name")) return doc["Name"].ToString();
                                return string.Empty;
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<BsonDocument>> GetProfessorAssignedSubjectsAsync(string professorId)
        {
            var result = new List<BsonDocument>();
            if (string.IsNullOrWhiteSpace(professorId)) return result;
            try
            {
                // Try multiple possible collection names
                var collectionCandidates = new[]
                {
                    "professorAssignment",
                    "ProfessorAssignment",
                    "ProfessorAssignments",
                    "professorAssignments",
                    "professor_assignment"
                };

                BsonDocument? assignment = null;
                foreach (var collName in collectionCandidates)
                {
                    try
                    {
                        var collection = _enrollmentDatabase.GetCollection<BsonDocument>(collName);

                        var filters = new List<FilterDefinition<BsonDocument>>();
                        if (ObjectId.TryParse(professorId, out var pid))
                        {
                            filters.Add(Builders<BsonDocument>.Filter.Eq("professorId", pid));
                            filters.Add(Builders<BsonDocument>.Filter.Eq("ProfessorId", pid));
                        }
                        filters.Add(Builders<BsonDocument>.Filter.Eq("professorId", professorId));
                        filters.Add(Builders<BsonDocument>.Filter.Eq("ProfessorId", professorId));

                        var filter = Builders<BsonDocument>.Filter.Or(filters);
                        assignment = await collection.Find(filter).FirstOrDefaultAsync();
                        if (assignment != null)
                            break;
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (assignment == null || !assignment.Contains("classMeetingIds")) return result;

                var arr = assignment["classMeetingIds"].AsBsonArray;
                foreach (var v in arr)
                {
                    var raw = v.IsString ? v.AsString : v.ToString();
                    var parts = raw.Split(':');
                    if (parts.Length != 4) continue;
                    var section = parts[0];
                    var subjectCode = parts[1];
                    var units = parts[2];
                    var scheduleId = parts[3];
                    section = section?.Trim('"');
                    subjectCode = subjectCode?.Trim('"');
                    units = units?.Trim('"');
                    scheduleId = scheduleId?.Trim('"');
                    var subjectName = await GetSubjectNameByCodeAsync(subjectCode) ?? string.Empty;
                    var doc = new BsonDocument
                    {
                        { "section", section },
                        { "subjectCode", subjectCode },
                        { "units", units },
                        { "scheduleId", scheduleId },
                        { "subjectName", subjectName }
                    };
                    if (!string.IsNullOrWhiteSpace(section) && ObjectId.TryParse(section.Trim(), out _))
                        doc["sectionId"] = section.Trim();

                    // Enrich with classCode if a class already exists linked to this schedule
                    try
                    {
                        var ownerEmail = string.Empty; // owner unknown at this layer; will be resolved in controller
                    }
                    catch { }
                    result.Add(doc);
                }
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var d in result)
                {
                    var k = (d.GetValue("section", "").ToString() + "|" + d.GetValue("subjectCode", "").ToString() + "|" + d.GetValue("scheduleId", "").ToString());
                    seen.Add(k);
                }
                var meetingCollections = new[] { "classMeetings", "ClassMeetings", "classMeeting", "ClassMeeting", "SHSSchedules", "SHSSchedule", "SHSSchedules_v2" };
                foreach (var collName in meetingCollections)
                {
                    try
                    {
                        var coll = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                        var predicates = new List<FilterDefinition<BsonDocument>>();
                        if (ObjectId.TryParse(professorId, out var pid))
                        {
                            predicates.Add(Builders<BsonDocument>.Filter.Eq("TeacherId", pid));
                            predicates.Add(Builders<BsonDocument>.Filter.Eq("teacherId", pid));
                        }
                        predicates.Add(Builders<BsonDocument>.Filter.Eq("TeacherId", professorId));
                        predicates.Add(Builders<BsonDocument>.Filter.Eq("teacherId", professorId));
                        var filter = Builders<BsonDocument>.Filter.Or(predicates);
                        var docs = await coll.Find(filter).ToListAsync();
                        foreach (var m in docs)
                        {
                            string section = MeetingSectionDisplayLabel(m);
                            var sectionEnrollId = ReadEnrollmentSectionIdFromMeetingDoc(m);
                            string subjectCode = m.Contains("SubjectCode") ? m["SubjectCode"].ToString() : string.Empty;
                            string subjectName = m.Contains("SubjectName") ? m["SubjectName"].ToString() : string.Empty;
                            string units = m.Contains("Units") ? m["Units"].ToString() : string.Empty;
                            string schoolYear = m.Contains("SchoolYear") ? m["SchoolYear"].ToString() : string.Empty;
                        string semester = m.Contains("Semester") ? m["Semester"].ToString()
                            : (m.Contains("semester") ? m["semester"].ToString()
                            : (m.Contains("Term") ? m["Term"].ToString()
                            : (m.Contains("term") ? m["term"].ToString() : string.Empty)));
                            string timeSlotDisplay = m.Contains("TimeSlotDisplay") ? m["TimeSlotDisplay"].ToString() : string.Empty;
                            string roomName = m.Contains("RoomName") ? m["RoomName"].ToString() : string.Empty;
                            string scheduleId = string.Empty;
                            if (m.Contains("_id")) scheduleId = m["_id"].ToString();
                            else if (m.Contains("Id")) scheduleId = m["Id"].ToString();
                            if (string.IsNullOrWhiteSpace(subjectCode) && m.Contains("SubjectId")) subjectCode = m["SubjectId"].ToString();
                            var key = section + "|" + subjectCode + "|" + scheduleId;
                            if (seen.Contains(key)) continue;
                            var doc = new BsonDocument
                            {
                                { "section", section },
                                { "subjectCode", subjectCode },
                                { "units", units },
                                { "scheduleId", scheduleId },
                            { "subjectName", subjectName },
                            { "schoolYear", schoolYear },
                            { "semester", semester },
                            { "timeSlotDisplay", timeSlotDisplay },
                            { "roomName", roomName }
                            };
                            if (!string.IsNullOrWhiteSpace(sectionEnrollId))
                                doc["sectionId"] = sectionEnrollId;
                            result.Add(doc);
                            seen.Add(key);
                        }
                        if (result.Count > 0) break;
                    }
                    catch { continue; }
                }
            }
            catch
            {
            }
            return result;
        }

        public async Task<ClassItem?> GetClassByScheduleIdAndOwnerAsync(string scheduleId, string ownerEmail)
        {
            if (string.IsNullOrWhiteSpace(scheduleId) || string.IsNullOrWhiteSpace(ownerEmail)) return null;
            var normalized = ownerEmail.Trim().ToLowerInvariant();
            return await _classes.Find(c => c.ScheduleId == scheduleId && c.OwnerEmail.ToLower() == normalized).FirstOrDefaultAsync();
        }

        public async Task UpsertClassMeetingAsync(BsonDocument meeting)
        {
            if (meeting == null) return;
            var collCandidates = new[] { "classMeetings", "ClassMeetings", "classMeeting", "ClassMeeting" };
            IMongoCollection<BsonDocument>? coll = null;
            foreach (var name in collCandidates)
            {
                try
                {
                    coll = _enrollmentDatabase.GetCollection<BsonDocument>(name);
                    break;
                }
                catch { continue; }
            }
            coll ??= _enrollmentDatabase.GetCollection<BsonDocument>("classMeetings");

            // Determine identifier
            string id = string.Empty;
            if (meeting.Contains("_id")) id = meeting["_id"].ToString();
            else if (meeting.Contains("Id")) id = meeting["Id"].ToString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                var filter = Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Eq("_id", id),
                    Builders<BsonDocument>.Filter.Eq("Id", id)
                );
                await coll.ReplaceOneAsync(filter, meeting, new ReplaceOptions { IsUpsert = true });
            }
            else
            {
                await coll.InsertOneAsync(meeting);
            }
        }

        public async Task<List<BsonDocument>> GetClassMeetingsByTeacherNameAsync(string teacherName)
        {
            var result = new List<BsonDocument>();
            if (string.IsNullOrWhiteSpace(teacherName)) return result;
            var meetingCollections = new[] { "classMeetings", "ClassMeetings", "classMeeting", "ClassMeeting" };
            foreach (var collName in meetingCollections)
            {
                try
                {
                    var coll = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                    // Use case-insensitive contains match for robustness
                    var filter = Builders<BsonDocument>.Filter.Or(
                        Builders<BsonDocument>.Filter.Regex("TeacherName", new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(teacherName), "i")),
                        Builders<BsonDocument>.Filter.Regex("teacherName", new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(teacherName), "i"))
                    );
                    var docs = await coll.Find(filter).ToListAsync();
                    foreach (var m in docs)
                    {
                        string section = MeetingSectionDisplayLabel(m);
                        var sectionEnrollId = ReadEnrollmentSectionIdFromMeetingDoc(m);
                        string subjectCode = m.Contains("SubjectCode") ? m["SubjectCode"].ToString() : string.Empty;
                        string subjectName = m.Contains("SubjectName") ? m["SubjectName"].ToString() : string.Empty;
                        string units = m.Contains("Units") ? m["Units"].ToString() : string.Empty;
                        string schoolYear = m.Contains("SchoolYear") ? m["SchoolYear"].ToString() : string.Empty;
                        string scheduleId = string.Empty;
                        if (m.Contains("_id")) scheduleId = m["_id"].ToString();
                        else if (m.Contains("Id")) scheduleId = m["Id"].ToString();
                        var doc = new BsonDocument
                        {
                            { "section", section },
                            { "subjectCode", subjectCode },
                            { "units", units },
                            { "scheduleId", scheduleId },
                            { "subjectName", subjectName },
                            { "schoolYear", schoolYear }
                        };
                        if (!string.IsNullOrWhiteSpace(sectionEnrollId))
                            doc["sectionId"] = sectionEnrollId;
                        result.Add(doc);
                    }
                    if (result.Count > 0) break;
                }
                catch { continue; }
            }
            return result;
        }

        public async Task<List<BsonDocument>> GetClassMeetingsForProfessorFlexibleAsync(string? professorId, string? teacherName, string? teacherEmail)
        {
            var result = new List<BsonDocument>();
            var meetingCollections = new[]
            {
                "classMeetings", "ClassMeetings", "classMeeting", "ClassMeeting",
                "ClassMeetings_v2", "class_meetings", "Meetings", "meetings",
                "Schedules", "schedules", "Schedule", "schedule", "ClassSchedule", "ClassSchedules",
                "SHSSchedules", "SHSSchedule", "SHSSchedules_v2"
            };
            foreach (var collName in meetingCollections)
            {
                try
                {
                    var coll = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                    var ors = new List<FilterDefinition<BsonDocument>>();
                    if (!string.IsNullOrWhiteSpace(professorId))
                    {
                        if (ObjectId.TryParse(professorId, out var pid))
                        {
                            foreach (var f in new[] { "TeacherId", "teacherId", "ProfessorId", "professorId", "InstructorId", "instructorId", "FacultyId", "facultyId", "Teacher.Id", "teacher.id", "Professor.Id", "professor.id" })
                            {
                                ors.Add(Builders<BsonDocument>.Filter.Eq(f, pid));
                            }
                        }
                        foreach (var f in new[] { "TeacherId", "teacherId", "ProfessorId", "professorId", "InstructorId", "instructorId", "FacultyId", "facultyId", "Teacher.Id", "teacher.id", "Professor.Id", "professor.id" })
                        {
                            ors.Add(Builders<BsonDocument>.Filter.Eq(f, professorId));
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(teacherName))
                    {
                        var name = teacherName.Trim();
                        var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var t in tokens)
                        {
                            var re = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(t), "i");
                            foreach (var f in new[]
                            {
                                "TeacherName","teacherName","ProfessorName","professorName","InstructorName","instructorName",
                                "FacultyName","facultyName","AdviserName","adviserName","Teacher.Name","teacher.name","Professor.Name","professor.name"
                            })
                            {
                                ors.Add(Builders<BsonDocument>.Filter.Regex(f, re));
                            }
                        }
                        var fullRe = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(name), "i");
                        foreach (var f in new[]
                        {
                            "TeacherName","teacherName","ProfessorName","professorName","InstructorName","instructorName",
                            "FacultyName","facultyName","AdviserName","adviserName","Teacher.Name","teacher.name","Professor.Name","professor.name"
                        })
                        {
                            ors.Add(Builders<BsonDocument>.Filter.Regex(f, fullRe));
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(teacherEmail))
                    {
                        var re = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(teacherEmail.Trim()), "i");
                        foreach (var f in new[]
                        {
                            "TeacherEmail","teacherEmail","Email","email",
                            "ProfessorEmail","professorEmail","InstructorEmail","instructorEmail",
                            "FacultyEmail","facultyEmail","Teacher.Email","teacher.email","Professor.Email","professor.email"
                        })
                        {
                            ors.Add(Builders<BsonDocument>.Filter.Regex(f, re));
                        }
                    }
                    if (ors.Count == 0)
                    {
                        continue;
                    }
                    var filter = Builders<BsonDocument>.Filter.Or(ors);
                    var docs = await coll.Find(filter).ToListAsync();
                    foreach (var m in docs)
                    {
                        string section = MeetingSectionDisplayLabel(m);
                        var sectionEnrollId = ReadEnrollmentSectionIdFromMeetingDoc(m);
                        string subjectCode = m.Contains("SubjectCode") ? m["SubjectCode"].ToString() : string.Empty;
                        string subjectName = m.Contains("SubjectName") ? m["SubjectName"].ToString() : string.Empty;
                        string units = m.Contains("Units") ? m["Units"].ToString() : string.Empty;
                        string schoolYear = m.Contains("SchoolYear") ? m["SchoolYear"].ToString() : string.Empty;
                        string semester = m.Contains("Semester") ? m["Semester"].ToString()
                            : (m.Contains("semester") ? m["semester"].ToString()
                            : (m.Contains("Term") ? m["Term"].ToString()
                            : (m.Contains("term") ? m["term"].ToString() : string.Empty)));
                        string timeSlotDisplay = m.Contains("TimeSlotDisplay") ? m["TimeSlotDisplay"].ToString() : string.Empty;
                        string roomName = m.Contains("RoomName") ? m["RoomName"].ToString() : string.Empty;
                        string scheduleId = string.Empty;
                        if (m.Contains("_id")) scheduleId = m["_id"].ToString();
                        else if (m.Contains("Id")) scheduleId = m["Id"].ToString();
                        var doc = new BsonDocument
                        {
                            { "section", section },
                            { "subjectCode", subjectCode },
                            { "units", units },
                            { "scheduleId", scheduleId },
                            { "subjectName", subjectName },
                            { "schoolYear", schoolYear },
                            { "semester", semester },
                            { "timeSlotDisplay", timeSlotDisplay },
                            { "roomName", roomName }
                        };
                        if (!string.IsNullOrWhiteSpace(sectionEnrollId))
                            doc["sectionId"] = sectionEnrollId;
                        result.Add(doc);
                    }
                    if (result.Count > 0) break;
                }
                catch { continue; }
            }
            return result;
        }

        /// <summary>
        /// Gets sections assigned to the professor from SHSSchedules (SectionID), with optional names from SHSSections.
        /// </summary>
        public async Task<List<(string sectionId, string sectionName)>> GetProfessorAssignedSectionsAsync(string? professorId, string? teacherName, string? teacherEmail)
        {
            var sectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var meetingCollections = new[]
            {
                "SHSSchedules", "SHSSchedule", "SHSSchedules_v2",
                "classMeetings", "ClassMeetings", "classMeeting", "ClassMeeting",
                "Schedules", "schedules", "ClassSchedule", "ClassSchedules"
            };
            foreach (var collName in meetingCollections)
            {
                try
                {
                    var coll = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                    var ors = new List<FilterDefinition<BsonDocument>>();
                    if (!string.IsNullOrWhiteSpace(professorId))
                    {
                        if (ObjectId.TryParse(professorId, out var pid))
                        {
                            foreach (var f in new[] { "TeacherId", "teacherId", "ProfessorId", "professorId", "InstructorId", "instructorId" })
                                ors.Add(Builders<BsonDocument>.Filter.Eq(f, pid));
                        }
                        foreach (var f in new[] { "TeacherId", "teacherId", "ProfessorId", "professorId", "InstructorId", "instructorId" })
                            ors.Add(Builders<BsonDocument>.Filter.Eq(f, professorId));
                    }
                    if (!string.IsNullOrWhiteSpace(teacherName))
                    {
                        var re = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(teacherName.Trim()), "i");
                        foreach (var f in new[] { "TeacherName", "teacherName", "ProfessorName", "professorName", "InstructorName", "instructorName" })
                            ors.Add(Builders<BsonDocument>.Filter.Regex(f, re));
                    }
                    if (!string.IsNullOrWhiteSpace(teacherEmail))
                    {
                        var re = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(teacherEmail.Trim()), "i");
                        foreach (var f in new[] { "TeacherEmail", "teacherEmail", "Email", "email", "ProfessorEmail", "professorEmail" })
                            ors.Add(Builders<BsonDocument>.Filter.Regex(f, re));
                    }
                    if (ors.Count == 0) continue;
                    var filter = Builders<BsonDocument>.Filter.Or(ors);
                    var docs = await coll.Find(filter).ToListAsync();
                    foreach (var m in docs)
                    {
                        string sid = string.Empty;
                        if (m.Contains("SectionID")) sid = m["SectionID"].ToString();
                        else if (m.Contains("SectionId")) sid = m["SectionId"].ToString();
                        else if (m.Contains("sectionId")) sid = m["sectionId"].ToString();
                        else if (m.Contains("SectionName")) sid = m["SectionName"].ToString();
                        if (!string.IsNullOrWhiteSpace(sid)) sectionIds.Add(sid.Trim());
                    }
                    if (sectionIds.Count > 0) break;
                }
                catch { continue; }
            }

            var result = new List<(string sectionId, string sectionName)>();
            var sectionsCollectionNames = new[] { "SHSSections", "SHSSection", "Sections", "sections" };
            foreach (var sectionId in sectionIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                string name = sectionId;
                foreach (var collName in sectionsCollectionNames)
                {
                    try
                    {
                        var sectionsColl = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                        var filter = Builders<BsonDocument>.Filter.Or(
                            Builders<BsonDocument>.Filter.Eq("_id", sectionId),
                            Builders<BsonDocument>.Filter.Eq("Id", sectionId)
                        );
                        var doc = await sectionsColl.Find(filter).FirstOrDefaultAsync();
                        if (doc != null)
                        {
                            foreach (var k in new[] { "SectionName", "sectionName", "Name", "name", "Title", "title", "Label", "label" })
                            {
                                if (doc.Contains(k) && !doc[k].IsBsonNull && !string.IsNullOrWhiteSpace(doc[k].ToString()))
                                {
                                    name = doc[k].ToString().Trim();
                                    break;
                                }
                            }
                            break;
                        }
                    }
                    catch { continue; }
                }
                result.Add((sectionId, name));
            }
            return result;
        }

        /// <summary>SHSSchedules / classMeetings often store the enrollment FK as SectionID (capital ID).</summary>
        private static string ReadEnrollmentSectionIdFromMeetingDoc(BsonDocument m)
        {
            if (m == null) return string.Empty;
            foreach (var key in new[] { "SectionID", "SectionId", "sectionId" })
            {
                if (m.Contains(key) && !m[key].IsBsonNull)
                {
                    var s = m[key].ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            }
            return string.Empty;
        }

        private static string MeetingSectionDisplayLabel(BsonDocument m)
        {
            if (m == null) return string.Empty;
            foreach (var key in new[] { "SectionName", "sectionName" })
            {
                if (m.Contains(key) && !m[key].IsBsonNull)
                {
                    var s = m[key].ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            }
            var enrollId = ReadEnrollmentSectionIdFromMeetingDoc(m);
            return string.IsNullOrWhiteSpace(enrollId) ? string.Empty : enrollId;
        }

        private static string EnrollmentSectionKey(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            return new string(s.Trim().Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }

        private static string? ReadEmailFromStudentBson(BsonDocument d)
        {
            if (d.Contains("Email") && !d["Email"].IsBsonNull)
            {
                var e = d["Email"].ToString();
                if (!string.IsNullOrWhiteSpace(e)) return e.Trim();
            }
            if (d.Contains("email") && !d["email"].IsBsonNull)
            {
                var e = d["email"].ToString();
                if (!string.IsNullOrWhiteSpace(e)) return e.Trim();
            }
            if (d.TryGetValue("Student", out var stu) && stu.IsBsonDocument)
            {
                var sd = stu.AsBsonDocument;
                if (sd.Contains("Email") && !sd["Email"].IsBsonNull)
                {
                    var e = sd["Email"].ToString();
                    if (!string.IsNullOrWhiteSpace(e)) return e.Trim();
                }
                if (sd.Contains("email") && !sd["email"].IsBsonNull)
                {
                    var e = sd["email"].ToString();
                    if (!string.IsNullOrWhiteSpace(e)) return e.Trim();
                }
            }
            return null;
        }

        private static string? ReadEnrollmentUsernameFromDoc(BsonDocument d)
        {
            static string? Pick(BsonDocument doc, string key)
            {
                if (!doc.Contains(key) || doc[key].IsBsonNull) return null;
                var s = doc[key].ToString()?.Trim();
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
            return Pick(d, "StudentUsername") ?? Pick(d, "studentUsername")
                   ?? Pick(d, "Username") ?? Pick(d, "username")
                   ?? Pick(d, "StudentId") ?? Pick(d, "studentId");
        }

        private static void AddEqStringOrObjectId(string field, string sid, ICollection<FilterDefinition<BsonDocument>> filters)
        {
            filters.Add(Builders<BsonDocument>.Filter.Eq(field, sid));
            if (ObjectId.TryParse(sid.Trim(), out var oid))
                filters.Add(Builders<BsonDocument>.Filter.Eq(field, oid));
        }

        /// <summary>Find SHSSections (etc.) documents whose name/label matches the class section text.</summary>
        private async Task<List<string>> ResolveSectionDocumentIdsByLabelFromEnrollmentAsync(string label)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(label)) return ids.ToList();
            var trimmed = label.Trim();
            var norm = EnrollmentSectionKey(trimmed);
            var sectionsCollectionNames = new[] { "SHSSections", "SHSSection", "Sections", "sections" };
            foreach (var collName in sectionsCollectionNames)
            {
                try
                {
                    var coll = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                    var escaped = Regex.Escape(trimmed);
                    var rx = new BsonRegularExpression("^" + escaped + "$", "i");
                    var nameMatch = Builders<BsonDocument>.Filter.Or(
                        Builders<BsonDocument>.Filter.Regex("SectionName", rx),
                        Builders<BsonDocument>.Filter.Regex("sectionName", rx),
                        Builders<BsonDocument>.Filter.Regex("Name", rx),
                        Builders<BsonDocument>.Filter.Regex("name", rx),
                        Builders<BsonDocument>.Filter.Regex("Title", rx),
                        Builders<BsonDocument>.Filter.Regex("title", rx),
                        Builders<BsonDocument>.Filter.Regex("Label", rx),
                        Builders<BsonDocument>.Filter.Regex("label", rx),
                        Builders<BsonDocument>.Filter.Regex("Code", rx),
                        Builders<BsonDocument>.Filter.Regex("code", rx)
                    );
                    using var cur = await coll.Find(nameMatch).Limit(40).ToCursorAsync();
                    while (await cur.MoveNextAsync())
                    {
                        foreach (var doc in cur.Current)
                        {
                            if (doc.TryGetValue("_id", out var idv) && idv != null && !idv.IsBsonNull)
                                ids.Add(idv.ToString()!);
                            if (doc.Contains("Id") && !doc["Id"].IsBsonNull)
                            {
                                var s = doc["Id"].ToString();
                                if (!string.IsNullOrWhiteSpace(s)) ids.Add(s.Trim());
                            }
                        }
                    }

                    if (ids.Count == 0 && !string.IsNullOrEmpty(norm))
                    {
                        var proj = Builders<BsonDocument>.Projection.Include("_id").Include("Id")
                            .Include("SectionName").Include("sectionName").Include("Name").Include("name")
                            .Include("Title").Include("title").Include("Label").Include("label").Include("Code").Include("code");
                        using var cur2 = await coll.Find(FilterDefinition<BsonDocument>.Empty).Project(proj).Limit(4000).ToCursorAsync();
                        while (await cur2.MoveNextAsync())
                        {
                            foreach (var doc in cur2.Current)
                            {
                                foreach (var k in new[] { "SectionName", "sectionName", "Name", "name", "Title", "title", "Label", "label", "Code", "code" })
                                {
                                    if (!doc.Contains(k) || doc[k].IsBsonNull) continue;
                                    var txt = doc[k].ToString();
                                    if (string.IsNullOrWhiteSpace(txt)) continue;
                                    if (EnrollmentSectionKey(txt) != norm) continue;
                                    if (doc.TryGetValue("_id", out var idv2) && idv2 != null && !idv2.IsBsonNull)
                                        ids.Add(idv2.ToString()!);
                                    if (doc.Contains("Id") && !doc["Id"].IsBsonNull)
                                    {
                                        var s = doc["Id"].ToString();
                                        if (!string.IsNullOrWhiteSpace(s)) ids.Add(s.Trim());
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { continue; }
            }
            return ids.ToList();
        }

        private async Task<List<string>> TryGetStudentEmailsMatchingSectionTextAsync(string sectionText)
        {
            var emails = new List<string>();
            if (string.IsNullOrWhiteSpace(sectionText)) return emails;
            var sec = sectionText.Trim();
            var escaped = Regex.Escape(sec);
            var rx = new BsonRegularExpression("^" + escaped + "$", "i");
            var textFilters = Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Regex("Section", rx),
                Builders<BsonDocument>.Filter.Regex("section", rx),
                Builders<BsonDocument>.Filter.Regex("SectionName", rx),
                Builders<BsonDocument>.Filter.Regex("sectionName", rx),
                Builders<BsonDocument>.Filter.Regex("CurrentSection", rx),
                Builders<BsonDocument>.Filter.Regex("currentSection", rx),
                Builders<BsonDocument>.Filter.Regex("Student.Section", rx),
                Builders<BsonDocument>.Filter.Regex("Student.section", rx),
                Builders<BsonDocument>.Filter.Regex("Student.SectionName", rx)
            );
            var studentCollections = new[] { _enrollmentStudents?.CollectionNamespace.CollectionName ?? "SHSStudents", "SHSStudents", "shsstudents", "Students", "students", "STUDENTS" };
            foreach (var collName in studentCollections.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var coll = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                    var docs = await coll.Find(textFilters).Limit(2000).ToListAsync();
                    foreach (var d in docs)
                    {
                        var e = ReadEmailFromStudentBson(d);
                        if (!string.IsNullOrWhiteSpace(e)) emails.Add(e);
                    }
                    if (emails.Count > 0) break;
                }
                catch { continue; }
            }
            return emails;
        }

        /// <summary>
        /// Gets student emails from enrollment student collections where section id fields match.
        /// </summary>
        public async Task<List<string>> GetStudentEmailsBySectionIdAsync(string sectionId)
        {
            var emails = new List<string>();
            if (string.IsNullOrWhiteSpace(sectionId)) return emails;
            try
            {
                var sid = sectionId.Trim();
                var sectionFilters = new List<FilterDefinition<BsonDocument>>();
                foreach (var field in new[]
                         {
                             "CurrentSectionId", "currentSectionId",
                             "SectionID", "SectionId", "sectionId",
                             "AssignedSectionId", "assignedSectionId",
                             "Student.CurrentSectionId", "Student.currentSectionId",
                             "Student.SectionID", "Student.SectionId", "Student.sectionId"
                         })
                {
                    AddEqStringOrObjectId(field, sid, sectionFilters);
                }

                var studentCollections = new[] { _enrollmentStudents?.CollectionNamespace.CollectionName ?? "SHSStudents", "SHSStudents", "shsstudents", "Students", "students", "STUDENTS" };
                foreach (var collName in studentCollections.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var coll = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                        var filter = Builders<BsonDocument>.Filter.Or(sectionFilters);
                        var projection = Builders<BsonDocument>.Projection
                            .Include("Email").Include("email")
                            .Include("Student");
                        var docs = await coll.Find(filter).Project(projection).ToListAsync();
                        foreach (var d in docs)
                        {
                            var e = ReadEmailFromStudentBson(d);
                            if (!string.IsNullOrWhiteSpace(e)) emails.Add(e);
                        }
                        if (emails.Count > 0) break;
                    }
                    catch { continue; }
                }
            }
            catch { }
            return emails.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public async Task<List<string>> GetStudentEmailsByScheduleIdAsync(string scheduleId)
        {
            var emails = new List<string>();
            if (string.IsNullOrWhiteSpace(scheduleId)) return emails;
            try
            {
                // First, try to derive SectionId via professorAssignments by matching scheduleId in classMeetingIds
                string sectionId = string.Empty;
                try
                {
                    var profAssignCollections = new[] { "professorAssignments", "ProfessorAssignments", "professorAssignment", "ProfessorAssignment" };
                    foreach (var collName in profAssignCollections)
                    {
                        try
                        {
                            var coll = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                            var docs = await coll.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
                            foreach (var doc in docs)
                            {
                                if (!doc.Contains("classMeetingIds")) continue;
                                var arr = doc["classMeetingIds"].AsBsonArray;
                                foreach (var v in arr)
                                {
                                    var raw = v.IsString ? v.AsString : v.ToString();
                                    var parts = raw.Split(':');
                                    if (parts.Length == 4)
                                    {
                                        var sec = parts[0]?.Trim('"');
                                        var sched = parts[3]?.Trim('"');
                                        if (string.Equals(sched, scheduleId, StringComparison.Ordinal))
                                        {
                                            sectionId = sec ?? string.Empty;
                                            break;
                                        }
                                    }
                                }
                                if (!string.IsNullOrWhiteSpace(sectionId)) break;
                            }
                            if (!string.IsNullOrWhiteSpace(sectionId)) break;
                        }
                        catch { continue; }
                    }
                }
                catch { }

                if (string.IsNullOrWhiteSpace(sectionId))
                {
                    // Enrollment DB: SHSSchedules links schedule _id → SectionID → SHSStudents.CurrentSectionId
                    var meetingCollections = new[]
                    {
                        "SHSSchedules", "SHSSchedule", "SHSSchedules_v2",
                        "classMeetings", "ClassMeetings", "classMeeting", "ClassMeeting",
                        "Schedules", "schedules"
                    };
                    BsonDocument? meeting = null;
                    foreach (var collName in meetingCollections)
                    {
                        try
                        {
                            var coll = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                            var schedTrim = scheduleId.Trim();
                            var idFilters = new List<FilterDefinition<BsonDocument>>
                            {
                                Builders<BsonDocument>.Filter.Eq("Id", schedTrim),
                                Builders<BsonDocument>.Filter.Eq("_id", schedTrim),
                                Builders<BsonDocument>.Filter.Eq("ScheduleId", schedTrim),
                                Builders<BsonDocument>.Filter.Eq("scheduleId", schedTrim)
                            };
                            if (ObjectId.TryParse(schedTrim, out var schedOid))
                            {
                                idFilters.Add(Builders<BsonDocument>.Filter.Eq("_id", schedOid));
                                idFilters.Add(Builders<BsonDocument>.Filter.Eq("Id", schedOid));
                                idFilters.Add(Builders<BsonDocument>.Filter.Eq("ScheduleId", schedOid));
                                idFilters.Add(Builders<BsonDocument>.Filter.Eq("scheduleId", schedOid));
                            }
                            var filter = Builders<BsonDocument>.Filter.Or(idFilters);
                            meeting = await coll.Find(filter).FirstOrDefaultAsync();
                            if (meeting != null) break;
                        }
                        catch { continue; }
                    }

                    if (meeting != null)
                    {
                        if (meeting.Contains("SectionID")) sectionId = meeting["SectionID"].ToString();
                        else if (meeting.Contains("SectionId")) sectionId = meeting["SectionId"].ToString();
                        else if (meeting.Contains("sectionId")) sectionId = meeting["sectionId"].ToString();
                    }
                }

                if (string.IsNullOrWhiteSpace(sectionId)) return emails;

                // Prefer SHSStudents.CurrentSectionId (matches SHSSections._id / SHSSchedules.SectionID)
                var directBySection = await GetStudentEmailsBySectionIdAsync(sectionId);
                if (directBySection.Count > 0)
                    return directBySection.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                var enrollmentCollections = new[] { "studentsectionenrollment", "studentSectionEnrollment", "StudentSectionEnrollment", "studentSectionEnrollments", "StudentSectionEnrollments", "student_section_enrollments" };
                var usernames = new List<string>();
                foreach (var collName in enrollmentCollections)
                {
                    try
                    {
                        var coll = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                        var secFilters = new List<FilterDefinition<BsonDocument>>
                        {
                            Builders<BsonDocument>.Filter.Eq("SectionID", sectionId),
                            Builders<BsonDocument>.Filter.Eq("SectionId", sectionId),
                            Builders<BsonDocument>.Filter.Eq("sectionId", sectionId)
                        };
                        if (ObjectId.TryParse(sectionId.Trim(), out var enrollSecOid))
                        {
                            secFilters.Add(Builders<BsonDocument>.Filter.Eq("SectionID", enrollSecOid));
                            secFilters.Add(Builders<BsonDocument>.Filter.Eq("SectionId", enrollSecOid));
                            secFilters.Add(Builders<BsonDocument>.Filter.Eq("sectionId", enrollSecOid));
                        }
                        var filter = Builders<BsonDocument>.Filter.Or(secFilters);
                        var docs = await coll.Find(filter).ToListAsync();
                        if (docs != null && docs.Count > 0)
                        {
                            foreach (var d in docs)
                            {
                                var u = ReadEnrollmentUsernameFromDoc(d);
                                if (!string.IsNullOrWhiteSpace(u)) usernames.Add(u);
                            }
                            break;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (usernames.Count == 0) return emails;

                var mainStudentCollection = _enrollmentStudents?.CollectionNamespace.CollectionName ?? "SHSStudents";
                var studentCollections = new[] { mainStudentCollection, "SHSStudents", "shsstudents", "students", "Students", "STUDENTS" };
                foreach (var collName in studentCollections)
                {
                    try
                    {
                        var coll = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                        var filter = Builders<BsonDocument>.Filter.Or(
                            Builders<BsonDocument>.Filter.In("Username", usernames),
                            Builders<BsonDocument>.Filter.In("username", usernames),
                            Builders<BsonDocument>.Filter.In("StudentId", usernames),
                            Builders<BsonDocument>.Filter.In("studentId", usernames)
                        );
                        var projection = Builders<BsonDocument>.Projection.Include("Email").Include("email").Include("Username").Include("username").Include("Student");
                        var docs = await coll.Find(filter).Project(projection).ToListAsync();
                        foreach (var d in docs)
                        {
                            var e = ReadEmailFromStudentBson(d);
                            if (!string.IsNullOrWhiteSpace(e)) emails.Add(e);
                        }
                        if (emails.Count > 0) break;
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch
            {
            }
            return emails.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public async Task<(string room, string floorDisplay)> GetRoomAndFloorByScheduleIdAsync(string scheduleId)
        {
            if (string.IsNullOrWhiteSpace(scheduleId)) return (string.Empty, string.Empty);
            try
            {
                var meetingCollections = new[] { "classMeetings", "ClassMeetings", "classMeeting", "ClassMeeting", "Schedules", "schedules", "SHSSchedules", "shsSchedules", "shs_schedules" };
                foreach (var collName in meetingCollections)
                {
                    try
                    {
                        var coll = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                        var filter = Builders<BsonDocument>.Filter.Or(new[] {
                            Builders<BsonDocument>.Filter.Eq("Id", scheduleId),
                            Builders<BsonDocument>.Filter.Eq("_id", scheduleId)
                        });
                        var m = await coll.Find(filter).FirstOrDefaultAsync();
                        if (m == null) continue;
                        string room = m.Contains("RoomName") ? m["RoomName"].ToString() :
                                      (m.Contains("roomName") ? m["roomName"].ToString() :
                                      (m.Contains("Room") ? m["Room"].ToString() :
                                      (m.Contains("room") ? m["room"].ToString() : string.Empty)));
                        string floor = m.Contains("Floor") ? m["Floor"].ToString() :
                                       (m.Contains("floor") ? m["floor"].ToString() : string.Empty);
                        if (string.IsNullOrWhiteSpace(floor) && !string.IsNullOrWhiteSpace(room))
                        {
                            var digits = new string(room.Where(char.IsDigit).ToArray());
                            if (!string.IsNullOrWhiteSpace(digits))
                            {
                                var first = digits[0];
                                if (int.TryParse(first.ToString(), out var fnum) && fnum > 0)
                                {
                                    floor = fnum == 1 ? "1st Floor" : fnum == 2 ? "2nd Floor" : fnum == 3 ? "3rd Floor" : $"{fnum}th Floor";
                                }
                            }
                        }
                        return (room, floor);
                    }
                    catch { continue; }
                }
            }
            catch { }
            return (string.Empty, string.Empty);
        }

        /// <summary>Gets room and time slot display for dashboard (e.g. "Room 305", "Mon & Wed • 10:00 AM – 11:30 AM").</summary>
        public async Task<(string room, string scheduleTimeDisplay)> GetRoomAndScheduleTimeByScheduleIdAsync(string scheduleId)
        {
            if (string.IsNullOrWhiteSpace(scheduleId)) return (string.Empty, string.Empty);
            try
            {
                var meetingCollections = new[] { "classMeetings", "ClassMeetings", "classMeeting", "ClassMeeting", "Schedules", "schedules", "SHSSchedules", "shsSchedules", "shs_schedules" };
                foreach (var collName in meetingCollections)
                {
                    try
                    {
                        var coll = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                        var filter = Builders<BsonDocument>.Filter.Or(
                            Builders<BsonDocument>.Filter.Eq("Id", scheduleId),
                            Builders<BsonDocument>.Filter.Eq("_id", scheduleId));
                        var m = await coll.Find(filter).FirstOrDefaultAsync();
                        if (m == null) continue;
                        string room = string.Empty;
                        if (m.Contains("RoomName")) room = m["RoomName"].ToString();
                        else if (m.Contains("roomName")) room = m["roomName"].ToString();
                        else if (m.Contains("Room")) room = m["Room"].ToString();
                        else if (m.Contains("room")) room = m["room"].ToString();
                        if (!string.IsNullOrWhiteSpace(room) && !room.StartsWith("Room ", StringComparison.OrdinalIgnoreCase))
                            room = "Room " + room.Trim();
                        string timeSlot = m.Contains("TimeSlotDisplay") ? m["TimeSlotDisplay"].ToString() :
                                         (m.Contains("timeSlotDisplay") ? m["timeSlotDisplay"].ToString() : string.Empty);
                        return (room.Trim(), timeSlot.Trim());
                    }
                    catch { continue; }
                }
            }
            catch { }
            return (string.Empty, string.Empty);
        }

        // Fetch role and department from StudentDB "Teachers" collection
        public async Task<(string role, string department)> GetTeacherRoleAndDepartmentByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return (string.Empty, string.Empty);
            try
            {
                var normalized = email.Trim().ToLowerInvariant();
                var collectionNames = new[] { "Teachers", "teachers", "TEACHERS" };
                foreach (var name in collectionNames)
                {
                    try
                    {
                        var coll = _database.GetCollection<BsonDocument>(name);
                        var regex = new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(normalized)}$", "i");
                        var filter = Builders<BsonDocument>.Filter.Or(
                            Builders<BsonDocument>.Filter.Regex("Email", regex),
                            Builders<BsonDocument>.Filter.Regex("email", regex)
                        );
                        var doc = await coll.Find(filter).FirstOrDefaultAsync();
                        if (doc == null) continue;
                        string role = string.Empty;
                        string dept = string.Empty;
                        foreach (var k in new[] { "Role", "role", "FacultyRole", "facultyRole" })
                        {
                            if (doc.Contains(k) && !doc[k].IsBsonNull)
                            {
                                role = doc[k].ToString();
                                if (!string.IsNullOrWhiteSpace(role)) break;
                            }
                        }
                        foreach (var k in new[] { "Department", "department", "Dept", "dept", "DepartmentName", "departmentName" })
                        {
                            if (doc.Contains(k) && !doc[k].IsBsonNull)
                            {
                                dept = doc[k].ToString();
                                if (!string.IsNullOrWhiteSpace(dept)) break;
                            }
                        }
                        return (role ?? string.Empty, dept ?? string.Empty);
                    }
                    catch { continue; }
                }
            }
            catch { }
            return (string.Empty, string.Empty);
        }

        public async Task<List<string>> GetStudentEmailsBySectionAsync(string section)
        {
            var emails = new List<string>();
            if (string.IsNullOrWhiteSpace(section)) return emails;
            try
            {
                var sec = section.Trim();
                // SHSSections._id as 24-char hex → SHSStudents.CurrentSectionId
                if (sec.Length == 24 && Regex.IsMatch(sec, @"^[a-fA-F0-9]{24}$"))
                {
                    var bySectionId = await GetStudentEmailsBySectionIdAsync(sec);
                    if (bySectionId.Count > 0)
                        return bySectionId.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                }

                foreach (var rid in await ResolveSectionDocumentIdsByLabelFromEnrollmentAsync(sec))
                {
                    var part = await GetStudentEmailsBySectionIdAsync(rid);
                    if (part.Count > 0) emails.AddRange(part);
                }
                emails = emails.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (emails.Count > 0)
                    return emails;

                // Try multiple variations of the enrollment collection name
                var enrollmentCollections = new[] { "studentsectionenrollment", "studentSectionEnrollment", "StudentSectionEnrollment", "student_section_enrollment", "studentSectionEnrollments", "StudentSectionEnrollments" };
                var usernames = new List<string>();
                foreach (var collName in enrollmentCollections)
                {
                    try
                    {
                        var coll = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                        var enrollSecFilters = new List<FilterDefinition<BsonDocument>>
                        {
                            Builders<BsonDocument>.Filter.Eq("SectionID", sec),
                            Builders<BsonDocument>.Filter.Eq("SectionId", sec),
                            Builders<BsonDocument>.Filter.Eq("sectionId", sec)
                        };
                        if (ObjectId.TryParse(sec, out var secOid))
                        {
                            enrollSecFilters.Add(Builders<BsonDocument>.Filter.Eq("SectionID", secOid));
                            enrollSecFilters.Add(Builders<BsonDocument>.Filter.Eq("SectionId", secOid));
                            enrollSecFilters.Add(Builders<BsonDocument>.Filter.Eq("sectionId", secOid));
                        }
                        var filter = Builders<BsonDocument>.Filter.Or(enrollSecFilters);
                        var docs = await coll.Find(filter).ToListAsync();
                        if (docs != null && docs.Count > 0)
                        {
                            foreach (var d in docs)
                            {
                                var u = ReadEnrollmentUsernameFromDoc(d);
                                if (!string.IsNullOrWhiteSpace(u)) usernames.Add(u);
                            }
                            break;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (usernames.Count == 0)
                {
                    // As a fallback, try case-insensitive regex match on Section
                    foreach (var collName in enrollmentCollections)
                    {
                        try
                        {
                            var coll = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                            var regex = new BsonRegularExpression($"^{Regex.Escape(sec)}$", "i");
                            var filter = Builders<BsonDocument>.Filter.Or(
                                Builders<BsonDocument>.Filter.Regex("SectionID", regex),
                                Builders<BsonDocument>.Filter.Regex("SectionId", regex),
                                Builders<BsonDocument>.Filter.Regex("sectionId", regex)
                            );
                            var docs = await coll.Find(filter).ToListAsync();
                            foreach (var d in docs)
                            {
                                var u = ReadEnrollmentUsernameFromDoc(d);
                                if (!string.IsNullOrWhiteSpace(u)) usernames.Add(u);
                            }
                            if (usernames.Count > 0) break;
                        }
                        catch { continue; }
                    }
                }

                if (usernames.Count == 0)
                {
                    var byText = await TryGetStudentEmailsMatchingSectionTextAsync(sec);
                    return byText.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                }

                var mainStudentCollection = _enrollmentStudents?.CollectionNamespace.CollectionName ?? "SHSStudents";
                var studentCollections = new[] { mainStudentCollection, "SHSStudents", "shsstudents", "students", "Students", "STUDENTS" };
                foreach (var collName in studentCollections)
                {
                    try
                    {
                        var coll = _enrollmentDatabase.GetCollection<BsonDocument>(collName);
                        var filter = Builders<BsonDocument>.Filter.Or(
                            Builders<BsonDocument>.Filter.In("Username", usernames),
                            Builders<BsonDocument>.Filter.In("username", usernames),
                            Builders<BsonDocument>.Filter.In("StudentId", usernames),
                            Builders<BsonDocument>.Filter.In("studentId", usernames)
                        );
                        var projection = Builders<BsonDocument>.Projection.Include("Email").Include("email").Include("Username").Include("username").Include("Student");
                        var docs = await coll.Find(filter).Project(projection).ToListAsync();
                        foreach (var d in docs)
                        {
                            var e = ReadEmailFromStudentBson(d);
                            if (!string.IsNullOrWhiteSpace(e)) emails.Add(e);
                        }
                        if (emails.Count > 0) break;
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch
            {
            }
            return emails.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Best-effort: fetch enrolled student emails for a class.
        /// Prefers ScheduleId → SectionId resolution, then falls back to SectionLabel/Section.
        /// </summary>
        public async Task<List<string>> GetStudentEmailsForClassAsync(StudentPortal.Models.AdminDb.ClassItem classItem)
        {
            var recipients = new List<string>();
            try
            {
                if (classItem == null) return recipients;

                if (!string.IsNullOrWhiteSpace(classItem.EnrollmentSectionId))
                    recipients = await GetStudentEmailsBySectionIdAsync(classItem.EnrollmentSectionId) ?? new List<string>();

                if (recipients.Count == 0 && !string.IsNullOrWhiteSpace(classItem.ScheduleId))
                    recipients = await GetStudentEmailsByScheduleIdAsync(classItem.ScheduleId) ?? new List<string>();

                if (recipients.Count == 0)
                {
                    var sec = !string.IsNullOrWhiteSpace(classItem.SectionLabel)
                        ? classItem.SectionLabel
                        : (classItem.Section ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(sec))
                        recipients = await GetStudentEmailsBySectionAsync(sec) ?? new List<string>();
                }
            }
            catch
            {
                // Notifications are best-effort
            }

            return recipients
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // ---------------- STUDENT MANAGEMENT ----------------
        public async Task<List<Student>> GetStudentsByClassIdAsync(string classId)
        {
            var classItem = await _classes.Find(c => c.Id == classId).FirstOrDefaultAsync();
            if (classItem == null) return new List<Student>();

            // Get students from Users collection who have this class in their JoinedClasses
            var usersInClass = await _users
                .Find(u => u.JoinedClasses.Contains(classItem.ClassCode) && u.Role == "Student")
                .ToListAsync();

            return usersInClass.Select(u => new Student
            {
                Id = u.Id ?? string.Empty,
                FullName = u.FullName ?? "Unknown Student",
                Email = u.Email ?? string.Empty
            }).ToList();
        }

        public async Task<List<StudentRecord>> GetStudentsByClassCodeAsync(string classCode)
        {
            if (string.IsNullOrEmpty(classCode)) return new List<StudentRecord>();
            var classItem = await _classes.Find(c => c.ClassCode == classCode).FirstOrDefaultAsync();
            if (classItem == null) return new List<StudentRecord>();

            var joined = await GetStudentsByClassIdAsync(classItem.Id);
            var roster = joined.Select(s => new StudentRecord
            {
                Id = s.Id,
                ClassId = classItem.Id,
                StudentName = s.FullName,
                StudentEmail = s.Email,
                Status = "Active",
                Grade = 0.0
            }).ToList();

            List<string> enrolledEmails;
            try
            {
                enrolledEmails = await GetStudentEmailsForClassAsync(classItem);
            }
            catch
            {
                enrolledEmails = new List<string>();
            }

            var seen = new HashSet<string>(
                roster.Where(r => !string.IsNullOrWhiteSpace(r.StudentEmail)).Select(r => r.StudentEmail),
                StringComparer.OrdinalIgnoreCase);

            foreach (var email in enrolledEmails.Where(e => !string.IsNullOrWhiteSpace(e)))
            {
                var em = email.Trim();
                if (seen.Contains(em)) continue;
                seen.Add(em);

                User? portalUser = null;
                try
                {
                    portalUser = await GetUserByEmailAsync(em);
                }
                catch
                {
                    portalUser = null;
                }

                EnrollmentStudent? enr = null;
                try
                {
                    enr = await GetEnrollmentStudentByEmailAsync(em);
                }
                catch
                {
                    enr = null;
                }

                string id;
                string name;
                if (portalUser != null && !string.IsNullOrWhiteSpace(portalUser.Id))
                {
                    id = portalUser.Id;
                    name = !string.IsNullOrWhiteSpace(portalUser.FullName)
                        ? portalUser.FullName
                        : string.Join(" ", new[] { portalUser.FirstName, portalUser.MiddleName, portalUser.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (string.IsNullOrWhiteSpace(name)) name = em;
                }
                else if (enr != null)
                {
                    id = string.IsNullOrWhiteSpace(enr.Id) ? em : enr.Id;
                    var nameParts = new[] { enr.FirstName, enr.MiddleName, enr.LastName }.Where(x => !string.IsNullOrWhiteSpace(x));
                    name = string.Join(" ", nameParts);
                    if (string.IsNullOrWhiteSpace(name))
                        name = string.IsNullOrWhiteSpace(enr.Username) ? em : enr.Username;
                }
                else
                {
                    id = em;
                    name = em;
                }

                roster.Add(new StudentRecord
                {
                    Id = id,
                    ClassId = classItem.Id,
                    StudentName = name,
                    StudentEmail = em,
                    Status = "Not joined",
                    Grade = 0.0
                });
            }

            var seatMap = classItem.SeatAssignmentsByStudentKey;
            return OrderRosterBySeatAssignments(roster, seatMap);
        }

        // ---------------- CONTENT ----------------
        public async Task InsertContentAsync(ContentItem content)
        {
            Console.WriteLine($"Inserting content - Type: {content.Type}, ClassId: {content.ClassId}, Title: {content.Title}");

            if (string.IsNullOrEmpty(content.Id))
                content.Id = ObjectId.GenerateNewId().ToString();

            await _contentCollection.InsertOneAsync(content);
        }

        public async Task<List<UploadItem>> GetRecentUploadsByClassIdAsync(string classId)
        {
            return await _uploadCollection.Find(u => u.ClassId == classId)
                .SortByDescending(u => u.UploadedAt)
                .Limit(5)
                .ToListAsync();
        }

        public async Task<List<ContentItem>> GetContentsByClassIdAsync(string classId)
        {
            return await GetContentsForClassAsync(classId, null);
        }

        /// <summary>
        /// Loads class content where <see cref="ContentItem.ClassId"/> is either the class MongoDB id
        /// or the human-readable class code (legacy / inconsistent writes).
        /// </summary>
        public async Task<List<ContentItem>> GetContentsForClassAsync(string? classId, string? classCode)
        {
            // ContentItem.ClassId uses [BsonRepresentation(ObjectId)]. Using Filter.Eq(c => c.ClassId, classCode)
            // makes the driver call ObjectId.Parse(classCode), which throws for real class codes (e.g. "8GBP2M").
            // Use raw BSON for string ClassId matches; ObjectId for normal class document ids.
            var filters = new List<FilterDefinition<ContentItem>>();

            if (!string.IsNullOrWhiteSpace(classId))
            {
                var idTrim = classId.Trim();
                if (ObjectId.TryParse(idTrim, out var classOid))
                {
                    filters.Add(new BsonDocumentFilterDefinition<ContentItem>(
                        new BsonDocument("ClassId", classOid)));
                }
                else
                {
                    filters.Add(new BsonDocumentFilterDefinition<ContentItem>(
                        new BsonDocument("ClassId", idTrim)));
                }
            }

            if (!string.IsNullOrWhiteSpace(classCode))
            {
                var codeTrim = classCode.Trim();
                var sameAsIdBranch = !string.IsNullOrWhiteSpace(classId)
                    && string.Equals(codeTrim, classId.Trim(), StringComparison.Ordinal);
                if (!sameAsIdBranch)
                {
                    filters.Add(new BsonDocumentFilterDefinition<ContentItem>(
                        new BsonDocument("ClassId", codeTrim)));
                }
            }

            if (filters.Count == 0)
                return new List<ContentItem>();

            var filter = filters.Count == 1
                ? filters[0]
                : Builders<ContentItem>.Filter.Or(filters);

            return await _contentCollection.Find(filter)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<ContentItem>> GetContentsByClassCodeAsync(string classCode)
        {
            var classItem = await GetClassByCodeAsync(classCode);
            if (classItem == null)
                return new List<ContentItem>();

            return await GetContentsForClassAsync(classItem.Id, classItem.ClassCode);
        }

        // ---------------- CONTENT MANAGEMENT ----------------
        public async Task<ContentItem?> GetContentByIdAsync(string contentId)
        {
            return await _contentCollection.Find(c => c.Id == contentId).FirstOrDefaultAsync();
        }

        public async Task UpdateContentAsync(ContentItem content)
        {
            var filter = Builders<ContentItem>.Filter.Eq(c => c.Id, content.Id);
            var update = Builders<ContentItem>.Update
                .Set(c => c.Title, content.Title)
                .Set(c => c.Description, content.Description)
                .Set(c => c.LinkUrl, content.LinkUrl)
                .Set(c => c.Deadline, content.Deadline)
                .Set(c => c.AllowSubmissionsPastDeadline, content.AllowSubmissionsPastDeadline)
                .Set(c => c.Attachments, content.Attachments)
                .Set(c => c.LinkedLibraryEbookIds, content.LinkedLibraryEbookIds)
                .Set(c => c.MetaText, content.MetaText)
                .Set(c => c.MaxGrade, content.MaxGrade)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);

            var result = await _contentCollection.UpdateOneAsync(filter, update);

            if (result.ModifiedCount == 0)
                throw new Exception("Could not save content");
        }

        public async Task DeleteContentAsync(string contentId)
        {
            await _contentCollection.DeleteOneAsync(c => c.Id == contentId);
        }

        // ---------------- FILE/UPLOAD MANAGEMENT ----------------
        public async Task<List<UploadItem>> GetUploadsByContentIdAsync(string contentId)
        {
            return await _uploadCollection
                .Find(u => u.ContentId == contentId)
                .SortByDescending(u => u.UploadedAt)
                .ToListAsync();
        }

        public async Task<UploadItem?> GetUploadByFileNameAsync(string fileName, string contentId)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;

            if (!string.IsNullOrWhiteSpace(contentId))
            {
                var exact = await _uploadCollection
                    .Find(u => u.FileName == fileName && u.ContentId == contentId)
                    .FirstOrDefaultAsync();
                if (exact != null) return exact;

                var content = await GetContentByIdAsync(contentId);
                if (content != null && !string.IsNullOrWhiteSpace(content.ClassId))
                {
                    return await _uploadCollection
                        .Find(u => u.FileName == fileName && u.ClassId == content.ClassId)
                        .SortByDescending(u => u.UploadedAt)
                        .FirstOrDefaultAsync();
                }
            }

            return null;
        }

        public async Task<List<UploadItem>> GetUploadsByClassIdAsync(string classId)
        {
            return await _uploadCollection
                .Find(u => u.ClassId == classId)
                .SortByDescending(u => u.UploadedAt)
                .ToListAsync();
        }

        public async Task InsertUploadAsync(UploadItem upload)
        {
            if (string.IsNullOrEmpty(upload.Id))
                upload.Id = ObjectId.GenerateNewId().ToString();

            await _uploadCollection.InsertOneAsync(upload);
        }

        public async Task DeleteUploadAsync(string uploadId)
        {
            var filter = Builders<UploadItem>.Filter.Eq(u => u.Id, uploadId);
            await _uploadCollection.DeleteOneAsync(filter);
        }

        public async Task DeleteUploadsByContentIdAsync(string contentId)
        {
            var filter = Builders<UploadItem>.Filter.Eq(u => u.ContentId, contentId);
            await _uploadCollection.DeleteManyAsync(filter);
        }

        public async Task UpdateUploadAsync(UploadItem upload)
        {
            var filter = Builders<UploadItem>.Filter.Eq(u => u.Id, upload.Id);
            await _uploadCollection.ReplaceOneAsync(filter, upload);
        }

        // ---------------- MATERIAL MANAGEMENT ----------------
        public async Task<List<string>> GetRecentMaterialsByClassIdAsync(string classId)
        {
            var materials = await _contentCollection
                .Find(c => c.ClassId == classId && c.Type == "material")
                .SortByDescending(c => c.CreatedAt)
                .Limit(5)
                .ToListAsync();

            return materials.Select(m => m.Title).ToList();
        }

        public async Task<List<ContentItem>> GetMaterialsByClassIdAsync(string classId)
        {
            return await _contentCollection
                .Find(c => c.ClassId == classId && c.Type == "material")
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        // ---------------- TASKS ----------------
        public async Task<TaskItem?> GetTaskByIdAsync(string taskId)
        {
            return await _taskCollection.Find(t => t.Id == taskId).FirstOrDefaultAsync();
        }

        public async Task<List<TaskItem>> GetTasksByClassIdAsync(string classId)
        {
            return await _taskCollection.Find(t => t.ClassId == classId)
                .SortByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task UpdateTaskAsync(TaskItem task)
        {
            var filter = Builders<TaskItem>.Filter.Eq(t => t.Id, task.Id);
            await _taskCollection.ReplaceOneAsync(filter, task);
        }

        public async Task DeleteTaskAsync(string taskId)
        {
            var filter = Builders<TaskItem>.Filter.Eq(t => t.Id, taskId);
            await _taskCollection.DeleteOneAsync(filter);
        }

        public async Task InsertTaskAsync(TaskItem task)
        {
            if (string.IsNullOrEmpty(task.Id))
                task.Id = ObjectId.GenerateNewId().ToString();

            await _taskCollection.InsertOneAsync(task);
        }

        // ---------------- TASK SUBMISSIONS ----------------
        public async Task<List<Submission>> GetTaskSubmissionsAsync(string taskId)
        {
            try
            {
                return await _submissionsCollection
                    .Find(s => s.TaskId == taskId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting task submissions: {ex.Message}");
                return new List<Submission>();
            }
        }

        public async Task<Submission?> GetSubmissionByStudentAndTaskAsync(string studentId, string taskId)
        {
            return await _submissionsCollection
                .Find(s => s.StudentId == studentId && s.TaskId == taskId)
                .FirstOrDefaultAsync();
        }

        public async Task<Submission?> GetSubmissionByIdAsync(string submissionId)
        {
            try
            {
                return await _submissionsCollection
                    .Find(s => s.Id == submissionId)
                    .FirstOrDefaultAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<Submission> CreateOrUpdateSubmissionAsync(Submission submission)
        {
            if (string.IsNullOrEmpty(submission.Id))
            {
                // Create new submission
                submission.Id = ObjectId.GenerateNewId().ToString();
                submission.CreatedAt = DateTime.UtcNow;
                submission.UpdatedAt = DateTime.UtcNow;
                await _submissionsCollection.InsertOneAsync(submission);
            }
            else
            {
                // Update existing submission
                submission.UpdatedAt = DateTime.UtcNow;
                var filter = Builders<Submission>.Filter.Eq(s => s.Id, submission.Id);
                await _submissionsCollection.ReplaceOneAsync(filter, submission);
            }

            return submission;
        }

        public async Task<bool> UpdateSubmissionStatusAsync(string submissionId, bool isApproved, bool hasPassed, string grade, string feedback)
        {
            var filter = Builders<Submission>.Filter.Eq(s => s.Id, submissionId);
            var update = Builders<Submission>.Update
                .Set(s => s.IsApproved, isApproved)
                .Set(s => s.HasPassed, hasPassed)
                .Set(s => s.Grade, grade)
                .Set(s => s.TeacherRemarks, feedback)
                // Keep legacy field updated for older readers.
                .Set(s => s.Feedback, feedback)
                .Set(s => s.ApprovedDate, isApproved ? DateTime.UtcNow : (DateTime?)null)
                .Set(s => s.UpdatedAt, DateTime.UtcNow);

            var result = await _submissionsCollection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<int> GetSubmissionCountAsync(string taskId, bool submittedOnly = true)
        {
            var filter = submittedOnly
                ? Builders<Submission>.Filter.Eq(s => s.TaskId, taskId) & Builders<Submission>.Filter.Eq(s => s.Submitted, true)
                : Builders<Submission>.Filter.Eq(s => s.TaskId, taskId);

            return (int)await _submissionsCollection.CountDocumentsAsync(filter);
        }

        public async Task<int> GetApprovedSubmissionCountAsync(string taskId)
        {
            return (int)await _submissionsCollection
                .CountDocumentsAsync(s => s.TaskId == taskId && s.IsApproved == true);
        }

        public async Task<List<StudentPortal.Models.AdminTask.TaskCommentItem>> GetTaskCommentsAsync(string taskId)
        {
            try
            {
                return await _taskCommentsCollection
                    .Find(c => c.TaskId == taskId)
                    .SortByDescending(c => c.CreatedAt)
                    .ToListAsync();
            }
            catch
            {
                return new List<StudentPortal.Models.AdminTask.TaskCommentItem>();
            }
        }

        public async Task<StudentPortal.Models.AdminTask.TaskCommentItem?> AddTaskCommentAsync(string taskId, string classId, string authorEmail, string authorName, string role, string text)
        {
            var item = new StudentPortal.Models.AdminTask.TaskCommentItem
            {
                TaskId = taskId,
                ClassId = classId,
                AuthorEmail = authorEmail,
                AuthorName = authorName,
                Role = role,
                Text = text,
                CreatedAt = DateTime.UtcNow,
                Replies = new List<StudentPortal.Models.AdminTask.TaskReplyItem>()
            };
            await _taskCommentsCollection.InsertOneAsync(item);
            return item;
        }

        public async Task<StudentPortal.Models.AdminTask.TaskCommentItem?> AddTaskReplyAsync(string commentId, string authorEmail, string authorName, string role, string text)
        {
            var reply = new StudentPortal.Models.AdminTask.TaskReplyItem
            {
                AuthorEmail = authorEmail,
                AuthorName = authorName,
                Role = role,
                Text = text,
                CreatedAt = DateTime.UtcNow
            };

            var filter = Builders<StudentPortal.Models.AdminTask.TaskCommentItem>.Filter.Eq(c => c.Id, commentId);
            var update = Builders<StudentPortal.Models.AdminTask.TaskCommentItem>.Update.Push(c => c.Replies, reply);
            await _taskCommentsCollection.UpdateOneAsync(filter, update);
            return await _taskCommentsCollection.Find(filter).FirstOrDefaultAsync();
        }

        // ---------------- JOIN REQUESTS ----------------
        public async Task<List<JoinRequest>> GetJoinRequestsByClassCodeAsync(string classCode)
        {
            return await _joinRequestsCollection.Find(j => j.ClassCode == classCode && j.Status == "Pending")
                .SortByDescending(j => j.RequestedAt)
                .ToListAsync();
        }

        public async Task<JoinRequest?> GetJoinRequestByIdAsync(string requestId)
        {
            return await _joinRequestsCollection.Find(j => j.Id == requestId).FirstOrDefaultAsync();
        }

        public async Task CreateJoinRequestAsync(JoinRequest req)
        {
            await _joinRequestsCollection.InsertOneAsync(req);
        }

        public async Task UpdateJoinRequestAsync(JoinRequest joinRequest)
        {
            var filter = Builders<JoinRequest>.Filter.Eq(r => r.Id, joinRequest.Id);
            await _joinRequestsCollection.ReplaceOneAsync(filter, joinRequest);
        }

        public async Task RemoveJoinRequest(string requestId)
        {
            var filter = Builders<JoinRequest>.Filter.Eq(r => r.Id, requestId);
            await _joinRequestsCollection.DeleteOneAsync(filter);
        }

        public async Task<bool> JoinRequestExistsAsync(string email, string classCode)
        {
            var count = await _joinRequestsCollection
                .Find(j => j.StudentEmail == email && j.ClassCode == classCode && j.Status == "Pending")
                .CountDocumentsAsync();

            return count > 0;
        }

        public async Task<List<JoinRequest>> GetJoinRequestsByEmailAsync(string email)
        {
            return await _joinRequestsCollection
                .Find(j => j.StudentEmail == email && j.Status == "Pending")
                .SortByDescending(j => j.RequestedAt)
                .ToListAsync();
        }

        public async Task ApproveJoinRequestsByEmailAndClassCodeAsync(string email, string classCode)
        {
            var filter = Builders<JoinRequest>.Filter.And(
                Builders<JoinRequest>.Filter.Eq(j => j.StudentEmail, email),
                Builders<JoinRequest>.Filter.Eq(j => j.ClassCode, classCode),
                Builders<JoinRequest>.Filter.Eq(j => j.Status, "Pending")
            );
            var update = Builders<JoinRequest>.Update
                .Set(j => j.Status, "Approved")
                .Set(j => j.ApprovedAt, DateTime.UtcNow);
            await _joinRequestsCollection.UpdateManyAsync(filter, update);
        }

        public async Task RejectJoinRequestByIdAsync(string requestId)
        {
            var filter = Builders<JoinRequest>.Filter.Eq(r => r.Id, requestId);
            var update = Builders<JoinRequest>.Update
                .Set(r => r.Status, "Rejected")
                .Set(r => r.RejectedAt, DateTime.UtcNow);
            await _joinRequestsCollection.UpdateOneAsync(filter, update);
        }

        public async Task<List<JoinRequest>> GetApprovedJoinRequestsByEmailSinceAsync(string email, DateTime sinceUtc)
        {
            var filter = Builders<JoinRequest>.Filter.And(
                Builders<JoinRequest>.Filter.Eq(j => j.StudentEmail, email),
                Builders<JoinRequest>.Filter.Eq(j => j.Status, "Approved"),
                Builders<JoinRequest>.Filter.Gte(j => j.ApprovedAt, sinceUtc)
            );
            return await _joinRequestsCollection.Find(filter).ToListAsync();
        }

        public async Task<List<JoinRequest>> GetRejectedJoinRequestsByEmailSinceAsync(string email, DateTime sinceUtc)
        {
            var filter = Builders<JoinRequest>.Filter.And(
                Builders<JoinRequest>.Filter.Eq(j => j.StudentEmail, email),
                Builders<JoinRequest>.Filter.Eq(j => j.Status, "Rejected"),
                Builders<JoinRequest>.Filter.Gte(j => j.RejectedAt, sinceUtc)
            );
            return await _joinRequestsCollection.Find(filter).ToListAsync();
        }

        // ---------------- ATTENDANCE ----------------
        public async Task UpsertAttendanceRecordAsync(string classCode, string studentId, string status)
        {
            if (string.IsNullOrEmpty(classCode) || string.IsNullOrEmpty(studentId) || string.IsNullOrEmpty(status))
                throw new ArgumentException("classCode, studentId, and status are required");

            var classItem = await GetClassByCodeAsync(classCode);
            if (classItem == null)
                throw new KeyNotFoundException($"Class with code {classCode} not found.");

            var user = await _users.Find(u => u.Id == studentId).FirstOrDefaultAsync();
            if (user == null)
                throw new KeyNotFoundException($"User with id {studentId} not found.");

            var todayStart = DateTime.UtcNow.Date;
            var todayEnd = todayStart.AddDays(1);

            var filter = Builders<StudentPortal.Models.AdminDb.AttendanceRecord>.Filter.And(
                Builders<StudentPortal.Models.AdminDb.AttendanceRecord>.Filter.Eq(a => a.ClassId, classItem.Id),
                Builders<StudentPortal.Models.AdminDb.AttendanceRecord>.Filter.Eq(a => a.StudentId, studentId),
                Builders<StudentPortal.Models.AdminDb.AttendanceRecord>.Filter.Gte(a => a.Date, todayStart),
                Builders<StudentPortal.Models.AdminDb.AttendanceRecord>.Filter.Lt(a => a.Date, todayEnd)
            );

            var update = Builders<StudentPortal.Models.AdminDb.AttendanceRecord>.Update
                .Set(a => a.ClassId, classItem.Id)
                .Set(a => a.ClassCode, classCode)
                .Set(a => a.StudentId, studentId)
                .Set(a => a.StudentName, user.FullName ?? string.Empty)
                .Set(a => a.Date, DateTime.UtcNow)
                .Set(a => a.Status, status);

            var options = new UpdateOptions { IsUpsert = true };
            await _attendanceCollection.UpdateOneAsync(filter, update, options);
        }

        /// <summary>
        /// Bulk-insert generic attendance rows coming from an Excel import
        /// into the AttendanceCopy collection. The collection is created
        /// automatically by MongoDB on first insert if it does not exist.
        /// </summary>
        public async Task InsertAttendanceCopyRowsAsync(List<Dictionary<string, string>> rows)
        {
            if (rows == null || rows.Count == 0) return;

            var docs = new List<BsonDocument>();
            foreach (var row in rows)
            {
                // Convert the row key/value pairs into a BsonDocument
                var doc = new BsonDocument();
                foreach (var kvp in row)
                {
                    doc[kvp.Key] = kvp.Value ?? string.Empty;
                }
                docs.Add(doc);
            }

            await _attendanceCopyCollection.InsertManyAsync(docs);
        }

        public async Task InsertAttendanceCopyDocsAsync(List<BsonDocument> docs)
        {
            if (docs == null || docs.Count == 0) return;
            await _attendanceCopyCollection.InsertManyAsync(docs);
        }

        public async Task UpsertAttendanceCopyDocAsync(string studentId, string subject, BsonDocument doc)
        {
            if (string.IsNullOrWhiteSpace(studentId)) return;
            var filters = new List<FilterDefinition<BsonDocument>>
            {
                Builders<BsonDocument>.Filter.Eq("Student_ID", studentId)
            };
            if (!string.IsNullOrWhiteSpace(subject))
            {
                filters.Add(Builders<BsonDocument>.Filter.Eq("StandingInfo.1", subject));
            }
            var filter = filters.Count == 2
                ? Builders<BsonDocument>.Filter.And(filters)
                : filters[0];
            await _attendanceCopyCollection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true });
        }

        public async Task UpsertAttendanceCopyStandingAsync(string studentId, string fullName, BsonArray standingInfo, string subject)
        {
            if (string.IsNullOrWhiteSpace(studentId)) return;

            var filterSid = Builders<BsonDocument>.Filter.Eq("Student_ID", studentId);
            var existing = await _attendanceCopyCollection.Find(filterSid).FirstOrDefaultAsync();

            if (existing == null)
            {
                var newDoc = new BsonDocument
                {
                    { "Student_ID", studentId },
                    { "Full_Name", fullName ?? string.Empty },
                    { "StandingInfo", standingInfo }
                };
                await _attendanceCopyCollection.InsertOneAsync(newDoc);
                return;
            }

            string targetField = "StandingInfo";
            int index = 0;
            while (true)
            {
                var fieldName = index == 0 ? "StandingInfo" : $"StandingInfo{index}";
                if (existing.Contains(fieldName))
                {
                    try
                    {
                        var arr = existing[fieldName].AsBsonArray;
                        var subjExisting = arr.Count > 1 ? (arr[1].IsString ? arr[1].AsString : arr[1].ToString()) : string.Empty;
                        if (!string.IsNullOrWhiteSpace(subject) && string.Equals(subjExisting, subject, StringComparison.OrdinalIgnoreCase))
                        {
                            targetField = fieldName;
                            break;
                        }
                    }
                    catch { }
                    index++;
                    continue;
                }
                else
                {
                    targetField = fieldName;
                    break;
                }
            }

            var updateDefs = new List<UpdateDefinition<BsonDocument>>
            {
                Builders<BsonDocument>.Update.Set(targetField, standingInfo)
            };
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                updateDefs.Add(Builders<BsonDocument>.Update.Set("Full_Name", fullName));
            }

            var update = Builders<BsonDocument>.Update.Combine(updateDefs);
            await _attendanceCopyCollection.UpdateOneAsync(filterSid, update, new UpdateOptions { IsUpsert = true });
        }

        public async Task<List<StudentPortal.Models.AdminDb.AttendanceRecord>> GetAttendanceByClassCodeAsync(string classCode)
        {
            var classItem = await GetClassByCodeAsync(classCode);
            if (classItem == null) return new List<StudentPortal.Models.AdminDb.AttendanceRecord>();
            return await _attendanceCollection
                .Find(a => a.ClassId == classItem.Id)
                .SortByDescending(a => a.Date)
                .ToListAsync();
        }

        public async Task<List<StudentPortal.Models.AdminDb.AttendanceRecord>> GetAttendanceByStudentAsync(string classCode, string studentId)
        {
            var classItem = await GetClassByCodeAsync(classCode);
            if (classItem == null) return new List<StudentPortal.Models.AdminDb.AttendanceRecord>();
            return await _attendanceCollection
                .Find(a => a.ClassId == classItem.Id && a.StudentId == studentId)
                .SortByDescending(a => a.Date)
                .ToListAsync();
        }

        public async Task AddStudentToClass(string studentEmail, string classCode)
        {
            if (string.IsNullOrEmpty(studentEmail) || string.IsNullOrEmpty(classCode))
                throw new ArgumentException("Email and ClassCode are required.");

            var user = await _users.Find(u => u.Email == studentEmail).FirstOrDefaultAsync();
            if (user == null)
                throw new KeyNotFoundException($"User with email {studentEmail} not found.");

            if (user.JoinedClasses == null)
                user.JoinedClasses = new List<string>();

            if (!user.JoinedClasses.Contains(classCode))
                user.JoinedClasses.Add(classCode);

            await UpdateUserAsync(user);
        }

        public async Task<bool> RemoveStudentFromClassById(string studentId, string classId)
        {
            if (string.IsNullOrEmpty(studentId) || string.IsNullOrEmpty(classId))
                throw new ArgumentException("StudentId and ClassId are required.");

            // Get the class to find the class code
            var classItem = await GetClassByIdAsync(classId);
            if (classItem == null)
                throw new KeyNotFoundException($"Class with id {classId} not found.");

            // Get the user by student ID
            var user = await _users.Find(u => u.Id == studentId).FirstOrDefaultAsync();
            if (user == null)
                throw new KeyNotFoundException($"User with id {studentId} not found.");

            // Remove the class code from JoinedClasses
            if (user.JoinedClasses != null && user.JoinedClasses.Contains(classItem.ClassCode))
            {
                var filter = Builders<User>.Filter.Eq(u => u.Id, studentId);
                var update = Builders<User>.Update.Pull(u => u.JoinedClasses, classItem.ClassCode);
                var result = await _users.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }

            return false;
        }

        public async Task<bool> RemoveStudentFromClassByEmail(string studentEmail, string classCode)
        {
            if (string.IsNullOrEmpty(studentEmail) || string.IsNullOrEmpty(classCode))
                throw new ArgumentException("Email and ClassCode are required.");

            var user = await _users.Find(u => u.Email == studentEmail).FirstOrDefaultAsync();
            if (user == null)
                throw new KeyNotFoundException($"User with email {studentEmail} not found.");

            // Remove the class code from JoinedClasses
            if (user.JoinedClasses != null && user.JoinedClasses.Contains(classCode))
            {
                var filter = Builders<User>.Filter.Eq(u => u.Email, studentEmail);
                var update = Builders<User>.Update.Pull(u => u.JoinedClasses, classCode);
                var result = await _users.UpdateOneAsync(filter, update);
                return result.ModifiedCount > 0;
            }

            return false;
        }

        // ---------------- ASSESSMENT MANAGEMENT ----------------
        public async Task<AdminAssessment?> GetAssessmentByIdAsync(string assessmentId)
        {
            return await _database.GetCollection<AdminAssessment>("Assessments")
                .Find(a => a.Id == assessmentId)
                .FirstOrDefaultAsync();
        }

        public async Task<AdminAssessment?> GetAssessmentByClassIdAsync(string classId)
        {
            return await _database.GetCollection<AdminAssessment>("Assessments")
                .Find(a => a.ClassId == classId && a.Status == "Active")
                .SortByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<List<AdminAssessment>> GetAssessmentsByClassIdAsync(string classId)
        {
            return await _database.GetCollection<AdminAssessment>("Assessments")
                .Find(a => a.ClassId == classId)
                .SortByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<AdminAssessment> CreateAssessmentAsync(AdminAssessment assessment)
        {
            if (string.IsNullOrEmpty(assessment.Id))
                assessment.Id = ObjectId.GenerateNewId().ToString();

            assessment.CreatedAt = DateTime.UtcNow;
            assessment.UpdatedAt = DateTime.UtcNow;

            await _database.GetCollection<AdminAssessment>("Assessments")
                .InsertOneAsync(assessment);

            return assessment;
        }

        public async Task<AdminAssessment> UpdateAssessmentAsync(AdminAssessment assessment)
        {
            assessment.UpdatedAt = DateTime.UtcNow;

            var filter = Builders<AdminAssessment>.Filter.Eq(a => a.Id, assessment.Id);
            await _database.GetCollection<AdminAssessment>("Assessments")
                .ReplaceOneAsync(filter, assessment);

            return assessment;
        }

        public async Task<bool> DeleteAssessmentAsync(string assessmentId)
        {
            var result = await _database.GetCollection<AdminAssessment>("Assessments")
                .DeleteOneAsync(a => a.Id == assessmentId);

            return result.DeletedCount > 0;
        }

        // ---------------- ASSESSMENT SUBMISSIONS ----------------
        public async Task<List<AssessmentSubmission>> GetSubmissionsByAssessmentIdAsync(string assessmentId)
        {
            return await _database.GetCollection<AssessmentSubmission>("AssessmentSubmissions")
                .Find(s => s.AssessmentId == assessmentId)
                .SortBy(s => s.StudentName)
                .ToListAsync();
        }

        public async Task<AssessmentSubmission?> GetSubmissionByStudentAsync(string assessmentId, string studentId)
        {
            return await _database.GetCollection<AssessmentSubmission>("AssessmentSubmissions")
                .Find(s => s.AssessmentId == assessmentId && s.StudentId == studentId)
                .FirstOrDefaultAsync();
        }

        public async Task<AssessmentSubmission> CreateOrUpdateSubmissionAsync(AssessmentSubmission submission)
        {
            if (string.IsNullOrEmpty(submission.Id))
            {
                submission.Id = ObjectId.GenerateNewId().ToString();
                submission.CreatedAt = DateTime.UtcNow;
                await _database.GetCollection<AssessmentSubmission>("AssessmentSubmissions")
                    .InsertOneAsync(submission);
            }
            else
            {
                submission.UpdatedAt = DateTime.UtcNow;
                var filter = Builders<AssessmentSubmission>.Filter.Eq(s => s.Id, submission.Id);
                await _database.GetCollection<AssessmentSubmission>("AssessmentSubmissions")
                    .ReplaceOneAsync(filter, submission);
            }

            return submission;
        }

        public async Task<int> GetAssessmentSubmissionCountAsync(string assessmentId, string status = "Submitted")
        {
            return (int)await _database.GetCollection<AssessmentSubmission>("AssessmentSubmissions")
                .Find(s => s.AssessmentId == assessmentId && s.Status == status)
                .CountDocumentsAsync();
        }

        // ---------------- DEBUG METHODS ----------------
        public async Task<List<ClassItem>> DebugGetAllClassesAsync()
        {
            var classes = await _classes.Find(_ => true).ToListAsync();
            Console.WriteLine($"Total classes in database: {classes.Count}");
            foreach (var cls in classes)
            {
                Console.WriteLine($"Class: {cls.ClassCode} - {cls.SubjectName} - ID: {cls.Id}");
            }
            return classes;
        }

        // Add this method to your MongoDbService class
        public async Task DebugSubmissions(string taskId)
        {
            try
            {
                Console.WriteLine($"=== DEBUG SUBMISSIONS FOR TASK: {taskId} ===");

                // Get all submissions for this task
                var submissions = await _submissionsCollection
                    .Find(s => s.TaskId == taskId)
                    .ToListAsync();

                Console.WriteLine($"Found {submissions.Count} submissions for this task:");

                foreach (var sub in submissions)
                {
                    Console.WriteLine($"- Submission ID: {sub.Id}");
                    Console.WriteLine($"  Student: {sub.StudentName} ({sub.StudentId})");
                    Console.WriteLine($"  Email: {sub.StudentEmail}");
                    Console.WriteLine($"  Submitted: {sub.Submitted}");
                    Console.WriteLine($"  SubmittedAt: {sub.SubmittedAt}");
                    Console.WriteLine($"  File: {sub.FileName} (Size: {sub.FileSize})");
                    Console.WriteLine($"  FileUrl: {sub.FileUrl}");
                    Console.WriteLine($"  Approved: {sub.IsApproved}");
                    Console.WriteLine($"  Passed: {sub.HasPassed}");
                    Console.WriteLine($"  Grade: {sub.Grade}");
                    Console.WriteLine($"  PrivateComment: {sub.PrivateComment}");
                    Console.WriteLine($"  TeacherRemarks: {sub.TeacherRemarks}");
                    Console.WriteLine($"  Feedback(Legacy): {sub.Feedback}");
                    Console.WriteLine($"  Created: {sub.CreatedAt}");
                    Console.WriteLine($"  Updated: {sub.UpdatedAt}");
                    Console.WriteLine($"  ---");
                }

                if (submissions.Count == 0)
                {
                    Console.WriteLine("No submissions found for this task.");
                }

                Console.WriteLine("=== END DEBUG ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== DEBUG ERROR ===");
                Console.WriteLine($"Error in DebugSubmissions: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                Console.WriteLine($"=== END DEBUG ERROR ===");
            }
        }

        public async Task<bool> UpdateSubmissionStatusAsync(string studentId, string taskId, bool isApproved, bool hasPassed, string grade, string feedback)
        {
            try
            {
                var submission = await GetSubmissionByStudentAndTaskAsync(studentId, taskId);
                if (submission == null)
                    return false;

                submission.IsApproved = isApproved;
                submission.HasPassed = hasPassed;
                submission.Grade = grade;
                submission.TeacherRemarks = feedback;
                // Keep legacy field updated for older readers.
                submission.Feedback = feedback;
                submission.ApprovedDate = isApproved ? DateTime.UtcNow : (DateTime?)null;
                submission.UpdatedAt = DateTime.UtcNow;

                await CreateOrUpdateSubmissionAsync(submission);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateSubmissionStatusAsync: {ex.Message}");
                return false;
            }
        }
        public async Task AddAntiCheatLogAsync(StudentPortal.Models.AdminDb.AntiCheatLog log)
        {
            await _antiCheatLogsCollection.InsertOneAsync(log);
        }

        public async Task<bool> HasRecentDuplicateAntiCheatLogAsync(
            string classId,
            string contentId,
            string studentId,
            string eventType,
            string details,
            int withinSeconds = 2)
        {
            if (string.IsNullOrWhiteSpace(classId) || string.IsNullOrWhiteSpace(contentId))
                return false;
            var cutoff = DateTime.UtcNow.AddSeconds(-withinSeconds);
            var filter = Builders<StudentPortal.Models.AdminDb.AntiCheatLog>.Filter.Eq(l => l.ClassId, classId)
                       & Builders<StudentPortal.Models.AdminDb.AntiCheatLog>.Filter.Eq(l => l.ContentId, contentId)
                       & Builders<StudentPortal.Models.AdminDb.AntiCheatLog>.Filter.Eq(l => l.StudentId, studentId)
                       & Builders<StudentPortal.Models.AdminDb.AntiCheatLog>.Filter.Eq(l => l.EventType, eventType ?? string.Empty)
                       & Builders<StudentPortal.Models.AdminDb.AntiCheatLog>.Filter.Eq(l => l.Details, details ?? string.Empty)
                       & Builders<StudentPortal.Models.AdminDb.AntiCheatLog>.Filter.Gte(l => l.LogTimeUtc, cutoff);
            return await _antiCheatLogsCollection.Find(filter).Limit(1).AnyAsync();
        }

        public async Task<List<StudentPortal.Models.AdminDb.AntiCheatLog>> GetAntiCheatLogsAsync(string classId, string contentId)
        {
            if (string.IsNullOrWhiteSpace(classId) || string.IsNullOrWhiteSpace(contentId))
                return new List<StudentPortal.Models.AdminDb.AntiCheatLog>();
            var filter = Builders<StudentPortal.Models.AdminDb.AntiCheatLog>.Filter.Eq(l => l.ClassId, classId)
                       & Builders<StudentPortal.Models.AdminDb.AntiCheatLog>.Filter.Eq(l => l.ContentId, contentId);
            return await _antiCheatLogsCollection.Find(filter).ToListAsync();
        }

        /// <summary>
        /// Students whose summed integrity events for this assessment meet or exceed <see cref="AssessmentAntiCheatRules.IntegrityLockThreshold"/>.
        /// </summary>
        public async Task<List<CheatLockedStudentRow>> GetIntegrityThresholdStudentRowsAsync(string classId, string contentId)
        {
            var logs = await GetAntiCheatLogsAsync(classId, contentId);
            if (logs == null || logs.Count == 0) return new List<CheatLockedStudentRow>();

            var groups = new Dictionary<string, List<StudentPortal.Models.AdminDb.AntiCheatLog>>(StringComparer.Ordinal);
            foreach (var log in logs)
            {
                if (string.IsNullOrEmpty(log.StudentId) && string.IsNullOrWhiteSpace(log.StudentEmail)) continue;
                var key = !string.IsNullOrEmpty(log.StudentId)
                    ? "id:" + log.StudentId
                    : "em:" + (log.StudentEmail ?? "").Trim().ToLowerInvariant();
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<StudentPortal.Models.AdminDb.AntiCheatLog>();
                    groups[key] = list;
                }
                list.Add(log);
            }

            var result = new List<CheatLockedStudentRow>();
            foreach (var kv in groups)
            {
                var list = kv.Value;
                var total = list.Sum(l => l.EventCount > 0 ? l.EventCount : 1);

                var sample = list.OrderByDescending(l => l.LogTimeUtc).First();
                var sid = list.FirstOrDefault(l => !string.IsNullOrEmpty(l.StudentId))?.StudentId ?? "";
                var email = sample.StudentEmail ?? "";
                if (string.IsNullOrEmpty(sid) && !string.IsNullOrWhiteSpace(email))
                {
                    var u = await GetUserByEmailAsync(email);
                    sid = u?.Id ?? "";
                }

                var name = sample.StudentName ?? "";
                if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(email))
                {
                    var uName = await GetUserByEmailAsync(email);
                    if (!string.IsNullOrWhiteSpace(uName?.FullName)) name = uName.FullName;
                }

                StudentPortal.Models.AdminDb.AssessmentUnlock? unlock = null;
                if (!string.IsNullOrEmpty(sid))
                    unlock = await GetAssessmentUnlockAsync(classId, contentId, sid);
                // For display/decisioning: when unlocked, only count events since unlock.
                var totalForLock = AssessmentAntiCheatRules.SumIntegrityEventsForLock(list, sid, email, unlock);
                // Keep row visible if teacher restored access (so "Remove override" still works),
                // otherwise only show students currently at/above threshold.
                if (totalForLock < AssessmentAntiCheatRules.IntegrityLockThreshold && !(unlock != null && unlock.Unlocked)) continue;

                var canTeacherRestoreIntegrity = true;
                if (!string.IsNullOrEmpty(sid))
                {
                    var assessRes = await GetAssessmentResultAsync(classId, contentId, sid);
                    canTeacherRestoreIntegrity = (assessRes?.TeacherIntegrityRestoreCount ?? 0) < 1;
                }

                result.Add(new CheatLockedStudentRow
                {
                    StudentId = sid,
                    StudentEmail = email,
                    StudentName = string.IsNullOrWhiteSpace(name) ? (email ?? "Student") : name,
                    IntegrityEventTotal = totalForLock,
                    TeacherRestoredAccess = unlock != null && unlock.Unlocked,
                    RestoredByEmail = unlock?.UnlockedBy,
                    RestoredAtUtc = unlock?.UnlockedAtUtc,
                    CanTeacherRestoreIntegrity = canTeacherRestoreIntegrity
                });
            }

            return result.OrderByDescending(r => r.IntegrityEventTotal).ToList();
        }

        public async Task<StudentPortal.Models.AdminDb.AssessmentUnlock?> GetAssessmentUnlockAsync(string classId, string contentId, string studentId)
        {
            var filter = Builders<StudentPortal.Models.AdminDb.AssessmentUnlock>.Filter.And(
                Builders<StudentPortal.Models.AdminDb.AssessmentUnlock>.Filter.Eq(u => u.ClassId, classId),
                Builders<StudentPortal.Models.AdminDb.AssessmentUnlock>.Filter.Eq(u => u.ContentId, contentId),
                Builders<StudentPortal.Models.AdminDb.AssessmentUnlock>.Filter.Eq(u => u.StudentId, studentId)
            );
            return await _assessmentUnlocksCollection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task SetAssessmentUnlockAsync(string classId, string classCode, string contentId, string studentId, string studentEmail, bool unlocked, string? unlockedBy)
        {
            var filter = Builders<StudentPortal.Models.AdminDb.AssessmentUnlock>.Filter.And(
                Builders<StudentPortal.Models.AdminDb.AssessmentUnlock>.Filter.Eq(u => u.ClassId, classId),
                Builders<StudentPortal.Models.AdminDb.AssessmentUnlock>.Filter.Eq(u => u.ContentId, contentId),
                Builders<StudentPortal.Models.AdminDb.AssessmentUnlock>.Filter.Eq(u => u.StudentId, studentId)
            );
            var update = Builders<StudentPortal.Models.AdminDb.AssessmentUnlock>.Update
                .Set(u => u.ClassId, classId)
                .Set(u => u.ClassCode, classCode ?? string.Empty)
                .Set(u => u.ContentId, contentId)
                .Set(u => u.StudentId, studentId)
                .Set(u => u.StudentEmail, studentEmail ?? string.Empty)
                .Set(u => u.Unlocked, unlocked)
                .Set(u => u.UnlockedBy, unlockedBy)
                .Set(u => u.UnlockedAtUtc, unlocked ? DateTime.UtcNow : (DateTime?)null);
            await _assessmentUnlocksCollection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        }

        public async Task ClearAssessmentUnlockAsync(string classId, string contentId, string studentId)
        {
            var filter = Builders<StudentPortal.Models.AdminDb.AssessmentUnlock>.Filter.And(
                Builders<StudentPortal.Models.AdminDb.AssessmentUnlock>.Filter.Eq(u => u.ClassId, classId),
                Builders<StudentPortal.Models.AdminDb.AssessmentUnlock>.Filter.Eq(u => u.ContentId, contentId),
                Builders<StudentPortal.Models.AdminDb.AssessmentUnlock>.Filter.Eq(u => u.StudentId, studentId)
            );
            await _assessmentUnlocksCollection.DeleteOneAsync(filter);
        }

        public async Task<StudentPortal.Models.StudentDb.AssessmentResult?> GetAssessmentResultAsync(string classId, string contentId, string studentId)
        {
            if (string.IsNullOrEmpty(classId) || string.IsNullOrEmpty(contentId) || string.IsNullOrEmpty(studentId)) return null;
            return await _assessmentResultsCollection
                .Find(r => r.ClassId == classId && r.ContentId == contentId && r.StudentId == studentId)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Resolves the student's assessment row by <paramref name="user"/>.<c>Id</c> when present, otherwise by email
        /// (for accounts where BSON id is missing so integrity lock rows still match after navigation).
        /// </summary>
        public async Task<StudentPortal.Models.StudentDb.AssessmentResult?> GetAssessmentResultForStudentAsync(string classId, string contentId, User? user)
        {
            if (string.IsNullOrEmpty(classId) || string.IsNullOrEmpty(contentId) || user == null) return null;
            if (!string.IsNullOrEmpty(user.Id))
                return await GetAssessmentResultAsync(classId, contentId, user.Id);
            if (string.IsNullOrWhiteSpace(user.Email)) return null;
            var escaped = Regex.Escape(user.Email.Trim());
            var filter = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.And(
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ClassId, classId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ContentId, contentId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Regex(
                    r => r.StudentEmail,
                    new BsonRegularExpression($"^{escaped}$", "i")));
            return await _assessmentResultsCollection.Find(filter).FirstOrDefaultAsync();
        }

        /// <summary>
        /// Persists integrity lock on the student's assessment result so reopening the quiz cannot bypass
        /// the lock if anti-cheat log queries or client state are inconsistent.
        /// </summary>
        public async Task SetAssessmentIntegrityLockAsync(string classId, string classCode, string contentId, string studentId, string studentEmail)
        {
            if (string.IsNullOrEmpty(classId) || string.IsNullOrEmpty(contentId) || string.IsNullOrEmpty(studentId)) return;
            var filter = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.And(
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ClassId, classId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ContentId, contentId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.StudentId, studentId)
            );
            var now = DateTime.UtcNow;
            var update = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Update
                .SetOnInsert(r => r.Id, ObjectId.GenerateNewId().ToString())
                .SetOnInsert(r => r.ClassId, classId)
                .SetOnInsert(r => r.ClassCode, classCode ?? string.Empty)
                .SetOnInsert(r => r.ContentId, contentId)
                .SetOnInsert(r => r.StudentId, studentId)
                .SetOnInsert(r => r.Status, "pending")
                .Set(r => r.StudentEmail, studentEmail ?? string.Empty)
                .Set(r => r.IntegrityLockedAtUtc, now);
            await _assessmentResultsCollection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        }

        /// <summary>
        /// Same as <see cref="SetAssessmentIntegrityLockAsync"/> but supports students without a Mongo user id by keying the upsert on email.
        /// </summary>
        public async Task SetAssessmentIntegrityLockForUserAsync(string classId, string classCode, string contentId, User user)
        {
            if (string.IsNullOrEmpty(classId) || string.IsNullOrEmpty(contentId) || user == null) return;
            if (!string.IsNullOrEmpty(user.Id))
            {
                await SetAssessmentIntegrityLockAsync(classId, classCode, contentId, user.Id, user.Email ?? string.Empty);
                return;
            }

            if (string.IsNullOrWhiteSpace(user.Email)) return;
            var escaped = Regex.Escape(user.Email.Trim());
            var filter = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.And(
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ClassId, classId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ContentId, contentId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Regex(
                    r => r.StudentEmail,
                    new BsonRegularExpression($"^{escaped}$", "i")));
            var now = DateTime.UtcNow;
            var update = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Update
                .SetOnInsert(r => r.Id, ObjectId.GenerateNewId().ToString())
                .SetOnInsert(r => r.ClassId, classId)
                .SetOnInsert(r => r.ClassCode, classCode ?? string.Empty)
                .SetOnInsert(r => r.ContentId, contentId)
                .SetOnInsert(r => r.StudentId, string.Empty)
                .SetOnInsert(r => r.Status, "pending")
                .Set(r => r.StudentEmail, user.Email.Trim())
                .Set(r => r.IntegrityLockedAtUtc, now);
            await _assessmentResultsCollection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        }

        /// <summary>
        /// Records one teacher &quot;Restore integrity access&quot; for this student/assessment (max one restore per policy).
        /// </summary>
        public async Task IncrementTeacherIntegrityRestoreCountAsync(string classId, string classCode, string contentId, string studentId, string studentEmail)
        {
            if (string.IsNullOrEmpty(classId) || string.IsNullOrEmpty(contentId) || string.IsNullOrEmpty(studentId)) return;
            var filter = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.And(
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ClassId, classId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ContentId, contentId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.StudentId, studentId)
            );
            var update = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Update
                .SetOnInsert(r => r.Id, ObjectId.GenerateNewId().ToString())
                .SetOnInsert(r => r.ClassId, classId)
                .SetOnInsert(r => r.ClassCode, classCode ?? string.Empty)
                .SetOnInsert(r => r.ContentId, contentId)
                .SetOnInsert(r => r.StudentId, studentId)
                .SetOnInsert(r => r.Status, "pending")
                .Set(r => r.StudentEmail, studentEmail ?? string.Empty)
                .Inc(r => r.TeacherIntegrityRestoreCount, 1);
            await _assessmentResultsCollection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        }

        public async Task UpsertAssessmentSubmittedAsync(string classId, string classCode, string contentId, string studentId, string studentEmail)
        {
            var filter = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.And(
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ClassId, classId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ContentId, contentId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.StudentId, studentId)
            );
            var update = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Update
                .SetOnInsert(r => r.Id, ObjectId.GenerateNewId().ToString())
                .Set(r => r.ClassId, classId)
                .Set(r => r.ClassCode, classCode)
                .Set(r => r.ContentId, contentId)
                .Set(r => r.StudentId, studentId)
                .Set(r => r.StudentEmail, studentEmail)
                .Set(r => r.SubmittedAt, DateTime.UtcNow)
                .Set(r => r.Status, "submitted");
            var options = new UpdateOptions { IsUpsert = true };
            await _assessmentResultsCollection.UpdateOneAsync(filter, update, options);
        }

        public async Task MarkAssessmentDoneAsync(string classId, string classCode, string contentId, string studentId, string studentEmail)
        {
            var filter = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.And(
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ClassId, classId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ContentId, contentId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.StudentId, studentId)
            );
            var update = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Update
                .SetOnInsert(r => r.Id, ObjectId.GenerateNewId().ToString())
                .Set(r => r.ClassId, classId)
                .Set(r => r.ClassCode, classCode)
                .Set(r => r.ContentId, contentId)
                .Set(r => r.StudentId, studentId)
                .Set(r => r.StudentEmail, studentEmail)
                .Set(r => r.SubmittedAt, DateTime.UtcNow)
                .Set(r => r.Status, "done");
            var options = new UpdateOptions { IsUpsert = true };
            await _assessmentResultsCollection.UpdateOneAsync(filter, update, options);
        }

        public async Task ResetAssessmentResultAsync(string classId, string contentId, string studentId)
        {
            if (string.IsNullOrEmpty(classId) || string.IsNullOrEmpty(contentId) || string.IsNullOrEmpty(studentId)) return;
            var filter = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.And(
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ClassId, classId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ContentId, contentId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.StudentId, studentId)
            );
            var update = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Update
                .Set(r => r.SubmittedAt, (DateTime?)null)
                .Set(r => r.Status, "pending")
                .Set(r => r.Score, (double?)null)
                .Set(r => r.MaxScore, (double?)null)
                .Set(r => r.IntegrityLockedAtUtc, (DateTime?)null);
            await _assessmentResultsCollection.UpdateOneAsync(filter, update);
        }
        public async Task UpdateAssessmentScoreAsync(string classId, string contentId, string studentId, double? score, double? maxScore)
        {
            var filter = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.And(
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ClassId, classId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.ContentId, contentId),
                Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Filter.Eq(r => r.StudentId, studentId)
            );
            var update = Builders<StudentPortal.Models.StudentDb.AssessmentResult>.Update
                .Set(r => r.Score, score)
                .Set(r => r.MaxScore, maxScore)
                .Set(r => r.Status, score.HasValue ? "scored" : "submitted");
            await _assessmentResultsCollection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        }
    }
}
