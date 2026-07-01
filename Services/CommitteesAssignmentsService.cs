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
            var examDate = current.Exam.ExamDate.Date;
            var startTime = current.Exam.StartTime;
            var endTime = current.Exam.EndTime;

            var conflict = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam).ThenInclude(e => e.Subject)
                .Include(a => a.ExamLocation)
                .Where(a => a.ExamSchedule.ExamId != current.ExamId &&
                            (a.LocationId == currentLocId ||
                             a.ExamLocation.ParentLocationId == currentLocId ||
                             a.ExamLocation.ParentLocation.ParentLocationId == currentLocId ||
                             a.ExamLocation.ParentLocation.ParentLocation.ParentLocationId == currentLocId) &&
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
        // ✅ جديد: CheckRelativeConflictAsync
        // يمنع تعيين أي شخص له صلة قرابة (في جدول Relatives) بطالب من نفس الفرقة/المستوى 
        // الدراسي لمادة الامتحان المطلوب تكليفه فيها، بغض النظر عن المادة أو اللجنة أو الدور.
        // طول ما فرقة الطالب القريب بتمتحن (أي مادة من مواد الفرقة دي) يُمنع الشخص تماماً.
        // ========================================================================
        public async Task<string> CheckRelativeConflictAsync(int personId, int examScheduleId)
        {
            var schedule = await _context.ExamSchedules
                .Include(s => s.Exam).ThenInclude(e => e.Subject)
                .FirstOrDefaultAsync(s => s.ExamScheduleId == examScheduleId);

            if (schedule == null || schedule.Exam?.Subject == null)
                return null;

            var targetYear = schedule.Exam.Subject.AcademicYear;

            bool hasConflict = await _context.Relatives
                .Include(r => r.Student)
                .AnyAsync(r => r.PersonId == personId && r.Student != null && r.Student.AcademicYear == targetYear);

            if (hasConflict)
            {
                string yearName = targetYear switch
                {
                    AcademicLevel.FirstYear => "المستوى الأول",
                    AcademicLevel.SecondYear => "المستوى الثاني",
                    AcademicLevel.ThirdYear => "المستوى الثالث",
                    AcademicLevel.FourthYear => "المستوى الرابع",
                    _ => "هذا المستوى"
                };

                return $"عفواً، لا يمكن تعيين هذا الشخص لأن لديه صلة قرابة بطالب في ({yearName})، ولا يجوز تكليفه في أي دور أو لجنة أو جراش بهذه الفرقة بالكامل مهما اختلفت المادة.";
            }

            return null;
        }

        // ========================================================================
        // 2. RunAssignmentAsync: ماكينة التوزيع الذكية الشجرية المتوافقة مع السقوف الديناميكية
        // ========================================================================
        public async Task<bool> RunAssignmentAsync(int examScheduleId)
        {
            var currentSchedule = await _context.ExamSchedules
                .Include(s => s.Exam).ThenInclude(e => e.Subject)
                .Include(s => s.ExamLocation)
                .FirstOrDefaultAsync(s => s.ExamScheduleId == examScheduleId);

            if (currentSchedule == null || currentSchedule.Exam == null || currentSchedule.ExamLocation == null)
                return false;

            var currentLocId = currentSchedule.LocationId; // كود الجراش الموحد
            var examId = currentSchedule.ExamId;
            var examDate = currentSchedule.Exam.ExamDate.Date;
            string currentYearCode = "2025 / 2026";

            // 1. تنظيف التوزيع التلقائي القديم للجراش وفروعه بالكامل
            var oldAutoAssignments = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule)
                .Include(a => a.ExamLocation)
                .Where(a => a.ExamSchedule.ExamId == examId &&
                            (a.LocationId == currentLocId ||
                             a.ExamLocation.ParentLocationId == currentLocId ||
                             a.ExamLocation.ParentLocation.ParentLocationId == currentLocId ||
                             a.ExamLocation.ParentLocation.ParentLocation.ParentLocationId == currentLocId) &&
                            a.AssignmentType == "Auto")
                .ToListAsync();

            if (oldAutoAssignments.Any())
            {
                _context.CommitteesAssignments.RemoveRange(oldAutoAssignments);
                await _context.SaveChangesAsync();
            }

            // 2. سحب فروع المقرات (صفوف -> صالات -> لجان) التابعة للجراش
            var activeRows = await _context.ExamLocations
                .Where(x => x.Type == LocationType.Row && x.ParentLocationId == currentLocId)
                .OrderBy(x => x.LocationName)
                .ToListAsync();

            if (!activeRows.Any()) return false;

            var activeRowIds = activeRows.Select(r => r.LocationId).ToList();
            var activeBlocks = await _context.ExamLocations
                .Where(x => x.Type == LocationType.Block && x.ParentLocationId != null && activeRowIds.Contains(x.ParentLocationId.Value))
                .OrderBy(x => x.LocationName)
                .ToListAsync();

            if (!activeBlocks.Any()) return false;

            var activeBlockIds = activeBlocks.Select(b => b.LocationId).ToList();
            var activeCommitteeIds = await _context.ExamLocations
                .Where(x => x.Type == LocationType.Committee && x.ParentLocationId != null && activeBlockIds.Contains(x.ParentLocationId.Value))
                .Select(x => x.LocationId)
                .ToListAsync();

            // ✅ تصحيح جوهري: استبعاد أي موظف له صلة قرابة بأي طالب في نفس فرقة/مستوى مادة 
            // الامتحان الحالية بالكامل، مش بس طلاب نفس الجراش أو نفس اللجنة.
            // يعني لو قريبه في المستوى التاني، يتمنع من كل تكليف في المستوى التاني كله 
            // بغض النظر عن الجراش أو المادة أو اللجنة.
            var targetAcademicYear = currentSchedule.Exam.Subject?.AcademicYear;

            var excludedPersonIdsDueToRelatives = targetAcademicYear.HasValue
                ? await _context.Relatives
                    .Where(r => r.Student != null && r.Student.AcademicYear == targetAcademicYear.Value)
                    .Select(r => r.PersonId)
                    .Distinct()
                    .ToListAsync()
                : new List<int>();

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

            // جلب سقوف التكليفات السنوية ديناميكياً من جدول AssignmentSettings لمنع تخطي الحدود
            var limitsDict = await _context.AssignmentSettings
                .Where(s => s.AcademicYearCode == currentYearCode)
                .ToDictionaryAsync(s => s.JobRole, s => s.MaxAssignmentsLimit);

            var finalAssignments = new List<CommitteesAssignment>();
            var random = new Random();

            var currentDayHallPriorityStaff = new List<Person>();
            var historyPriorityStaff = new List<Person>();
            var normalStaff = new List<Person>();
            var lowPriorityStaff = new List<Person>();

            foreach (var staff in allAvailableStaff)
            {
                // ✅ هنا تم تصحيح الخطأ التجميعي ليعتمد الفحص على القاموس بشكل صحيح وديناميكي
                if (staff.JobRole == JobTitle.Professor ||
                    staff.JobRole == JobTitle.AssistantProfessor ||
                    staff.JobRole == JobTitle.ProfessorEmeritus)
                {
                    int totalAssignmentsCount = await _context.CommitteesAssignments.CountAsync(a => a.PersonId == staff.PersonId);
                    int allowedLimit = limitsDict.ContainsKey(staff.JobRole) ? limitsDict[staff.JobRole] : 4;

                    if (totalAssignmentsCount >= allowedLimit)
                    {
                        continue; // تخطي الشخص وتجاوز السقف فوراً
                    }
                }

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

            // تجميع وتصنيف الرتب الوظيفية لقادة اللجان والجراشات
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

            // توزيع رؤساء الجراشات والقطاعات الفرعية بناءً على خيار (دور واحد أو دورين)
            bool hasTwoFloors = (currentSchedule.ExamLocation.Floor == 2);
            int totalManagersNeeded = hasTwoFloors ? 5 : 3;

            if (poolManagers.Count < totalManagersNeeded) return false;

            for (int i = 0; i < totalManagersNeeded; i++)
            {
                var manager = poolManagers[i];
                string roleType = "رئيس جراش";
                string subRole = "";

                if (hasTwoFloors)
                {
                    subRole = i switch
                    {
                        0 => "قطاع أول",
                        1 => "قطاع ثاني",
                        2 => "قطاع ثالث",
                        3 => "احتياطي دور أول",
                        4 => "احتياطي دور ثاني",
                        _ => "رئيس"
                    };
                    if (i >= 3) roleType = "رئيس جراش احتياطي";
                }
                else
                {
                    subRole = i switch
                    {
                        0 => "قطاع أول",
                        1 => "قطاع ثاني",
                        2 => "احتياطي",
                        _ => "رئيس"
                    };
                    if (i == 2) roleType = "رئيس جراش احتياطي";
                }

                finalAssignments.Add(new CommitteesAssignment
                {
                    PersonId = manager.PersonId,
                    ExamScheduleId = examScheduleId,
                    RoleId = manager.RoleId,
                    LocationId = currentLocId,
                    AssignmentType = "Auto",
                    RoleType = roleType,
                    SubRoleType = subRole
                });
            }

            // توزيع المراقبين الاحتياطيين للأدوار والمبنى
            int reserveLeadersCount = hasTwoFloors ? 2 : 1;
            var reserveLeaders = poolLeaders.Take(reserveLeadersCount).ToList();

            foreach (var res in reserveLeaders)
            {
                int index = reserveLeaders.IndexOf(res);
                string roleType = "مراقب احتياطي";
                string subRole = hasTwoFloors ? (index == 0 ? "الدور الأول" : "الدور الثاني") : "لجراش";

                finalAssignments.Add(new CommitteesAssignment
                {
                    PersonId = res.PersonId,
                    ExamScheduleId = examScheduleId,
                    RoleId = res.RoleId,
                    LocationId = currentLocId,
                    AssignmentType = "Auto",
                    RoleType = roleType,
                    SubRoleType = subRole
                });
                poolLeaders.Remove(res);
            }

            // توزيع الطواقم الطبية المسؤولة عن الجراش
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

            // هندسة التوزيع الشجري المتتابع: الدوران صفاً صفاً وتغطية صالاته ولجانه الداخلية بالكامل
            foreach (var row in activeRows)
            {
                var blocksInRow = activeBlocks
                    .Where(b => b.ParentLocationId == row.LocationId)
                    .OrderBy(b => b.LocationName)
                    .ToList();

                foreach (var block in blocksInRow)
                {
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

                    var committeesInBlock = await _context.ExamLocations
                        .Where(c => c.ParentLocationId == block.LocationId && c.Type == LocationType.Committee)
                        .OrderBy(c => c.LocationName)
                        .ToListAsync();

                    foreach (var com in committeesInBlock)
                    {
                        var obs = poolObservers
                            .FirstOrDefault(p => !yesterdayAssignments.Any(y => y.PersonId == p.PersonId && y.LocationId == com.LocationId));

                        if (obs == null) obs = poolObservers.FirstOrDefault();

                        if (obs != null)
                        {
                            finalAssignments.Add(new CommitteesAssignment
                            {
                                PersonId = obs.PersonId,
                                ExamScheduleId = examScheduleId,
                                LocationId = com.LocationId,
                                AssignmentType = "Auto",
                                RoleType = "ملاحظ لجنة",
                                RoleId = obs.RoleId
                            });
                            poolObservers.Remove(obs);
                        }
                    }
                }
            }

            // توزيع الملاحظين الاحتياطيين بالصالات بنسبة 5% بعد الفراغ من التغطية الهرمية
            int distributedReserves = 0;
            int totalCommitteesCount = activeCommitteeIds.Count;
            int requiredReserveObserversCount = (int)Math.Ceiling(totalCommitteesCount * 0.05);
            if (requiredReserveObserversCount < 1) requiredReserveObserversCount = 1;

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
