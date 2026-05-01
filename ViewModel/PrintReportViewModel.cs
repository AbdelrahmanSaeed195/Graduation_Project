using System;
using System.Collections.Generic;

namespace projectweb.Models.ViewModels
{
    public class PrintReportViewModel
    {
        public string CollegeName { get; set; } = "كلية علوم الرياضة";
        public string AcademicYear { get; set; } = "2024 / 2025";
        public string PersonRoleInReport { get; set; }
        public List<AssignmentRowGroup> Rows { get; set; }
        public Dictionary<string, string> YearTimes { get; set; } = new Dictionary<string, string>();
    }

    public class AssignmentRowGroup
    {
        public string Day { get; set; }
        public DateTime Date { get; set; }
        public List<AssignmentReportItem> DailyItems { get; set; }
    }

    public class AssignmentReportItem
    {
        public string SubjectName { get; set; }
        public string TargetYear { get; set; }
        public string ExamTimeRange { get; set; }
        public string PersonFullName { get; set; }
    }
}