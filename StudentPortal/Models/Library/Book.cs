using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StudentPortal.Models.Library
{
    [BsonIgnoreExtraElements] // Ignore extra fields that might exist in the database
    public class Book
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId _id { get; set; }

        [BsonElement("title")]
        public string Title { get; set; } = "";

        [BsonElement("author")]
        public string Author { get; set; } = "";

        [BsonElement("publisher")]
        public string Publisher { get; set; } = "";

        [BsonElement("classification_no")]
        public string ClassificationNo { get; set; } = "";

        [BsonElement("isbn")]
        public string ISBN { get; set; } = "";

        [BsonElement("subject")]
        public string Subject { get; set; } = "";

        [BsonElement("image")]
        public string Image { get; set; } = "/images/default-book.png";

        [BsonElement("total_copies")]
        public int TotalCopies { get; set; } = 1;

        [BsonElement("available_copies")]
        public int AvailableCopies { get; set; } = 1;

        [BsonElement("publication_date")]
        public DateTime? PublicationDate { get; set; }

        [BsonElement("restrictions")]
        public string? Restrictions { get; set; }

        [BsonElement("is_reference_only")]
        public bool IsReferenceOnly { get; set; } = false;

        [BsonElement("is_active")]
        public bool IsActive { get; set; } = true;

        [BsonElement("is_ebook")]
        public bool IsEBook { get; set; } = false;

        [BsonElement("ebook_file_path")]
        public string? EBookFilePath { get; set; }

        [BsonElement("ebook_file_name")]
        public string? EBookFileName { get; set; }

        [BsonElement("ebook_content_type")]
        public string? EBookContentType { get; set; }

        [BsonElement("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [BsonIgnore]
        public bool IsAvailable => AvailableCopies > 0 && !IsReferenceOnly;

        /// <summary>
        /// Library rows that should behave as eBooks in the LMS: explicit flag or legacy rows with a stored file under uploads/ebooks.
        /// </summary>
        [BsonIgnore]
        public bool EffectiveIsEBook => IsEBook || LooksLikeStoredLibraryEbookFile(EBookFilePath);

        public static bool LooksLikeStoredLibraryEbookFile(string? pathOrUrl)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl)) return false;
            var n = pathOrUrl.Trim().Replace('\\', '/');
            return n.Contains("/uploads/ebooks/", StringComparison.OrdinalIgnoreCase);
        }
    }
}

    