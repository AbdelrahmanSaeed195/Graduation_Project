namespace projectweb.ViewModel
{
    public class CommitteeStatisticsViewModel
    {
        public string AcademicYear { get; set; }
        public List<PersonCommitteeStatRow> Rows { get; set; }
    }

    public class PersonCommitteeStatRow
    {
        public string FullName { get; set; }
        public string JobRole { get; set; }
        public string RoleType { get; set; }
        public int TotalCount { get; set; }
        public int Year1Count { get; set; }
        public int Year2Count { get; set; }
        public int Year3Count { get; set; }
        public int Year4Count { get; set; }
    }
}
