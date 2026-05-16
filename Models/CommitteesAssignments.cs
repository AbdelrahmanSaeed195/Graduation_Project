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

        [Display(Name = "الصالة")]
        public int? HallId { get; set; } 

        [Display(Name = "البلوك")]
        public int? BlockId { get; set; }

        [Display(Name = "اللجنة")]
        public int? CommitteeId { get; set; }

        [Required(ErrorMessage = "يجب تحديد الدور")]
        [Display(Name = "الدور الوظيفي")]
        public int RoleId { get; set; }

        [Required(ErrorMessage = "يجب اختيار الجلسة")]
        [Display(Name = "جلسة الامتحان")]
        public int ExamScheduleId { get; set; }

        [Display(Name = "نوع التوزيع")]
        public string? AssignmentType { get; set; }

        [Display(Name = "مسمى الدور")]
        public string? RoleType { get; set; }

        // --- العلاقات (Navigation Properties) ---

        [ValidateNever]
        [ForeignKey("PersonId")]
        public virtual Person Person { get; set; }

        [ValidateNever]
        [ForeignKey("HallId")]
        public virtual Hall? Hall { get; set; }

        [ValidateNever]
        [ForeignKey("BlockId")]
        public virtual Block? Block { get; set; }

        [ValidateNever]
        [ForeignKey("CommitteeId")]
        public virtual Committee? Committee { get; set; }

        [ValidateNever]
        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; }

        [ValidateNever]
        [ForeignKey("ExamScheduleId")]
        public virtual ExamSchedule ExamSchedule { get; set; }
    }
}