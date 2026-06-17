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

        public async Task<string> CheckTimeConflictAsync(int examScheduleId)
        {
            var current = await _context.ExamSchedules
                .Include(s => s.Exam).ThenInclude(e => e.Subject)
                .Include(s => s.Block)
                .FirstOrDefaultAsync(s => s.ExamScheduleId == examScheduleId);

            if (current == null || current.Exam == null) return "الجلسة المطلوبة غير موجودة.";

            var hallId = current.Block.HallId;
            var examDate = current.Exam.ExamDate.Date;
            var startTime = current.Exam.StartTime;
            var endTime = current.Exam.EndTime;

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

        public async Task<bool> RunAssignmentAsync(int examScheduleId)
        {
            var currentSchedule = await _context.ExamSchedules
                .Include(s => s.Exam)
                .Include(s => s.Block)
                .FirstOrDefaultAsync(s => s.ExamScheduleId == examScheduleId);

            if (currentSchedule == null || currentSchedule.Exam == null) return false;

            var hallId = currentSchedule.Block.HallId;
            var examId = currentSchedule.ExamId;
            var examDate = currentSchedule.Exam.ExamDate.Date;

            // 1. تنظيف التوزيع التلقائي القديم لهذه المادة في هذه الصالة اليوم
            var oldAutoAssignments = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule)
                .Where(a => a.ExamSchedule.ExamId == examId &&
                            ((a.HallId == hallId) || (a.Block.HallId == hallId) || (a.Committee.Block.HallId == hallId)) &&
                            a.AssignmentType == "Auto")
                .ToListAsync();

            if (oldAutoAssignments.Any())
            {
                _context.CommitteesAssignments.RemoveRange(oldAutoAssignments);
                await _context.SaveChangesAsync();
            }

            var scheduledSchedules = await _context.ExamSchedules
                .Include(es => es.Block)
                .Where(es => es.ExamId == examId && es.Block.HallId == hallId)
                .ToListAsync();

            if (!scheduledSchedules.Any()) return false;

            var activeBlocks = scheduledSchedules
                .Select(es => es.Block)
                .GroupBy(b => b.BlockId)
                .Select(g => g.First())
                .OrderBy(b => b.BlockName)
                .ToList();

            var busyPersonIds = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .Where(a => a.ExamSchedule.ExamId == examId)
                .Select(a => a.PersonId).ToListAsync();

            var allAvailableStaff = await _context.Persons
                .Include(p => p.Role)
                .Where(p => !busyPersonIds.Contains(p.PersonId) && p.IsActiveForAssignment)
                .ToListAsync();

            var staffWhoWorkedInThisHallToday = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .Where(a => a.ExamSchedule.Exam.ExamDate.Date == examDate &&
                            ((a.HallId == hallId) || (a.Block.HallId == hallId) || (a.Committee.Block.HallId == hallId)))
                .Select(a => a.PersonId)
                .Distinct()
                .ToListAsync();

            var yesterday = examDate.AddDays(-1).Date;
            var yesterdayAssignments = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .Where(a => a.ExamSchedule.Exam.ExamDate.Date == yesterday && a.CommitteeId != null)
                .Select(a => new { a.PersonId, a.CommitteeId })
                .ToListAsync();

            var finalAssignments = new List<CommitteesAssignment>();
            var random = new Random();

            var currentDayHallPriorityStaff = new List<Person>();
            var historyPriorityStaff = new List<Person>();
            var normalStaff = new List<Person>();
            var lowPriorityStaff = new List<Person>();

            foreach (var staff in allAvailableStaff)
            {
                if (staffWhoWorkedInThisHallToday.Contains(staff.PersonId))
                {
                    currentDayHallPriorityStaff.Add(staff);
                }
                else
                {
                    int weight = await GetStaffPriorityWeightAsync(staff.PersonId, examDate);
                    if (weight == 2) historyPriorityStaff.Add(staff);
                    else if (weight == 1) normalStaff.Add(staff);
                    else lowPriorityStaff.Add(staff);
                }
            }

            // تجميع الخزانات بناءً على الهيكل الجديد
            var poolManagers = new List<Person>();
            poolManagers.AddRange(currentDayHallPriorityStaff.Where(p => p.JobRole == JobTitle.Professor || p.JobRole == JobTitle.AssistantProfessor || p.JobRole == JobTitle.ProfessorEmeritus).OrderBy(p => random.Next()));
            poolManagers.AddRange(historyPriorityStaff.Where(p => p.JobRole == JobTitle.Professor || p.JobRole == JobTitle.AssistantProfessor || p.JobRole == JobTitle.ProfessorEmeritus).OrderBy(p => random.Next()));
            poolManagers.AddRange(normalStaff.Where(p => p.JobRole == JobTitle.Professor || p.JobRole == JobTitle.AssistantProfessor || p.JobRole == JobTitle.ProfessorEmeritus).OrderBy(p => random.Next()));
            poolManagers.AddRange(lowPriorityStaff.Where(p => p.JobRole == JobTitle.Professor || p.JobRole == JobTitle.AssistantProfessor || p.JobRole == JobTitle.ProfessorEmeritus).OrderBy(p => random.Next()));

            var poolLeaders = new List<Person>();
            poolLeaders.AddRange(currentDayHallPriorityStaff.Where(p => p.JobRole == JobTitle.StaffObserver).OrderBy(p => random.Next()));
            poolLeaders.AddRange(historyPriorityStaff.Where(p => p.JobRole == JobTitle.StaffObserver).OrderBy(p => random.Next()));
            poolLeaders.AddRange(normalStaff.Where(p => p.JobRole == JobTitle.StaffObserver).OrderBy(p => random.Next()));
            poolLeaders.AddRange(lowPriorityStaff.Where(p => p.JobRole == JobTitle.StaffObserver).OrderBy(p => random.Next()));

            var poolObservers = new List<Person>();
            poolObservers.AddRange(currentDayHallPriorityStaff.Where(p => p.JobRole == JobTitle.Assistant || p.JobRole == JobTitle.AssistantStaff || p.JobRole == JobTitle.Employee).OrderBy(p => random.Next()));
            poolObservers.AddRange(historyPriorityStaff.Where(p => p.JobRole == JobTitle.Assistant || p.JobRole == JobTitle.AssistantStaff || p.JobRole == JobTitle.Employee).OrderBy(p => random.Next()));
            poolObservers.AddRange(normalStaff.Where(p => p.JobRole == JobTitle.Assistant || p.JobRole == JobTitle.AssistantStaff || p.JobRole == JobTitle.Employee).OrderBy(p => random.Next()));
            poolObservers.AddRange(lowPriorityStaff.Where(p => p.JobRole == JobTitle.Assistant || p.JobRole == JobTitle.AssistantStaff || p.JobRole == JobTitle.Employee).OrderBy(p => random.Next()));

            var poolDoctors = new List<Person>();
            poolDoctors.AddRange(allAvailableStaff.Where(p => p.JobRole == JobTitle.Doctor).OrderBy(p => random.Next()));

            var poolNurses = new List<Person>();
            poolNurses.AddRange(allAvailableStaff.Where(p => p.JobRole == JobTitle.Nurse).OrderBy(p => random.Next()));

            int totalActiveBlocks = activeBlocks.Count;
            int half = (int)Math.Ceiling((double)totalActiveBlocks / 2);

            var firstSectionBlocks = activeBlocks.Take(half).ToList();
            var secondSectionBlocks = activeBlocks.Skip(half).ToList();

            // 2. توزيع رؤساء الصالات (أساتذة)
            var selectedManagers = poolManagers.Take(3).ToList();
            if (selectedManagers.Count < 2) return false;

            for (int i = 0; i < selectedManagers.Count; i++)
            {
                var manager = selectedManagers[i];

                if (i == 0)
                {
                    var firstBlock = firstSectionBlocks.FirstOrDefault();
                    finalAssignments.Add(new CommitteesAssignment
                    {
                        PersonId = manager.PersonId,
                        ExamScheduleId = examScheduleId,
                        RoleId = manager.RoleId,
                        HallId = hallId,
                        BlockId = firstBlock?.BlockId,
                        AssignmentType = "Auto",
                        RoleType = "رئيس صالة أساسي (القطاع الأول)"
                    });
                }
                else if (i == 1)
                {
                    var firstBlockOfSecondSection = secondSectionBlocks.FirstOrDefault();
                    finalAssignments.Add(new CommitteesAssignment
                    {
                        PersonId = manager.PersonId,
                        ExamScheduleId = examScheduleId,
                        RoleId = manager.RoleId,
                        HallId = hallId,
                        BlockId = firstBlockOfSecondSection?.BlockId,
                        AssignmentType = "Auto",
                        RoleType = "رئيس صالة أساسي (القطاع الثاني)"
                    });
                }
                else if (i == 2)
                {
                    finalAssignments.Add(new CommitteesAssignment
                    {
                        PersonId = manager.PersonId,
                        ExamScheduleId = examScheduleId,
                        RoleId = manager.RoleId,
                        HallId = hallId,
                        BlockId = null,
                        CommitteeId = null,
                        AssignmentType = "Auto",
                        RoleType = "رئيس صالة احتياطي"
                    });
                }
            }

            // 3. توزيع مراقب احتياطي واحد فقط للصالة (يكون من فئة "مدرس" ومسؤول عنه رئيس الصالة)
            var reserveLeader = poolLeaders.FirstOrDefault();
            if (reserveLeader != null)
            {
                finalAssignments.Add(new CommitteesAssignment
                {
                    PersonId = reserveLeader.PersonId,
                    ExamScheduleId = examScheduleId,
                    RoleId = reserveLeader.RoleId,
                    HallId = hallId,
                    BlockId = null, 
                    AssignmentType = "Auto",
                    RoleType = "مراقب احتياطي للصالة (تحت إدارة رئيس الصالة)"
                });
                poolLeaders.Remove(reserveLeader); 
            }

            // حساب الـ 5% ملاحظين احتياطيين
            var totalCommitteesCount = await _context.ExamSchedules
                .Include(es => es.Block)
                .Where(es => es.ExamId == examId && es.Block.HallId == hallId)
                .CountAsync();

            int requiredReserveObserversCount = (int)Math.Ceiling(totalCommitteesCount * 0.05);
            if (requiredReserveObserversCount < 1) requiredReserveObserversCount = 1;

            // تأمين الدكاترة والممرضين
            var doctor = poolDoctors.FirstOrDefault();
            if (doctor != null)
            {
                finalAssignments.Add(new CommitteesAssignment { PersonId = doctor.PersonId, ExamScheduleId = examScheduleId, HallId = hallId, BlockId = null, CommitteeId = null, AssignmentType = "Auto", RoleType = "دكتور الصالة", RoleId = doctor.RoleId });
            }

            var nurse = poolNurses.FirstOrDefault();
            if (nurse != null)
            {
                finalAssignments.Add(new CommitteesAssignment { PersonId = nurse.PersonId, ExamScheduleId = examScheduleId, HallId = hallId, BlockId = null, CommitteeId = null, AssignmentType = "Auto", RoleType = "ممرض الصالة", RoleId = nurse.RoleId });
            }

            // 4. توزيع المراقبين الأساسيين والملاحظين على البلوكات واللجان
            foreach (var block in activeBlocks)
            {
                var blockLeader = poolLeaders.FirstOrDefault();
                if (blockLeader != null)
                {
                    finalAssignments.Add(new CommitteesAssignment
                    {
                        PersonId = blockLeader.PersonId,
                        ExamScheduleId = examScheduleId,
                        BlockId = block.BlockId,
                        HallId = hallId,
                        AssignmentType = "Auto",
                        RoleType = "مراقب",
                        RoleId = blockLeader.RoleId
                    });
                    poolLeaders.Remove(blockLeader);
                }

                var activeCommitteesInBlock = await _context.Committees
                    .Where(c => c.BlockId == block.BlockId)
                    .ToListAsync();

                foreach (var com in activeCommitteesInBlock)
                {
                    var obs = poolObservers
                        .FirstOrDefault(p => !yesterdayAssignments.Any(y => y.PersonId == p.PersonId && y.CommitteeId == com.CommitteeId));

                    if (obs == null)
                    {
                        obs = poolObservers.FirstOrDefault();
                    }

                    if (obs != null)
                    {
                        finalAssignments.Add(new CommitteesAssignment
                        {
                            PersonId = obs.PersonId,
                            ExamScheduleId = examScheduleId,
                            CommitteeId = com.CommitteeId,
                            BlockId = block.BlockId,
                            HallId = hallId,
                            AssignmentType = "Auto",
                            RoleType = "ملاحظ لجنة",
                            RoleId = obs.RoleId
                        });
                        poolObservers.Remove(obs);
                    }
                }
            }

            // 5. توزيع الملاحظين الاحتياطيين بنسبة 5% على البلوكات
            int distributedReserves = 0;
            while (distributedReserves < requiredReserveObserversCount && poolObservers.Any())
            {
                foreach (var block in activeBlocks)
                {
                    if (distributedReserves >= requiredReserveObserversCount || !poolObservers.Any())
                        break;

                    var reserveNote = poolObservers.FirstOrDefault();
                    if (reserveNote != null)
                    {
                        finalAssignments.Add(new CommitteesAssignment
                        {
                            PersonId = reserveNote.PersonId,
                            ExamScheduleId = examScheduleId,
                            RoleId = reserveNote.RoleId,
                            HallId = hallId,
                            BlockId = block.BlockId, 
                            AssignmentType = "Auto",
                            RoleType = "ملاحظ احتياطي للكلية (تحت إدارة المراقب)"
                        });
                        poolObservers.Remove(reserveNote);
                        distributedReserves++;
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

        private async Task<int> GetStaffPriorityWeightAsync(int personId, DateTime currentExamDate)
        {
            var yesterday = currentExamDate.AddDays(-1).Date;
            var dayBeforeYesterday = currentExamDate.AddDays(-2).Date;

            bool workedYesterday = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .AnyAsync(a => a.PersonId == personId && a.ExamSchedule.Exam.ExamDate.Date == yesterday);

            if (workedYesterday) return 0;

            bool workedDayBefore = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .AnyAsync(a => a.PersonId == personId && a.ExamSchedule.Exam.ExamDate.Date == dayBeforeYesterday);

            if (workedDayBefore) return 2;

            return 1;
        }
    }
}