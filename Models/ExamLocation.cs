using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projectweb.Models
{
    public class ExamLocation
    {
        [Key]
        [Display(Name = "كود المكان")]
        public int LocationId { get; set; }

        [Required(ErrorMessage = "اسم المكان أو رقمه مطلوب")]
        [Display(Name = "اسم / رقم المكان")]
        public string LocationName { get; set; }

        [Required]
        [Display(Name = "نوع المكان")]
        public LocationType Type { get; set; }

        [Display(Name = "السنة الدراسية المخصصة لهذا الجراش")]
        public AcademicLevel? AcademicYear { get; set; }


        [Display(Name = "رقم الدور")]
        public int? Floor { get; set; }

        [Display(Name = "الحد الأقصى للتفريعات")]
        public int? MaxSubLocations { get; set; }

        [Display(Name = "سعة الطلاب الاستيعابية")]
        public int? StudentCapacity { get; set; }

        [Display(Name = "الحالة الإدارية")]
        public string? Status { get; set; }

        // =====================================
        // Self-Referencing
        // =====================================
        [Display(Name = "المكان الرئيسي التابع له")]
        public int? ParentLocationId { get; set; }

        [ValidateNever]
        [ForeignKey("ParentLocationId")]
        public virtual ExamLocation ParentLocation { get; set; }

        [ValidateNever]
        public virtual ICollection<ExamLocation> SubLocations { get; set; } = new List<ExamLocation>();

        // =====================================
        // Navigation Properties 
        // =====================================

        [ValidateNever]
        public virtual ICollection<Student> Students { get; set; } = new List<Student>();

        [ValidateNever]
        public virtual ICollection<CommitteesAssignment> CommitteesAssignments { get; set; } = new List<CommitteesAssignment>();

        [ValidateNever]
        public virtual ICollection<ExamSchedule> ExamSchedules { get; set; } = new List<ExamSchedule>();
    }
}