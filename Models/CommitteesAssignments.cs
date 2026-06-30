using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace projectweb.Models
{
    public class CommitteesAssignment
    {
        [Key]
        [Display(Name = "كود التكليف")]
        public int AssignmentId { get; set; }

        [Required(ErrorMessage = "يجب اختيار الموظف")]
        [Display(Name = "الموظف")]
        public int PersonId { get; set; }

        [Display(Name = "نوع التوزيع")]
        public string? AssignmentType { get; set; }

        [Display(Name = "مسمى الدور")]
        public string? RoleType { get; set; }

        [Display(Name = "التوزيع الفرعي (القطاع/الدور)")]
        public string? SubRoleType { get; set; }

        // ========================================================

        [Display(Name = "مكان التكليف (جراش/صالة/لجنة)")]
        public int? LocationId { get; set; }

        [Required(ErrorMessage = "يجب تحديد الدور")]
        [Display(Name = "الدور الوظيفي")]
        public int RoleId { get; set; }

        [Required(ErrorMessage = "يجب اختيار الجلسة")]
        [Display(Name = "جلسة الامتحان")]
        public int ExamScheduleId { get; set; }

        [ValidateNever]
        [ForeignKey("PersonId")]
        public virtual Person Person { get; set; } 

        [ValidateNever]
        [ForeignKey("LocationId")]
        [Display(Name = "بيانات مكان التكليف")]
        public virtual ExamLocation? ExamLocation { get; set; }

        [ValidateNever]
        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; }

        [ValidateNever]
        [ForeignKey("ExamScheduleId")]
        public virtual ExamSchedule ExamSchedule { get; set; }
    }
}