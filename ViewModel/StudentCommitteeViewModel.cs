using System.ComponentModel.DataAnnotations;
using projectweb.Models; 

namespace projectweb.ViewModel
{
    public class StudentCommitteeViewModel
    {
        public int StudentId { get; set; }

        [Display(Name = "اسم الطالب")]
        public string FullName { get; set; }

        [Display(Name = "السنة الدراسية")]
        public AcademicLevel AcademicYear { get; set; }

        [Display(Name = "رقم الجلوس")]
        public int SeatNumber { get; set; } 

        [Display(Name = "كود اللجنة")]
        public int CommitteeId { get; set; }

        [Display(Name = "رقم اللجنة")]
        public int CommitteeNumber { get; set; }

        [Display(Name = "عدد الطلاب في اللجنة")]
        public int NumberOfStudent { get; set; }
    }
}