namespace projectweb.ViewModel
{
    public class StudentCommitteeViewModel
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public int AcademicYear { get; set; }
        public int? SeatNumber { get; set; }

        public int CommitteeId { get; set; }
        public int CommitteeNumber { get; set; }
        public int NumberOfStudent { get; set; }
    }
}
