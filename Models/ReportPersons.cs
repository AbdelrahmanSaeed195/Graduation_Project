using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projectweb.Models
{
    public class ReportPerson
    {
        [Key, Column(Order = 0)]
        [Display(Name = "كود المحضر")]
        public int ReportId { get; set; }

        [Display(Name = "التوقيع الإلكتروني")]
        public string Signature { get; set; }

        [Key, Column(Order = 1)]
        [Display(Name = "كود الشخص")]
        public int PersonId { get; set; }

        [ValidateNever]
        [ForeignKey("PersonId")]
        [Display(Name = "بيانات الموقع")]
        public virtual Person Person { get; set; }

        [Required]
        [Display(Name = "وقت التوقيع")]
        public DateTime SignedAt { get; set; } = DateTime.Now;

        [ValidateNever]
        [ForeignKey("ReportId")]
        [Display(Name = "بيانات المحضر")]
        public virtual Report Report { get; set; }

        [Display(Name = "الطالب المعني")]
        public int? StudentId { get; set; }

        [ForeignKey("StudentId")]
        [Display(Name = "بيانات الطالب")]
        public virtual Student Student { get; set; }

        [Required(ErrorMessage = "يجب تحديد صفة الموقع")]
        [Display(Name = "الصفة في المحضر")]
        public int RoleId { get; set; }

        [ValidateNever]
        [ForeignKey("RoleId")]
        [Display(Name = "بيانات الصفة")]
        public virtual Role Role { get; set; }

       
    }
}