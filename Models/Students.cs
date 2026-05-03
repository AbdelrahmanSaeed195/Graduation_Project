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

        [Required(ErrorMessage = "الرقم القومي مطلوب")]
        [StringLength(14, MinimumLength = 14, ErrorMessage = "الرقم القومي يجب أن يكون 14 رقم")]
        [Display(Name = "الرقم القومي")]
        [RegularExpression(@"^\d{14}$", ErrorMessage = "الرقم القومي يجب أن يتكون من 14 رقم فقط")]
        public string NationalId { get; set; }

        [Required(ErrorMessage = "السنة الدراسية مطلوبة")]
        [Display(Name = "السنة الدراسية")]
        public int AcademicYear { get; set; }

        [Display(Name = "رقم الجلوس")]
        public int SeatNumber { get; set; } = 0;

        [Display(Name = "الجلسة الامتحانية")]
        public int? ExamScheduleId { get; set; }

        [ValidateNever]
        [ForeignKey("ExamScheduleId")]
        public virtual ExamSchedule ExamSchedule { get; set; }

        [NotMapped]
        public Committee Committee => ExamSchedule?.Committee;

        [ValidateNever]
        [Display(Name = "صلات القرابة")]
        public virtual ICollection<Relative> Relatives { get; set; }
    }
}