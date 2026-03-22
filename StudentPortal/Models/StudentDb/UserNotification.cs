using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace StudentPortal.Models.StudentDb
{
    public class UserNotification
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("type")]
        public string Type { get; set; } = string.Empty;

        [BsonElement("text")]
        public string Text { get; set; } = string.Empty;

        [BsonElement("code")]
        public string? Code { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("read")]
        public bool Read { get; set; } = false;

        [BsonElement("deleted")]
        public bool Deleted { get; set; } = false;
    }

    public class DeleteNotificationRequest
    {
        public string Id { get; set; } = string.Empty;
    }

    public class MarkReadNotificationRequest
    {
        public string Id { get; set; } = string.Empty;
    }
}
