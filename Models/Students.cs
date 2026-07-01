using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projectweb.Models
{
    [Index(nameof(NationalId), IsUnique = true)]
    public class Student
    {
        [Key]
        [Display(Name = "كود الطالب")]
        public int StudentId { get; set; }

        [Required(ErrorMessage = "اسم الطالب بالكامل مطلوب")]
        [StringLength(200)]
        [Display(Name = "اسم الطالب")]
        public string FullName { get; set; }

        
        [StringLength(14, MinimumLength = 14, ErrorMessage = "الرقم القومي يجب أن يكون 14 رقم")]
        [Display(Name = "الرقم القومي")]
        [RegularExpression(@"^\d{14}$", ErrorMessage = "الرقم القومي يجب أن يتكون من 14 رقم فقط")]
        public string NationalId { get; set; }

        [Required(ErrorMessage = "السنة الدراسية مطلوبة")]
        [Display(Name = "السنة الدراسية")]
        public AcademicLevel AcademicYear { get; set; }

       

       

        // ========================================================
        // navigation properties
        // ========================================================

        [Display(Name = "الموقع الامتحاني المعين (اللجنة)")]
        public int? LocationId { get; set; }

        [ValidateNever]
        [ForeignKey("LocationId")]
        [Display(Name = "بيانات موقع الامتحان")]
        public virtual ExamLocation ExamLocation { get; set; }

        [Display(Name = "الجلسة الامتحانية الحالية")]
        public int? ExamScheduleId { get; set; }

        [ValidateNever]
        [ForeignKey("ExamScheduleId")]
        public virtual ExamSchedule ExamSchedule { get; set; }

        [ValidateNever]
        [Display(Name = "صلات القرابة")]
        public virtual ICollection<Relative> Relatives { get; set; }
    }
}