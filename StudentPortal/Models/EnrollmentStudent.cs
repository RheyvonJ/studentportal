using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StudentPortal.Models
{
    [BsonIgnoreExtraElements] // Ignore extra fields that might exist in the database
    public class EnrollmentStudent
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public bool FirstLogin { get; set; }

        [BsonElement("Email")]
        public string Email { get; set; } = string.Empty;

        // Name fields (present in SHSStudents)
        [BsonElement("FirstName")]
        public string? FirstName { get; set; }
        [BsonElement("MiddleName")]
        public string? MiddleName { get; set; }
        [BsonElement("LastName")]
        public string? LastName { get; set; }
        [BsonElement("Suffix")]
        public string? Suffix { get; set; }

        public string Type { get; set; } = string.Empty;

        public string ResetTokenHash { get; set; } = "";

        public DateTime? ResetTokenExpiryUtc { get; set; }

        // Optional fields that may or may not exist in the database
        public string? AccountStatus { get; set; }

        // Fields used by EnrollmentSystem/SHSStudents for activation/deactivation
        // Example keys observed: IsActive, enrollmentStatus, deactivatedAt, deactivationReason, SchoolYear
        [BsonElement("IsActive")]
        public bool? IsActive { get; set; }

        // Some deployments may store the same flag in camelCase.
        [BsonElement("isActive")]
        public bool? IsActiveCamel { get; set; }

        [BsonElement("enrollmentStatus")]
        public string? EnrollmentStatus { get; set; }

        [BsonElement("deactivatedAt")]
        public DateTime? DeactivatedAt { get; set; }

        [BsonElement("deactivationReason")]
        public string? DeactivationReason { get; set; }

        [BsonElement("SchoolYear")]
        public string? SchoolYear { get; set; }

        [BsonElement("schoolYear")]
        public string? SchoolYearCamel { get; set; }

        public string? LastEnrolledProgram { get; set; }
        public string? LastEnrolledYearLevel { get; set; }
        public string? LastEnrolledSemester { get; set; }
        public string? LastEnrolledAcademicYear { get; set; }
        public DateTime? LastEnrolledAt { get; set; }
        public DateTime? InactivatedAt { get; set; }
        public string? InactiveReason { get; set; }
        public string? InactiveTriggerTerm { get; set; }
    }
}

