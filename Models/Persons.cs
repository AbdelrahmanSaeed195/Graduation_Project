using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projectweb.Models
{
    public class Person
    {
        [Key]
        [Display(Name = "كود الموظف")]
        public int PersonId { get; set; }

        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [StringLength(200)]
        [Display(Name = "الاسم الكامل")]
        public string FullName { get; set; }

        [StringLength(20)]
        [Display(Name = "رقم الهاتف")]
        public string Phone { get; set; }

        [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; }

        [Required(ErrorMessage = "الرقم القومي مطلوب")]
        [StringLength(20, MinimumLength = 14, ErrorMessage = "الرقم القومي يجب أن يكون 14 رقماً")]
        [Display(Name = "الرقم القومي")]
        public string NationalId { get; set; }

        [Required(ErrorMessage = "يجب تحديد التخصص/الوظيفة")]
        [Display(Name = "الوظيفة")]
        public int RoleID { get; set; }

        [ForeignKey("RoleID")]
        [Display(Name = "بيانات الوظيفة")]
        public virtual Role Role { get; set; }

        [Display(Name = "حالة النشاط في التكليفات")]
        public bool IsActiveForAssignment { get; set; } = true;

        [Display(Name = "تكليفات المراقبة")]
        public virtual ICollection<CommitteesAssignment> CommitteesAssignments { get; set; }

        [Display(Name = "التوقيعات على المحاضر")]
        public virtual ICollection<ReportPerson> ReportPersons { get; set; }

        [Display(Name = "أقارب الدرجة الأولى")]
        public virtual ICollection<Relative> Relatives { get; set; }

      
    }
}