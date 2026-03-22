using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace StudentPortal.Models.AdminDb
{
    public class AssessmentUnlock
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string ClassId { get; set; } = string.Empty;

        public string ClassCode { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string ContentId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string StudentId { get; set; } = string.Empty;

        public string StudentEmail { get; set; } = string.Empty;

        public bool Unlocked { get; set; } = false;

        public string? UnlockedBy { get; set; }
        public DateTime? UnlockedAtUtc { get; set; }
    }
}
