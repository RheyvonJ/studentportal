using System;
using System.Collections.Generic;

namespace StudentPortal.Models.AdminMaterial
{
    public class AdminMaterialViewModel
    {
        public string MaterialId { get; set; } = "";
        public string SubjectName { get; set; } = "";
        public string SubjectCode { get; set; } = "";
        public string ClassCode { get; set; } = "";
        public string InstructorName { get; set; } = "";
        public string InstructorInitials { get; set; } = "";
        public string TeacherRole { get; set; } = "";
        public string TeacherDepartment { get; set; } = "";
        public string RoomName { get; set; } = "";
        public string FloorDisplay { get; set; } = "";
        public string MaterialName { get; set; } = "";
        public string MaterialDescription { get; set; } = "";
        public List<string> Attachments { get; set; } = new();
        public string PostedDate { get; set; } = "";
        public string EditedDate { get; set; } = "";
        public List<string> RecentMaterials { get; set; } = new();
    }
}
