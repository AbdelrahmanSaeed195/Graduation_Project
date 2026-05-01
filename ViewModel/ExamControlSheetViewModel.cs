namespace projectweb.Models.ViewModels
{
    public class ExamControlSheetViewModel
    {
        // بيانات الترويسة
        public string AcademicYear { get; set; } = "2024 / 2025";
        public string Semester { get; set; } = "الفصل الدراسي الثاني";
        public string SubjectName { get; set; }
        public string TargetYear { get; set; } 
        public string ExamTime { get; set; }
        public string MainHead { get; set; } 
        public string MainObserver { get; set; } 

        // قائمة الملاحظين داخل الجدول
        public List<ObserverRowItem> ObserverRows { get; set; }

        // بيانات الطاقم الاحتياطي والطبي
        public string ReserveHead { get; set; }
        public string ReserveObserver { get; set; }
        public string DoctorName { get; set; }
    }

    public class ObserverRowItem
    {
        public int Id { get; set; } // "م"
        public string ObserverName { get; set; }
        public int? BookletsCount { get; set; }
        public string Notes { get; set; }
    }
}