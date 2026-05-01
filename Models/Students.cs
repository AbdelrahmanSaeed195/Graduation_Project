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
        [StringLength(20, MinimumLength = 14, ErrorMessage = "الرقم القومي يجب أن يكون 14 رقماً")]
        [Display(Name = "الرقم القومي")]
        [RegularExpression(@"^[0-9]{14}$", ErrorMessage = "الرقم القومي يجب أن يتكون من 14 رقماً فقط")]
        public string NationalId { get; set; }

        [Required(ErrorMessage = "السنة الدراسية مطلوبة")]
        [Display(Name = "السنة الدراسية")]
        public int AcademicYear { get; set; }

        [Required(ErrorMessage = "رقم الجلوس مطلوب")]
        [Display(Name = "رقم الجلوس")]
        public int SeatNumber { get; set; }

        [Display(Name = "اللجنة التابع لها")]
        public int? CommitteeId { get; set; }
        [ValidateNever]
        [ForeignKey("CommitteeId")]
        [Display(Name = "بيانات اللجنة")]
        public virtual Committee Committee { get; set; }
        [ValidateNever]
        [Display(Name = "صلات القرابة")]
        public virtual ICollection<Relative> Relatives { get; set; }

       

      
    }
}