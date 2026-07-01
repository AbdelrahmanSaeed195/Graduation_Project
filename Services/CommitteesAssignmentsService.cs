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

        // ========================================================================
        // 1. CheckTimeConflictAsync: فحص تعارضات الوقت في المقر الموحد
        // ========================================================================
        public async Task<string> CheckTimeConflictAsync(int examScheduleId)
        {
            var current = await _context.ExamSchedules
                .Include(s => s.Exam).ThenInclude(e => e.Subject)
                .Include(s => s.ExamLocation)
                .FirstOrDefaultAsync(s => s.ExamScheduleId == examScheduleId);

            if (current == null || current.Exam == null || current.ExamLocation == null)
                return "الجلسة المطلوبة غير موجودة.";

            var currentLocId = current.LocationId;
            var parentLocId = current.ExamLocation.ParentLocationId;
            var examDate = current.Exam.ExamDate.Date;
            var startTime = current.Exam.StartTime;
            var endTime = current.Exam.EndTime;

            var conflict = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam).ThenInclude(e => e.Subject)
                .Include(a => a.ExamLocation)
                .Where(a => a.ExamSchedule.ExamId != current.ExamId &&
                            (a.LocationId == currentLocId ||
                             a.LocationId == parentLocId ||
                             a.ExamLocation.ParentLocationId == currentLocId ||
                             a.ExamLocation.ParentLocation.ParentLocationId == parentLocId) &&
                            a.ExamSchedule.Exam.ExamDate.Date == examDate &&
                            ((startTime >= a.ExamSchedule.Exam.StartTime && startTime < a.ExamSchedule.Exam.EndTime) ||
                             (endTime > a.ExamSchedule.Exam.StartTime && endTime <= a.ExamSchedule.Exam.EndTime)))
                .Select(a => a.ExamSchedule.Exam.Subject.SubjectName)
                .Distinct()
                .FirstOrDefaultAsync();

            if (conflict != null)
            {
                return $"عفواً، لا يمكن إجراء التوزيع. هذا المقر مشغول حالياً بامتحان مادة ({conflict}) في نفس الفترة الزمنية.";
            }

            return null;
        }

        // ========================================================================
        // 2. RunAssignmentAsync: تشغيل ماكينة التوزيع التلقائي وهندسة المقاعد والأقارب
        // ========================================================================
        public async Task<bool> RunAssignmentAsync(int examScheduleId)
        {
            var currentSchedule = await _context.ExamSchedules
                .Include(s => s.Exam)
                .Include(s => s.ExamLocation)
                .FirstOrDefaultAsync(s => s.ExamScheduleId == examScheduleId);

            if (currentSchedule == null || currentSchedule.Exam == null || currentSchedule.ExamLocation == null)
                return false;

            var currentLocId = currentSchedule.LocationId;
            var examId = currentSchedule.ExamId;
            var examDate = currentSchedule.Exam.ExamDate.Date;

            // 1. تنظيف التوزيع التلقائي القديم
            var oldAutoAssignments = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule)
                .Include(a => a.ExamLocation)
                .Where(a => a.ExamSchedule.ExamId == examId &&
                            (a.LocationId == currentLocId || a.ExamLocation.ParentLocationId == currentLocId || a.ExamLocation.ParentLocation.ParentLocationId == currentLocId) &&
                            a.AssignmentType == "Auto")
                .ToListAsync();

            if (oldAutoAssignments.Any())
            {
                _context.CommitteesAssignments.RemoveRange(oldAutoAssignments);
                await _context.SaveChangesAsync();
            }

            var scheduledSchedules = await _context.ExamSchedules
     .Include(es => es.ExamLocation)
         .ThenInclude(l => l.ParentLocation)
     .Where(es => es.ExamId == examId &&
         (es.LocationId == currentLocId ||
          es.ExamLocation.ParentLocationId == currentLocId))
     .ToListAsync();

            if (!scheduledSchedules.Any()) return false;

            // استخراج الجراشات المستخدمة
            var hallIds = scheduledSchedules
                .Select(s =>
                {
                    if (s.ExamLocation.Type == LocationType.Hall)
                        return s.ExamLocation.LocationId;

                    if (s.ExamLocation.Type == LocationType.Row)
                        return s.ExamLocation.ParentLocationId;

                    if (s.ExamLocation.Type == LocationType.Block)
                        return s.ExamLocation.ParentLocation?.ParentLocationId;

                    if (s.ExamLocation.Type == LocationType.Committee)
                        return s.ExamLocation.ParentLocation?.ParentLocation?.ParentLocationId;

                    return null;
                })
                .Where(x => x.HasValue)
                .Select(x => x.Value)
                .Distinct()
                .ToList();


            // الصفوف الموجودة داخل الجراشات
            var activeRows = await _context.ExamLocations
                .Where(x =>
                    x.Type == LocationType.Row &&
                    x.ParentLocationId != null &&
                    hallIds.Contains(x.ParentLocationId.Value))
                .OrderBy(x => x.LocationName)
                .ToListAsync();

            if (!activeRows.Any())
                return false;


            // الصالات الموجودة داخل الصفوف
            var activeBlocks = await _context.ExamLocations
                .Where(x =>
                    x.Type == LocationType.Block &&
                    x.ParentLocationId != null &&
                    activeRows.Select(r => r.LocationId).Contains(x.ParentLocationId.Value))
                .OrderBy(x => x.LocationName)
                .ToListAsync();

            if (!activeBlocks.Any())
                return false;


            // اللجان الموجودة داخل الصالات
            var activeCommitteeIds = await _context.ExamLocations
                .Where(x =>
                    x.Type == LocationType.Committee &&
                    x.ParentLocationId != null &&
                    activeBlocks.Select(b => b.LocationId).Contains(x.ParentLocationId.Value))
                .Select(x => x.LocationId)
                .ToListAsync();

            var studentIdsInHall = await _context.Students
                .Where(s => (s.LocationId != null && activeCommitteeIds.Contains(s.LocationId.Value)) || s.ExamScheduleId == examScheduleId)
                .Select(s => s.StudentId)
                .ToListAsync();

            var excludedPersonIdsDueToRelatives = await _context.Relatives
                .Where(r => studentIdsInHall.Contains(r.StudentId))
                .Select(r => r.PersonId)
                .Distinct()
                .ToListAsync();

            var busyPersonIds = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule)
                .Where(a => a.ExamSchedule.ExamId == examId)
                .Select(a => a.PersonId).ToListAsync();

            var allAvailableStaff = await _context.Persons
                .Include(p => p.Role)
                .Where(p => !busyPersonIds.Contains(p.PersonId)
                            && !excludedPersonIdsDueToRelatives.Contains(p.PersonId)
                            && p.IsActiveForAssignment)
                .ToListAsync();

            var staffWhoWorkedInThisHallToday = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .Include(a => a.ExamLocation)
                .Where(a => a.ExamSchedule.Exam.ExamDate.Date == examDate &&
                            (a.LocationId == currentLocId || a.ExamLocation.ParentLocationId == currentLocId || a.ExamLocation.ParentLocation.ParentLocationId == currentLocId))
                .Select(a => a.PersonId)
                .Distinct()
                .ToListAsync();

            var yesterday = examDate.AddDays(-1).Date;
            var yesterdayAssignments = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .Where(a => a.ExamSchedule.Exam.ExamDate.Date == yesterday && a.LocationId != null)
                .Select(a => new { a.PersonId, LocationId = a.LocationId.Value })
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

            var poolManagers = new List<Person>();
            var managerRoles = new HashSet<JobTitle> { JobTitle.Professor, JobTitle.AssistantProfessor, JobTitle.ProfessorEmeritus };
            poolManagers.AddRange(currentDayHallPriorityStaff.Where(p => managerRoles.Contains(p.JobRole)).OrderBy(p => random.Next()));
            poolManagers.AddRange(historyPriorityStaff.Where(p => managerRoles.Contains(p.JobRole)).OrderBy(p => random.Next()));
            poolManagers.AddRange(normalStaff.Where(p => managerRoles.Contains(p.JobRole)).OrderBy(p => random.Next()));
            poolManagers.AddRange(lowPriorityStaff.Where(p => managerRoles.Contains(p.JobRole)).OrderBy(p => random.Next()));

            var poolLeaders = new List<Person>();
            poolLeaders.AddRange(currentDayHallPriorityStaff.Where(p => p.JobRole == JobTitle.StaffObserver).OrderBy(p => random.Next()));
            poolLeaders.AddRange(historyPriorityStaff.Where(p => p.JobRole == JobTitle.StaffObserver).OrderBy(p => random.Next()));
            poolLeaders.AddRange(normalStaff.Where(p => p.JobRole == JobTitle.StaffObserver).OrderBy(p => random.Next()));
            poolLeaders.AddRange(lowPriorityStaff.Where(p => p.JobRole == JobTitle.StaffObserver).OrderBy(p => random.Next()));

            var poolObservers = new List<Person>();
            var observerRoles = new HashSet<JobTitle> { JobTitle.Assistant, JobTitle.AssistantStaff, JobTitle.Employee };
            poolObservers.AddRange(currentDayHallPriorityStaff.Where(p => observerRoles.Contains(p.JobRole)).OrderBy(p => random.Next()));
            poolObservers.AddRange(historyPriorityStaff.Where(p => observerRoles.Contains(p.JobRole)).OrderBy(p => random.Next()));
            poolObservers.AddRange(normalStaff.Where(p => observerRoles.Contains(p.JobRole)).OrderBy(p => random.Next()));
            poolObservers.AddRange(lowPriorityStaff.Where(p => observerRoles.Contains(p.JobRole)).OrderBy(p => random.Next()));

            var poolDoctors = allAvailableStaff.Where(p => p.JobRole == JobTitle.Doctor).OrderBy(p => random.Next()).ToList();
            var poolNurses = allAvailableStaff.Where(p => p.JobRole == JobTitle.Nurse).OrderBy(p => random.Next()).ToList();

            int mainHallId = currentSchedule.ExamLocation.Type == LocationType.Hall
                 ? currentSchedule.ExamLocation.LocationId
                 : (currentSchedule.ExamLocation.ParentLocation?.ParentLocationId
                    ?? currentSchedule.ExamLocation.ParentLocationId
                    ?? currentLocId);

            // 2. جلب الكيان الرئيسي بالكامل من قاعدة البيانات لفحص حقله الفعلي
            // توزيع رؤساء اللجان على الصفوف

            // =======================
            // حساب عدد رؤساء اللجان المطلوبين
            // =======================

            int reserveManagers = activeRows.Count <= 2 ? 1 : 2;
            int totalManagersNeeded = activeRows.Count + reserveManagers;

            if (poolManagers.Count < totalManagersNeeded)
                return false;


            // =======================
            // توزيع رؤساء اللجان الأساسيين
            // =======================

            foreach (var row in activeRows)
            {
                var manager = poolManagers.First();

                finalAssignments.Add(new CommitteesAssignment
                {
                    PersonId = manager.PersonId,
                    ExamScheduleId = examScheduleId,
                    RoleId = manager.RoleId,
                    LocationId = row.LocationId,
                    AssignmentType = "Auto",
                    RoleType = "رئيس لجنة",
                    SubRoleType = row.LocationName
                });

                poolManagers.Remove(manager);
            }


            // =======================
            // توزيع رؤساء اللجان الاحتياطيين
            // =======================

            for (int i = 0; i < reserveManagers; i++)
            {
                if (!poolManagers.Any())
                    break;

                var reserve = poolManagers.First();

                finalAssignments.Add(new CommitteesAssignment
                {
                    PersonId = reserve.PersonId,
                    ExamScheduleId = examScheduleId,
                    RoleId = reserve.RoleId,
                    LocationId = currentLocId,
                    AssignmentType = "Auto",
                    RoleType = "رئيس لجنة احتياطي",
                    SubRoleType = $"احتياطي {i + 1}"
                });

                poolManagers.Remove(reserve);
            }

            // 3. توزيع المراقبين الاحتياطيين
            // توزيع مراقب احتياطي واحد لكل جراش

            int reserveCount = activeRows.Count <= 2 ? 1 : 2;

            var reserveLeaders = poolLeaders.Take(reserveCount).ToList();

            foreach (var reserve in reserveLeaders)
            {
                finalAssignments.Add(new CommitteesAssignment
                {
                    PersonId = reserve.PersonId,
                    ExamScheduleId = examScheduleId,
                    RoleId = reserve.RoleId,
                    LocationId = currentLocId,
                    AssignmentType = "Auto",
                    RoleType = "مراقب احتياطي",
                    SubRoleType = "احتياطي لجته"
                });

                poolLeaders.Remove(reserve);
            }

            // حساب الـ 5% ملاحظين احتياطيين
            int totalCommitteesCount = await _context.ExamSchedules
                .Include(es => es.ExamLocation)
                .Where(es => es.ExamId == examId && (es.LocationId == currentLocId || es.ExamLocation.ParentLocationId == currentLocId))
                .CountAsync();

            int requiredReserveObserversCount = (int)Math.Ceiling(totalCommitteesCount * 0.05);
            if (requiredReserveObserversCount < 1) requiredReserveObserversCount = 1;

            // توزيع الطاقم الطبي
            var doctor = poolDoctors.FirstOrDefault();
            if (doctor != null)
            {
                finalAssignments.Add(new CommitteesAssignment { PersonId = doctor.PersonId, ExamScheduleId = examScheduleId, LocationId = currentLocId, AssignmentType = "Auto", RoleType = "دكتور", RoleId = doctor.RoleId });
            }

            var nurse = poolNurses.FirstOrDefault();
            if (nurse != null)
            {
                finalAssignments.Add(new CommitteesAssignment { PersonId = nurse.PersonId, ExamScheduleId = examScheduleId, LocationId = currentLocId, AssignmentType = "Auto", RoleType = "مساعد دكتور", RoleId = nurse.RoleId });
            }

            // 4. توزيع المراقبين الأساسيين والملاحظين
            // توزيع المراقبين والملاحظين حسب الصفوف

            foreach (var row in activeRows)
            {
                // الصالات الموجودة داخل الصف الحالي
                var blocksInRow = activeBlocks
                    .Where(b => b.ParentLocationId == row.LocationId)
                    .OrderBy(b => b.LocationName)
                    .ToList();

                foreach (var block in blocksInRow)
                {
                    // ===============================
                    // توزيع مراقب على الصالة
                    // ===============================

                    var blockLeader = poolLeaders.FirstOrDefault();

                    if (blockLeader != null)
                    {
                        finalAssignments.Add(new CommitteesAssignment
                        {
                            PersonId = blockLeader.PersonId,
                            ExamScheduleId = examScheduleId,
                            LocationId = block.LocationId,
                            AssignmentType = "Auto",
                            RoleType = "مراقب",
                            RoleId = blockLeader.RoleId
                        });

                        poolLeaders.Remove(blockLeader);
                    }

                    // ===============================
                    // توزيع الملاحظين على اللجان
                    // ===============================

                    var committees = await _context.ExamLocations
                        .Where(c =>
                            c.ParentLocationId == block.LocationId &&
                            c.Type == LocationType.Committee)
                        .OrderBy(c => c.LocationName)
                        .ToListAsync();

                    foreach (var committee in committees)
                    {
                        var observer = poolObservers
                            .FirstOrDefault(p =>
                                !yesterdayAssignments.Any(y =>
                                    y.PersonId == p.PersonId &&
                                    y.LocationId == committee.LocationId));

                        observer ??= poolObservers.FirstOrDefault();

                        if (observer == null)
                            continue;

                        finalAssignments.Add(new CommitteesAssignment
                        {
                            PersonId = observer.PersonId,
                            ExamScheduleId = examScheduleId,
                            LocationId = committee.LocationId,
                            AssignmentType = "Auto",
                            RoleType = "ملاحظ لجنة",
                            RoleId = observer.RoleId
                        });

                        poolObservers.Remove(observer);
                    }
                }
            }

            // 5. توزيع الملاحظين الاحتياطيين بنسبة 5%
            int distributedReserves = 0;
            while (distributedReserves < requiredReserveObserversCount && poolObservers.Any())
            {
                foreach (var block in activeBlocks)
                {
                    if (distributedReserves >= requiredReserveObserversCount || !poolObservers.Any()) break;

                    var reserveNote = poolObservers.FirstOrDefault();
                    if (reserveNote != null)
                    {
                        finalAssignments.Add(new CommitteesAssignment
                        {
                            PersonId = reserveNote.PersonId,
                            ExamScheduleId = examScheduleId,
                            RoleId = reserveNote.RoleId,
                            LocationId = block.LocationId,
                            AssignmentType = "Auto",
                            RoleType = "ملاحظ احتياطي",
                            SubRoleType = "احتياطي صالة"
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