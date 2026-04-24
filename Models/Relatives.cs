using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projectweb.Models
{
    public class Relative
    {
        [Key]
        [Display(Name = "كود القرابة")]
        public int RelativeId { get; set; }

        [Required(ErrorMessage = "يجب تحديد الطالب")]
        [Display(Name = "الطالب")]
        public int StudentId { get; set; }
        [ValidateNever]
        [ForeignKey("StudentId")]
        [Display(Name = "بيانات الطالب")]
        public virtual Student Student { get; set; }

        [Required(ErrorMessage = "يجب تحديد الموظف")]
        [Display(Name = "الموظف")]
        public int PersonId { get; set; }
        [ValidateNever]
        [ForeignKey("PersonId")]
        [Display(Name = "بيانات الموظف")]
        public virtual Person Person { get; set; }

        [Required(ErrorMessage = "يجب تحديد درجة القرابة")]
        [StringLength(50)]
        [Display(Name = "درجة القرابة")]
        public string RelationType { get; set; }

        [Required(ErrorMessage = "السنة الدراسية مطلوبة")]
        [Display(Name = "السنة الأكاديمية")]
        public int AcademicYear { get; set; }
    }
}