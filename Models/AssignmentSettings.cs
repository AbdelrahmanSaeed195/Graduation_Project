using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace projectweb.Models
{
    [Index(nameof(AcademicYearCode), nameof(JobRole), IsUnique = true)]
    public class AssignmentSettings
    {
        [Key]
        [Display(Name = "كود الإعداد")]
        public int SettingId { get; set; }

        [Required(ErrorMessage = "يجب تحديد السنة الدراسية/الجامعية")]
        [StringLength(20)]
        [Display(Name = "السنة الجامعية")]
        public string AcademicYearCode { get; set; }

        [Required(ErrorMessage = "يجب اختيار الوظيفة المستهدفة")]
        [Display(Name = "الوظيفة المستهدفة")]
        public JobTitle JobRole { get; set; }

        [Required(ErrorMessage = "يجب تحديد الحد الأقصى للتكليفات")]
        [Range(1, 50, ErrorMessage = "الحد الأقصى يجب أن يكون بين 1 و 50 تكليف")]
        [Display(Name = "الحد الأقصى للتكليفات")]
        public int MaxAssignmentsLimit { get; set; }
    }
}