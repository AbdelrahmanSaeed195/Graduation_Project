namespace projectweb.Models.ViewModels
{
    public class ExamControlSheetViewModel
    {
      
        public string AcademicYear { get; set; } = "2025 / 2026";
        public string Semester { get; set; } = "الفصل الدراسي الثاني";
        public string SubjectName { get; set; }
        public string TargetYear { get; set; }
        public string ExamTime { get; set; }
        public string ExamDate { get; set; }
        public string ExamDay { get; set; }

        // رئيس اللجنة والمراقب الرئيسي ورئيس اللجنة الاحتياطي والمراقب الاحتياطي
        public string MainHead { get; set; }      
        public string MainObserver { get; set; } 
        public string ReserveHead { get; set; }     
        public string ReserveObserver { get; set; } 

         
        public List<string> ReserveObserversList { get; set; } = new List<string>();

        // أسماء الأطباء والممرضين
        public string DoctorName { get; set; } = "................";
        public string NurseName { get; set; } = "................";


        // قائمة ملاحظين في اللجنة
        public List<ObserverRowItem> ObserverRows { get; set; } = new List<ObserverRowItem>();
    }

    public class ObserverRowItem
    {
        public string ObserverName { get; set; }
        public string CommitteeNumber { get; set; }
    }
}