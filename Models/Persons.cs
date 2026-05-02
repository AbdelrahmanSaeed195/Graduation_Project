//using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
//using Microsoft.EntityFrameworkCore;
//using System.Collections.Generic;
//using System.ComponentModel.DataAnnotations;
//using System.ComponentModel.DataAnnotations.Schema;

//namespace projectweb.Models
//{
//    [Index(nameof(NationalId), IsUnique = true)]
//    public class Person
//    {
//        [Key]
//        [Display(Name = "كود الموظف")]
//        public int PersonId { get; set; }

//        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
//        [StringLength(200)]
//        [Display(Name = "الاسم الكامل")]
//        public string FullName { get; set; }

//        [StringLength(20)]
//        [Display(Name = "رقم الهاتف")]
//        public string Phone { get; set; }

//        [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
//        [Display(Name = "البريد الإلكتروني")]
//        public string Email { get; set; }

//        [Required(ErrorMessage = "الرقم القومي مطلوب")]
//        [StringLength(20, MinimumLength = 14, ErrorMessage = "الرقم القومي يجب أن يكون 14 رقماً")]
//        [Display(Name = "الرقم القومي")]
//        [RegularExpression(@"^[0-9]{14}$", ErrorMessage = "الرقم القومي يجب أن يتكون من 14 رقماً فقط")]
//        public string NationalId { get; set; }

//        [Required(ErrorMessage = "يجب تحديد التخصص/الوظيفة")]
//        [Display(Name = "الوظيفة")]
//        public int RoleID { get; set; }
//        [ValidateNever]
//        [ForeignKey("RoleID")]
//        [Display(Name = "بيانات الوظيفة")]
//        public virtual Role Role { get; set; }

//        [Display(Name = "حالة النشاط في التكليفات")]
//        public bool IsActiveForAssignment { get; set; } = true;
//        [ValidateNever]
//        [Display(Name = "تكليفات المراقبة")]
//        public virtual ICollection<CommitteesAssignment> CommitteesAssignments { get; set; }
//        [ValidateNever]
//        [Display(Name = "التوقيعات على المحاضر")]
//        public virtual ICollection<ReportPerson> ReportPersons { get; set; }
//        [ValidateNever]
//        [Display(Name = "أقارب الدرجة الأولى")]
//        public virtual ICollection<Relative> Relatives { get; set; }


//    }
//}
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projectweb.Models
{
    // تعريف الـ Enum بالوظائف المطلوبة
    public enum JobTitle
    {
        [Display(Name = "أستاذ متفرغ")]
        ProfessorEmeritus = 1,
        [Display(Name = "أستاذ مساعد")]
        AssistantProfessor = 2,
        [Display(Name = "أستاذ")]
        Professor = 3,
        [Display(Name = "عميد الكلية")]
        Dean = 4,
        [Display(Name = "موظف (ملاحظ)")]
        StaffObserver = 5
    }

    [Index(nameof(NationalId), IsUnique = true)]
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
        [RegularExpression(@"^[0-9]{14}$", ErrorMessage = "الرقم القومي يجب أن يتكون من 14 رقماً فقط")]
        public string NationalId { get; set; }

        [Required(ErrorMessage = "يجب تحديد التخصص/الوظيفة")]
        [Display(Name = "الوظيفة")]
        public JobTitle JobRole { get; set; } // التغيير هنا لنوع Enum

        [Required(ErrorMessage = "يجب تحديد التخصص/الوظيفة")]
        [Display(Name = "الوظيفة")]
        public int RoleID { get; set; } = 1;
        [ValidateNever]
        [ForeignKey("RoleID")]
        [Display(Name = "بيانات الوظيفة")]
        public virtual Role Role { get; set; }

        [Display(Name = "حالة النشاط في التكليفات")]
        public bool IsActiveForAssignment { get; set; } = true;

        [ValidateNever]
        [Display(Name = "تكليفات المراقبة")]
        public virtual ICollection<CommitteesAssignment> CommitteesAssignments { get; set; }

        [ValidateNever]
        [Display(Name = "التوقيعات على المحاضر")]
        public virtual ICollection<ReportPerson> ReportPersons { get; set; }

        [ValidateNever]
        [Display(Name = "أقارب الدرجة الأولى")]
        public virtual ICollection<Relative> Relatives { get; set; }
    }
}