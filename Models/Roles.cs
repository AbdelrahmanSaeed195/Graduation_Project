using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace projectweb.Models
{
    public enum StaffPosition
    {
        [Display(Name = "رئيس صالة")]
        HallManager = 1,

        [Display(Name = "مراقب")]
        BlockGroupLeader = 2,

        [Display(Name = "ملاحظ")]
        CommitteeObserver = 3,

        [Display(Name = "طبيب")]
        Doctor = 4,

        [Display(Name = "ممرض")]
        Nurse = 5,

    }
    public class Role
    {
        [Key]
        [Display(Name = "كود الدور")]
        public int RoleID { get; set; }

        [Required(ErrorMessage = "اسم الدور الوظيفي مطلوب")]
        [StringLength(100)]
        [Display(Name = "مسمى الدور الوظيفي")]
        public StaffPosition RoleName { get; set; }

        [Display(Name = "وصف الدور / المهام")]
        [DataType(DataType.MultilineText)]
        public string RoleDescription { get; set; }


        [Display(Name = "الموظفين بهذا الدور")]
        public virtual ICollection<Person> Persons { get; set; }

        [Display(Name = "التكليفات المرتبطة")]
        public virtual ICollection<CommitteesAssignment> CommitteesAssignments { get; set; }

        [Display(Name = "المحاضر المرتبطة")]
        public virtual ICollection<ReportPerson> ReportPersons { get; set; }


    }
}