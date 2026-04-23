using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projectweb.Models
{
    public class ReportPerson
    {
        [Key, Column(Order = 0)]
        [Display(Name = "كود المحضر")]
        public int ReportID { get; set; }

        [ForeignKey("ReportID")]
        [Display(Name = "بيانات المحضر")]
        public virtual Report Report { get; set; }

        [Key, Column(Order = 1)]
        [Display(Name = "كود الشخص")]
        public int PersonID { get; set; }

        [ForeignKey("PersonID")]
        [Display(Name = "بيانات الموقع")]
        public virtual Person Person { get; set; }

        [Required(ErrorMessage = "يجب تحديد صفة الموقع")]
        [Display(Name = "الصفة في المحضر")]
        public int RoleID { get; set; }

        [ForeignKey("RoleID")]
        [Display(Name = "بيانات الصفة")]
        public virtual Role Role { get; set; }

        [Display(Name = "التوقيع الإلكتروني")]
        public string Signature { get; set; }

        [Required]
        [Display(Name = "وقت التوقيع")]
        public DateTime SignedAt { get; set; } = DateTime.Now;
    }
}