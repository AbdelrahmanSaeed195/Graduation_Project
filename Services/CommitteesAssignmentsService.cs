using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace projectweb.Services
{
    public class _CommitteesAssignmentsService : ICommitteesAssignmentsService
    {
        private readonly ApplicationDbContext _context;

        public _CommitteesAssignmentsService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // 1. فحص تداخل الوقت (منع التوزيع إذا كانت الصالة مشغولة بمادة أخرى)
        // ============================================================
        public async Task<string> CheckTimeConflictAsync(int examScheduleId)
        {
            var current = await _context.ExamSchedules
                .Include(s => s.Exam).ThenInclude(e => e.Subject)
                .Include(s => s.Committee).ThenInclude(c => c.Block)
                .FirstOrDefaultAsync(s => s.ExamScheduleId == examScheduleId);

            if (current == null || current.Exam == null) return "الجلسة المطلوبة غير موجودة.";

            var hallId = current.Committee.Block.HallId;
            var examDate = current.Exam.ExamDate.Date;
            var startTime = current.Exam.StartTime;
            var endTime = current.Exam.EndTime;

            // البحث عن أي مادة مختلفة في نفس القاعة ونفس الوقت
            var conflict = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam).ThenInclude(e => e.Subject)
                .Where(a => a.ExamSchedule.ExamId != current.ExamId &&
                            ((a.HallId == hallId) || (a.Block.HallId == hallId) || (a.Committee.Block.HallId == hallId)) &&
                            a.ExamSchedule.Exam.ExamDate.Date == examDate &&
                            ((startTime >= a.ExamSchedule.Exam.StartTime && startTime < a.ExamSchedule.Exam.EndTime) ||
                             (endTime > a.ExamSchedule.Exam.StartTime && endTime <= a.ExamSchedule.Exam.EndTime)))
                .Select(a => a.ExamSchedule.Exam.Subject.SubjectName)
                .Distinct()
                .FirstOrDefaultAsync();

            if (conflict != null)
            {
                return $"عفواً، لا يمكن إجراء التوزيع. هذه الصالة مشغولة حالياً بامتحان مادة ({conflict}) في نفس الفترة الزمنية.";
            }

            return null;
        }

        // ============================================================
        // 2. تشغيل التوزيع التلقائي (مع تنظيف ذكي لنفس المادة فقط)
        // ============================================================
        public async Task<bool> RunAssignmentAsync(int examScheduleId)
        {
            var currentSchedule = await _context.ExamSchedules
                .Include(s => s.Exam)
                .Include(s => s.Committee).ThenInclude(c => c.Block)
                .FirstOrDefaultAsync(s => s.ExamScheduleId == examScheduleId);

            if (currentSchedule == null || currentSchedule.Exam == null) return false;

            var hallId = currentSchedule.Committee.Block.HallId;
            var examId = currentSchedule.ExamId;
            var examDate = currentSchedule.Exam.ExamDate.Date;

            // 1. تنظيف التوزيعات التلقائية القديمة لنفس الجلسة والصالة
            var oldAutoAssignments = await _context.CommitteesAssignments
                .Where(a => a.ExamSchedule.ExamId == examId &&
                            ((a.HallId == hallId) || (a.Block.HallId == hallId) || (a.Committee.Block.HallId == hallId)) &&
                            a.AssignmentType == "Auto")
                .ToListAsync();

            if (oldAutoAssignments.Any())
            {
                _context.CommitteesAssignments.RemoveRange(oldAutoAssignments);
                await _context.SaveChangesAsync();
            }

            // 2. جلب اللجان المجدولة لهذه الصالة
            var scheduledCommittees = await _context.ExamSchedules
                .Include(es => es.Committee).ThenInclude(c => c.Block)
                .Where(es => es.ExamId == examId && es.Committee.Block.HallId == hallId)
                .ToListAsync();

            if (!scheduledCommittees.Any()) return false;

            var activeBlocks = scheduledCommittees
                .Select(es => es.Committee.Block)
                .GroupBy(b => b.BlockID)
                .Select(g => g.First())
                .ToList();

            // 3. تحديد الموظفين المشغولين في نفس اليوم
            var busyPersonIds = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .Where(a => a.ExamSchedule.Exam.ExamDate.Date == examDate)
                .Select(a => a.PersonID).ToListAsync();

            // 4. جلب الموظفين النشطين وغير المشغولين
            var allAvailableStaff = await _context.Persons
                .Include(p => p.Role)
                .Where(p => !busyPersonIds.Contains(p.PersonId) && p.IsActiveForAssignment)
                .ToListAsync();

            var finalAssignments = new List<CommitteesAssignment>();
            var random = new Random();

            // --- الجزء المضاف: تطبيق منطق الأولوية لرؤساء الصالات ---
            // الموظفون الذين لم يكونوا رؤساء صالة بالأمس لديهم أولوية (Prioritized)
            var priorityStaff = new List<Person>();
            var secondaryStaff = new List<Person>();

            foreach (var staff in allAvailableStaff)
            {
                if (await HadPriorityRoleYesterday(staff.PersonId, examDate))
                    secondaryStaff.Add(staff);
                else
                    priorityStaff.Add(staff);
            }
            // -------------------------------------------------------

            // --- أ- توزيع رؤساء الصالة (الأولوية لمن لم يكلف بالأمس) ---
            // نحاول السحب من قائمة الأولوية أولاً، فإذا لم تكفِ نسحب من الباقي
            var hallManagersPool = priorityStaff
                .Where(p => p.JobRole == JobTitle.ProfessorEmeritus || p.JobRole == JobTitle.AssistantProfessor)
                .OrderBy(p => random.Next())
                .ToList();

            // إذا كان العدد أقل من المطلوب، نكمل من القائمة الثانوية
            if (hallManagersPool.Count < 3)
            {
                var extraManagers = secondaryStaff
                    .Where(p => p.JobRole == JobTitle.ProfessorEmeritus || p.JobRole == JobTitle.AssistantProfessor)
                    .OrderBy(p => random.Next())
                    .Take(3 - hallManagersPool.Count);
                hallManagersPool.AddRange(extraManagers);
            }

            // نأخذ أول 3 بعد الدمج والترتيب
            var selectedManagers = hallManagersPool.Take(3).ToList();

            if (selectedManagers.Count >= 2)
            {
                int totalBlocks = activeBlocks.Count;
                int half = totalBlocks / 2;
                for (int i = 0; i < selectedManagers.Count; i++)
                {
                    var manager = selectedManagers[i];
                    string roleTitle = i == 0 ? $"رئيس صالة أساسي (أول {half} بلوك)" :
                                       i == 1 ? $"رئيس صالة أساسي ({totalBlocks - half} بلوك)" : "رئيس صالة احتياطي";

                    finalAssignments.Add(new CommitteesAssignment
                    {
                        PersonID = manager.PersonId,
                        ExamScheduleId = examScheduleId,
                        RoleID = manager.RoleID,
                        HallId = hallId,
                        AssignmentType = "Auto",
                        RoleType = roleTitle
                    });
                    allAvailableStaff.Remove(manager);
                }
            }

            // --- ب- توزيع الطاقم الطبي ---
            var doctor = allAvailableStaff.OrderBy(p => random.Next()).FirstOrDefault(p => p.JobRole == JobTitle.Doctor);
            if (doctor != null)
            {
                finalAssignments.Add(new CommitteesAssignment { PersonID = doctor.PersonId, ExamScheduleId = examScheduleId, HallId = hallId, AssignmentType = "Auto", RoleType = "دكتور الصالة", RoleID = doctor.RoleID });
                allAvailableStaff.Remove(doctor);
            }

            var nurse = allAvailableStaff.OrderBy(p => random.Next()).FirstOrDefault(p => p.JobRole == JobTitle.Nurse);
            if (nurse != null)
            {
                finalAssignments.Add(new CommitteesAssignment { PersonID = nurse.PersonId, ExamScheduleId = examScheduleId, HallId = hallId, AssignmentType = "Auto", RoleType = "ممرض الصالة", RoleID = nurse.RoleID });
                allAvailableStaff.Remove(nurse);
            }

            // --- ج- توزيع المراقبين والملاحظين ---
            foreach (var block in activeBlocks)
            {
                var blockLeader = allAvailableStaff.Where(p => p.JobRole == JobTitle.StaffObserver || p.JobRole == JobTitle.Assistant).OrderBy(p => random.Next()).FirstOrDefault();
                if (blockLeader != null)
                {
                    finalAssignments.Add(new CommitteesAssignment
                    {
                        PersonID = blockLeader.PersonId,
                        ExamScheduleId = examScheduleId,
                        BlockId = block.BlockID,
                        AssignmentType = "Auto",
                        RoleType = "مراقب",
                        RoleID = blockLeader.RoleID
                    });
                    allAvailableStaff.Remove(blockLeader);

                    var committeesInBlock = scheduledCommittees.Where(es => es.Committee.BlockID == block.BlockID).ToList();
                    foreach (var com in committeesInBlock)
                    {
                        var obs = allAvailableStaff.Where(p => p.JobRole == JobTitle.Employee).OrderBy(p => random.Next()).FirstOrDefault();
                        if (obs != null)
                        {
                            finalAssignments.Add(new CommitteesAssignment
                            {
                                PersonID = obs.PersonId,
                                ExamScheduleId = examScheduleId,
                                CommitteeID = com.CommitteeId,
                                AssignmentType = "Auto",
                                RoleType = "ملاحظ لجنة",
                                RoleID = obs.RoleID
                            });
                            allAvailableStaff.Remove(obs);
                        }
                    }
                }
            }

            if (finalAssignments.Any())
            {
                _context.CommitteesAssignments.AddRange(finalAssignments);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        // ميثود مساعدة لفحص تكليف الأمس
        private async Task<bool> HadPriorityRoleYesterday(int personId, DateTime currentExamDate)
        {
            var yesterday = currentExamDate.AddDays(-1).Date;
            return await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .AnyAsync(a => a.PersonID == personId &&
                               a.ExamSchedule.Exam.ExamDate.Date == yesterday &&
                               (a.RoleType.Contains("رئيس صالة") || a.RoleID == 1));
        }
    }
}