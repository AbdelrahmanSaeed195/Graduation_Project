using System.ComponentModel.DataAnnotations;
using projectweb.Models;

namespace projectweb.ViewModel
{
    public class StudentCreateViewModel
    {
        [Required(ErrorMessage = "اسم الطالب بالكامل مطلوب")]
        [StringLength(200, ErrorMessage = "اسم الطالب لا يجب أن يتجاوز 200 حرف")]
        [Display(Name = "اسم الطالب بالكامل")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "الرقم القومي مطلوب")]
        [StringLength(14, MinimumLength = 14, ErrorMessage = "الرقم القومي يجب أن يكون 14 رقم بالتمام")]
        [RegularExpression(@"^\d{14}$", ErrorMessage = "الرقم القومي يجب أن يتكون من 14 رقم فقط")]
        [Display(Name = "الرقم القومي")]
        public string NationalId { get; set; }

        [Required(ErrorMessage = "السنة الدراسية مطلوبة")]
        [Display(Name = "السنة الدراسية")]
        public AcademicLevel AcademicYear { get; set; }

        [Required(ErrorMessage = "يجب تحديد التخصص")]
        [Display(Name = "التخصص")]
        public StudentSpecialization Specialization { get; set; } = StudentSpecialization.General;

        // ========================================================
        
       
        [Display(Name = "اللجنة الامتحانية التابع لها")]
        public int? LocationId { get; set; }
     
    }
}