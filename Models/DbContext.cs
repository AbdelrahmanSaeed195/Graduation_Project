using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using System.Linq;

namespace projectweb.Models
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Person> Persons { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Relative> Relatives { get; set; }
        public DbSet<Role> Roles { get; set; }

        // الجدول الموحد البديل للهيكل المكاني
        public DbSet<ExamLocation> ExamLocations { get; set; }

        public DbSet<CommitteesAssignment> CommitteesAssignments { get; set; }
        public DbSet<Exam> Exams { get; set; }
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<ExamSchedule> ExamSchedules { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<ReportPerson> ReportPersons { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // بيانات الأدوار الثابتة
            modelBuilder.Entity<Role>().HasData(
                 new Role { RoleID = 1, RoleName = StaffPosition.HallManager, RoleDescription = "رئيس جراش (أستاذ)" },
                 new Role { RoleID = 2, RoleName = StaffPosition.HallManager, RoleDescription = "رئيس جراش (أستاذ مساعد)" },
                 new Role { RoleID = 3, RoleName = StaffPosition.HallManager, RoleDescription = "رئيس جراش (أستاذ متفرغ)" },
                 new Role { RoleID = 4, RoleName = StaffPosition.BlockGroupLeader, RoleDescription = "مراقب (مدرس)" },
                 new Role { RoleID = 5, RoleName = StaffPosition.CommitteeObserver, RoleDescription = "ملاحظ (معيد)" },
                 new Role { RoleID = 6, RoleName = StaffPosition.CommitteeObserver, RoleDescription = "ملاحظ (مدرس مساعد)" },
                 new Role { RoleID = 7, RoleName = StaffPosition.CommitteeObserver, RoleDescription = "ملاحظ (موظف)" },
                 new Role { RoleID = 8, RoleName = StaffPosition.Doctor, RoleDescription = "دكتور" },
                 new Role { RoleID = 9, RoleName = StaffPosition.Nurse, RoleDescription = " مساعد دكتور" }
            );

            modelBuilder.Entity<Exam>()
                .HasOne(e => e.Subject)
                .WithMany(s => s.Exams)
                .HasForeignKey(e => e.SubjectID)
                .OnDelete(DeleteBehavior.Restrict);

            // ========================================================
            // ضبط علاقات الجدول الموحد الجديد (ExamLocation)
            // ========================================================

            // 1. علاقة الربط الذاتي الهرمية (Self-Referencing)
            modelBuilder.Entity<ExamLocation>()
                .HasOne(l => l.ParentLocation)
                .WithMany(l => l.SubLocations)
                .HasForeignKey(l => l.ParentLocationId)
                .OnDelete(DeleteBehavior.Restrict); 

         

            // ========================================================
            // تحديث علاقات التكليفات (CommitteesAssignment)
            // ========================================================
            modelBuilder.Entity<CommitteesAssignment>()
                .HasKey(ca => ca.AssignmentId);

            modelBuilder.Entity<CommitteesAssignment>()
                .HasIndex(ca => new { ca.PersonId, ca.ExamScheduleId })
                .IsUnique();

            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.Person)
                .WithMany(p => p.CommitteesAssignments)
                .HasForeignKey(ca => ca.PersonId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.ExamSchedule)
                .WithMany()
                .HasForeignKey(ca => ca.ExamScheduleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.Role)
                .WithMany(r => r.CommitteesAssignments)
                .HasForeignKey(ca => ca.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // تعديل ربط التكليفات بجدول الأماكن الموحد بدلاً من الـ 3 جداول الفرعية
            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.ExamLocation)
                .WithMany(l => l.CommitteesAssignments)
                .HasForeignKey(ca => ca.LocationId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // ========================================================
            // ضبط علاقات الأقارب والطلاب والمستندات الزمنية
            // ========================================================
            modelBuilder.Entity<Relative>()
                .HasOne(r => r.Student)
                .WithMany(s => s.Relatives)
                .HasForeignKey(r => r.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Relative>()
                .HasOne(r => r.Person)
                .WithMany(p => p.Relatives)
                .HasForeignKey(r => r.PersonId)
                .OnDelete(DeleteBehavior.Restrict);

            // تعديل ربط جدول الطلاب بالمكان الموحد (اللجنة)
            modelBuilder.Entity<Student>()
                .HasOne(s => s.ExamLocation)
                .WithMany(l => l.Students)
                .HasForeignKey(s => s.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // تعديل ربط جدول جدول الامتحانات (ExamSchedule) بالمكان الموحد (الصالة/البلوك)
            modelBuilder.Entity<ExamSchedule>()
                .HasOne(es => es.Exam)
                .WithMany(e => e.ExamSchedules)
                .HasForeignKey(es => es.ExamId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExamSchedule>()
                .HasOne(es => es.ExamLocation)
                .WithMany(l => l.ExamSchedules)
                .HasForeignKey(es => es.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExamSchedule>()
                .HasIndex(es => new { es.ExamId, es.LocationId })
                .IsUnique();

            // ========================================================
            // التقارير والمسؤولين عنها
            // ========================================================
            modelBuilder.Entity<Report>()
                .HasOne(r => r.ExamSchedule)
                .WithMany(es => es.Reports)
                .HasForeignKey(r => r.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReportPerson>()
                .HasKey(rp => new { rp.ReportId, rp.PersonId });

            modelBuilder.Entity<ReportPerson>()
                .HasOne(rp => rp.Report)
                .WithMany(r => r.ReportPersons)
                .HasForeignKey(rp => rp.ReportId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReportPerson>()
                .HasOne(rp => rp.Person)
                .WithMany(p => p.ReportPersons)
                .HasForeignKey(rp => rp.PersonId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReportPerson>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.ReportPersons)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Restrict);


            modelBuilder.Entity<ExamLocation>()
                .HasIndex(l => l.AcademicYear)
                .IsUnique()
                .HasFilter("[Type] = 0 AND [AcademicYear] IS NOT NULL");
            // رقم الـ Type بتاع Hall - غيّره حسب ترتيب الـ enum عندك
        }
    }
}