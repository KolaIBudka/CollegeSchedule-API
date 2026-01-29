using Microsoft.EntityFrameworkCore;
using CollegeSchedule.Models;

namespace CollegeSchedule.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Добавляем DbSet для каждой таблицы
        public DbSet<Building> Buildings { get; set; }
        public DbSet<Classroom> Classrooms { get; set; }
        public DbSet<Teacher> Teachers { get; set; }
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<Specialty> Specialties { get; set; }
        public DbSet<StudentGroup> StudentGroups { get; set; }
        public DbSet<Weekday> Weekdays { get; set; }
        public DbSet<LessonTime> LessonTimes { get; set; }
        public DbSet<Schedule> Schedules { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Уникальный индекс: группа + часть не может быть на двух парах одновременно
            modelBuilder.Entity<Schedule>()
                .HasIndex(s => new { s.LessonDate, s.LessonTimeId, s.GroupId, s.GroupPart })
                .IsUnique();

            // Уникальный индекс: кабинет не может быть занят дважды
            modelBuilder.Entity<Schedule>()
                .HasIndex(s => new { s.LessonDate, s.LessonTimeId, s.ClassroomId })
                .IsUnique();

            // Конвертация enum в строку
            modelBuilder.Entity<Schedule>()
                .Property(s => s.GroupPart)
                .HasConversion<string>();
        }
    }
}