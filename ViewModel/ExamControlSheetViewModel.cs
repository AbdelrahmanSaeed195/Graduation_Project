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

        public string MainHead { get; set; } // رئيس اللجنة
        public string MainObserver { get; set; } // مراقب اللجنة
        public string ReserveHead { get; set; } // احتياطي رئيس
        public string ReserveObserver { get; set; } // احتياطي مراقب
        public string DoctorName { get; set; } = "................";

        public List<ObserverRowItem> ObserverRows { get; set; } = new List<ObserverRowItem>();
    }

    public class ObserverRowItem
    {
        public string ObserverName { get; set; }
        public int? BookletsCount { get; set; }
        public string Notes { get; set; }
    }
}