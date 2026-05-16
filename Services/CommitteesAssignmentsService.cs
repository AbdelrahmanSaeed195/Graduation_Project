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

        // =====================================
        // 1. التحقق من تعارض الوقت والصالة قبل التوزيع
        // =====================================
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

        // =====================================
        // 2. تنفيذ التوزيع التلقائي بناءً على المعايير المحددة
        // =====================================
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

            // 1. تطهير وتنظيف التوزيع التلقائي القديم لـ "هذا الامتحان المختار حالياً فقط" داخل هذه الصالة
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

            // 2. جلب اللجان النشطة التي نزل لها هذا الامتحان المحدد بالظبط اليوم
            var scheduledCommittees = await _context.ExamSchedules
                .Include(es => es.Committee).ThenInclude(c => c.Block)
                .Where(es => es.ExamId == examId && es.Committee.Block.HallId == hallId)
                .ToListAsync();

            if (!scheduledCommittees.Any()) return false;

            // استخراج البلوكات النشطة التي تقع بها امتحانات المادة الحالية
            var activeBlocks = scheduledCommittees
                .Select(es => es.Committee.Block)
                .GroupBy(b => b.BlockId)
                .Select(g => g.First())
                .OrderBy(b => b.BlockName)
                .ToList();

            // 3. جلب الأشخاص المتاحين (الذين ليس لديهم تكليف آخر في نفس الجلسة الزمنية الحالية)
            var busyPersonIds = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .Where(a => a.ExamSchedule.ExamId == examId)
                .Select(a => a.PersonId).ToListAsync();

            var allAvailableStaff = await _context.Persons
                .Include(p => p.Role)
                .Where(p => !busyPersonIds.Contains(p.PersonId) && p.IsActiveForAssignment)
                .ToListAsync();

            // --- 🌟 ميزة الذكاء الحركي 1: جلب الأشخاص الذين عملوا في نفس الصالة اليوم في فترات سابقة (لتثبيتهم بالكامل) ---
            var staffWhoWorkedInThisHallToday = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .Where(a => a.ExamSchedule.Exam.ExamDate.Date == examDate &&
                            ((a.HallId == hallId) || (a.Block.HallId == hallId) || (a.Committee.Block.HallId == hallId)))
                .Select(a => a.PersonId)
                .Distinct()
                .ToListAsync();

            // --- 🌟 ميزة الذكاء الحركي 2: جلب اللجان التي وقف فيها كل موظف بالأمس (لمنع وقوفه في نفس اللجان اليوم) ---
            var yesterday = examDate.AddDays(-1).Date;
            var yesterdayAssignments = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .Where(a => a.ExamSchedule.Exam.ExamDate.Date == yesterday && a.CommitteeId != null)
                .Select(a => new { a.PersonId, a.CommitteeId })
                .ToListAsync();

            var finalAssignments = new List<CommitteesAssignment>();
            var random = new Random();

            // تصنيف الطاقم لضمان تثبيت نفس اليوم وتدوير الأيام الماضية بشكل صارم
            var currentDayHallPriorityStaff = new List<Person>(); // نفس طاقم الصبح (أعلى أولوية إجبارية)
            var historyPriorityStaff = new List<Person>();        // ارتاحوا أمس وعملوا قبل أمس (أولوية جدول الأيام)
            var normalStaff = new List<Person>();                 // طاقم عادي
            var lowPriorityStaff = new List<Person>();            // عملوا بالأمس (أقل أولوية - راحة إن أمكن)

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

            // فرز وبناء خزان الرؤساء مع تقديم طاقم اليوم الحالي أولاً بشكل صارم
            var hallManagersPool = currentDayHallPriorityStaff
                .Where(p => p.JobRole == JobTitle.ProfessorEmeritus || p.JobRole == JobTitle.AssistantProfessor || p.JobRole == JobTitle.Professor)
                .OrderBy(p => random.Next()).ToList();

            if (hallManagersPool.Count < 3)
            {
                var extraHistory = historyPriorityStaff
                    .Where(p => p.JobRole == JobTitle.ProfessorEmeritus || p.JobRole == JobTitle.AssistantProfessor || p.JobRole == JobTitle.Professor)
                    .OrderBy(p => random.Next()).Take(3 - hallManagersPool.Count);
                hallManagersPool.AddRange(extraHistory);
            }
            if (hallManagersPool.Count < 3)
            {
                var extraNormal = normalStaff
                    .Where(p => p.JobRole == JobTitle.ProfessorEmeritus || p.JobRole == JobTitle.AssistantProfessor || p.JobRole == JobTitle.Professor)
                    .OrderBy(p => random.Next()).Take(3 - hallManagersPool.Count);
                hallManagersPool.AddRange(extraNormal);
            }
            if (hallManagersPool.Count < 3)
            {
                var extraLow = lowPriorityStaff
                    .Where(p => p.JobRole == JobTitle.ProfessorEmeritus || p.JobRole == JobTitle.AssistantProfessor || p.JobRole == JobTitle.Professor)
                    .OrderBy(p => random.Next()).Take(3 - hallManagersPool.Count);
                hallManagersPool.AddRange(extraLow);
            }

            // إعادة توحيد وترتيب طاقم الملاحظين والمراقبين (المتاحين لليوم الحالي هم في الصدارة حتماً لضمان الثبات)
            var structuredAvailableStaff = new List<Person>();
            structuredAvailableStaff.AddRange(currentDayHallPriorityStaff.OrderBy(p => random.Next()));
            structuredAvailableStaff.AddRange(historyPriorityStaff.OrderBy(p => random.Next()));
            structuredAvailableStaff.AddRange(normalStaff.OrderBy(p => random.Next()));
            structuredAvailableStaff.AddRange(lowPriorityStaff.OrderBy(p => random.Next()));

            // اختيار الـ 3 مدراء المطلوبين
            var selectedManagers = hallManagersPool.Take(3).ToList();
            if (selectedManagers.Count < 2) return false;

            int totalActiveBlocks = activeBlocks.Count;
            int half = (int)Math.Ceiling((double)totalActiveBlocks / 2);

            var firstSectionBlocks = activeBlocks.Take(half).ToList();
            var secondSectionBlocks = activeBlocks.Skip(half).ToList();

            // --- أ- توزيع رؤساء الصالات (أستاذ متفرغ / مساعد / أستاذ) واحتياطييهم ---
            for (int i = 0; i < selectedManagers.Count; i++)
            {
                var manager = selectedManagers[i];
                structuredAvailableStaff.Remove(manager);

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

                // تعيين الاحتياطيين التابعين لكل رئيس صالة أساسي
                if (i < 2)
                {
                    var resObserver = structuredAvailableStaff.FirstOrDefault(p => p.JobRole == JobTitle.Assistant); // معيد فقط
                    if (resObserver != null)
                    {
                        finalAssignments.Add(new CommitteesAssignment
                        {
                            PersonId = resObserver.PersonId,
                            ExamScheduleId = examScheduleId,
                            RoleId = resObserver.RoleId,
                            HallId = hallId,
                            BlockId = null,
                            AssignmentType = "Auto",
                            RoleType = $"مراقب احتياطي - تبع رئيس {i + 1}"
                        });
                        structuredAvailableStaff.Remove(resObserver);
                    }

                    for (int k = 0; k < 2; k++)
                    {
                        var note = structuredAvailableStaff.FirstOrDefault(p => p.JobRole == JobTitle.Employee); // موظف فقط
                        if (note != null)
                        {
                            finalAssignments.Add(new CommitteesAssignment
                            {
                                PersonId = note.PersonId,
                                ExamScheduleId = examScheduleId,
                                RoleId = note.RoleId,
                                HallId = hallId,
                                BlockId = null,
                                AssignmentType = "Auto",
                                RoleType = $"ملاحظ احتياطي - تبع رئيس {i + 1}"
                            });
                            structuredAvailableStaff.Remove(note);
                        }
                    }
                }
            }

            // --- ب- توزيع الطاقم الطبي الأساسي (دكتور ل دكتور وممرض ل ممرض) ---
            var doctor = structuredAvailableStaff.FirstOrDefault(p => p.JobRole == JobTitle.Doctor);
            if (doctor != null)
            {
                finalAssignments.Add(new CommitteesAssignment { PersonId = doctor.PersonId, ExamScheduleId = examScheduleId, HallId = hallId, BlockId = null, CommitteeId = null, AssignmentType = "Auto", RoleType = "دكتور الصالة", RoleId = doctor.RoleId });
                structuredAvailableStaff.Remove(doctor);
            }

            var nurse = structuredAvailableStaff.FirstOrDefault(p => p.JobRole == JobTitle.Nurse);
            if (nurse != null)
            {
                finalAssignments.Add(new CommitteesAssignment { PersonId = nurse.PersonId, ExamScheduleId = examScheduleId, HallId = hallId, BlockId = null, CommitteeId = null, AssignmentType = "Auto", RoleType = "ممرض الصالة", RoleId = nurse.RoleId });
                structuredAvailableStaff.Remove(nurse);
            }

            // --- ج- توزيع المراقبين (المعيدين للبلوكات) والملاحظين (الموظفين للجان النشطة) ---
            foreach (var block in activeBlocks)
            {
                var activeCommitteesInBlock = scheduledCommittees.Where(es => es.Committee.BlockId == block.BlockId).ToList();

                if (activeCommitteesInBlock.Any())
                {
                    // المراقب يكون من فئة (معيد Assistant) واحد فقط وصريح يشرف على البلوك بالكامل
                    var blockLeader = structuredAvailableStaff.FirstOrDefault(p => p.JobRole == JobTitle.Assistant);
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
                        structuredAvailableStaff.Remove(blockLeader);
                    }

                    // الملاحظ يكون من فئة (موظف Employee) -> ملاحظ واحد فقط وصريح لكل لجنة نشطة وموزع عليها امتحان
                    foreach (var com in activeCommitteesInBlock)
                    {
                        // 🌟 ميزة التدوير ومنع تكرار لجنة الأمس: البحث عن موظف متاح لم يكن في هذه اللجنة بالأمس
                        var obs = structuredAvailableStaff
                            .FirstOrDefault(p => p.JobRole == JobTitle.Employee &&
                                                !yesterdayAssignments.Any(y => y.PersonId == p.PersonId && y.CommitteeId == com.CommitteeId));

                        // في حال غياب طاقم بديل كافي (حالة طوارئ)، نسحب أول موظف متاح حتى لو تكررت لجنته لضمان سير الامتحان
                        if (obs == null)
                        {
                            obs = structuredAvailableStaff.FirstOrDefault(p => p.JobRole == JobTitle.Employee);
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
                            structuredAvailableStaff.Remove(obs); // السحب الفوري النهائي الحاسم لمنع التكرار
                        }
                    }
                }
            }

            // 4. الحفظ النهائي الصريح للمادة الحالية في قاعدة البيانات
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