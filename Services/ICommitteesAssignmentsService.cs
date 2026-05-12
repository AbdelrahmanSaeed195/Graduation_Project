namespace projectweb.Services
{
    public interface ICommitteesAssignmentsService
    {
        
        Task<bool> RunAssignmentAsync(int examScheduleId);

        Task<string> CheckTimeConflictAsync(int examScheduleId);
    }
}
