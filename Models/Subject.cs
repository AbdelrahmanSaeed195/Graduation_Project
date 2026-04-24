using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace projectweb.Models
{
    public class Subject
    {
        [Key]
        [Display(Name = "كود المادة")]
        public int SubjectId { get; set; }

        [Required(ErrorMessage = "اسم المادة الدراسية مطلوب")]
        [StringLength(200)]
        [Display(Name = "اسم المادة")]
        public string SubjectName { get; set; }

        [Display(Name = "الامتحانات المرتبطة")]
        public virtual ICollection<Exam>? Exams { get; set; }

      
    }
}