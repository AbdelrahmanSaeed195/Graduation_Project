using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace projectweb.Models
{
    public class Subject
    {
        [Key]
        [Display(Name = "كود المادة")]
        public int SubjectId { get; set; }

        [Required(ErrorMessage = "كود المادة مطلوب")]
        [Display(Name = "كود المادة")]
        public string SubjectCode { get; set; }

        [Required(ErrorMessage = "اسم المادة الدراسية مطلوب")]
        [StringLength(200)]
        [Display(Name = "اسم المادة")]
        public string SubjectName { get; set; }

        [Required(ErrorMessage = "السنة الدراسية مطلوبة")]
        [Display(Name = "السنة الدراسية")]
        public AcademicLevel AcademicYear { get; set; }

        [Required(ErrorMessage = "يجب تحديد التخصص")]
        [Display(Name = "التخصص")]
        public StudentSpecialization Specialization { get; set; } = StudentSpecialization.General;

        // ========================================================
        // Navigation properties
        // ========================================================

        [ValidateNever]
        [Display(Name = "الامتحانات المرتبطة")]
        public virtual ICollection<Exam> Exams { get; set; }
    }
}