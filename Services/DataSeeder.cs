using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using projectweb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace projectweb.Services
{
    public static class DataSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await context.Database.MigrateAsync();
            var rng = new Random(123456);

            // طباعة للفحص في الـ Output window
            Console.WriteLine("====== بدء فحص قاعدة البيانات من الـ Seeder ======");
            Console.WriteLine($"عدد الجداول (Schedules) المكتشفة: {context.ExamSchedules.Count()}");
            Console.WriteLine($"عدد الأشخاص (Persons) المكتشفين: {context.Persons.Count()}");
            Console.WriteLine($"عدد الأماكن (Locations) المكتشفة: {context.ExamLocations.Count()}");
            Console.WriteLine($"عدد الأدوار (Roles) المكتشفة: {context.Roles.Count()}");

            // جلب البيانات
            var allSchedules = context.ExamSchedules.ToList();
            var allPersons = context.Persons.ToList();

            if (!allSchedules.Any() || !allPersons.Any())
            {
                Console.WriteLine("⚠️ تحذير: الجداول فارغة تماماً! الكود لن يقوم بالتوزيع.");
                Console.WriteLine("==================================================");
                return;
            }

            // التوزيع الإجباري (بدون شروط حماية مؤقتاً لتجربة السمع)
            foreach (var sched in allSchedules)
            {
                var assignedPeople = new System.Collections.Generic.HashSet<int>();
                var candidates = allPersons.AsEnumerable().OrderBy(_ => rng.Next()).Take(12).ToList();

                foreach (var person in candidates)
                {
                    if (context.CommitteesAssignments.Count(a => a.ExamScheduleId == sched.ExamScheduleId) >= 4) break;
                    if (assignedPeople.Contains(person.PersonId)) continue;
                    if (context.CommitteesAssignments.Any(a => a.PersonId == person.PersonId && a.ExamScheduleId == sched.ExamScheduleId)) continue;

                    int? assignLocationId = null;
                    var schedLocation = context.ExamLocations.Find(sched.LocationId);
                    if (schedLocation != null)
                    {
                        if (schedLocation.Type == LocationType.Committee) assignLocationId = schedLocation.LocationId;
                        else
                        {
                            var child = context.ExamLocations
                                .Where(l => l.ParentLocationId == schedLocation.LocationId && l.Type == LocationType.Committee)
                                .AsEnumerable()
                                .OrderBy(_ => rng.Next())
                                .FirstOrDefault();
                            if (child != null) assignLocationId = child.LocationId;
                        }
                    }

                    var roleId = context.Roles.AsEnumerable().OrderBy(_ => rng.Next()).FirstOrDefault()?.RoleID ?? 1;

                    var a = new CommitteesAssignment
                    {
                        PersonId = person.PersonId,
                        ExamScheduleId = sched.ExamScheduleId,
                        RoleId = roleId,
                        LocationId = assignLocationId,
                        AssignmentType = "توزيع يدوي"
                    };
                    context.CommitteesAssignments.Add(a);
                    assignedPeople.Add(person.PersonId);
                }
            }
            await context.SaveChangesAsync();

            // توليد التقارير
            foreach (var sched in allSchedules.Take(100))
            {
                if (!context.Reports.Any(r => r.ScheduleId == sched.ExamScheduleId))
                {
                    var commLocation = context.ExamLocations.Find(sched.LocationId);
                    context.Reports.Add(new Report
                    {
                        CreatedDate = DateTime.Now.AddDays(-rng.Next(0, 30)),
                        Status = ReportStatus.Normal,
                        ScheduleId = sched.ExamScheduleId,
                        LocationId = commLocation?.LocationId ?? sched.LocationId
                    });
                }
            }
            await context.SaveChangesAsync();

            // توليد الموقعين
            var allReportsFinal = context.Reports.ToList();
            foreach (var rep in allReportsFinal)
            {
                var signers = allPersons.OrderBy(_ => rng.Next()).Take(2).ToList();
                foreach (var signer in signers)
                {
                    if (!context.ReportPersons.Any(rp => rp.ReportId == rep.ReportId && rp.PersonId == signer.PersonId))
                    {
                        context.ReportPersons.Add(new ReportPerson
                        {
                            ReportId = rep.ReportId,
                            PersonId = signer.PersonId,
                            SignedAt = rep.CreatedDate.AddMinutes(rng.Next(1, 120)),
                            RoleId = context.Roles.AsEnumerable().OrderBy(_ => rng.Next()).FirstOrDefault()?.RoleID ?? 1,
                            Signature = "ممضى إلكترونياً"
                        });
                    }
                }
            }
            await context.SaveChangesAsync();
            Console.WriteLine("✅ تم التوزيع وملء الجداول بنجاح عظيم!");
            Console.WriteLine("==================================================");
        }
    }
}