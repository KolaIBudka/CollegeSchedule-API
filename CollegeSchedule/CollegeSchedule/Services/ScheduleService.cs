using CollegeSchedule.Data;
using CollegeSchedule.DTO;
using CollegeSchedule.Models;
using Microsoft.EntityFrameworkCore;

namespace CollegeSchedule.Services
{
    public class ScheduleService : IScheduleService
    {
        private readonly AppDbContext _db;

        public ScheduleService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<ScheduleByDateDto>> GetScheduleForGroup(string groupName, DateTime startDate, DateTime endDate)
        {
            ValidateDates(startDate, endDate);

            var group = await GetGroupByName(groupName);

            var schedules = await LoadSchedules(group.GroupId, startDate, endDate);

            return BuildScheduleDto(startDate, endDate, schedules);
        }

        public async Task<List<StudentGroupDto>> GetAllGroups()
        {
            var groups = await _db.StudentGroups
                .Include(g => g.Specialty)
                .OrderBy(g => g.GroupName)
                .ToListAsync();

            return groups.Select(g => new StudentGroupDto
            {
                GroupId = g.GroupId,
                GroupName = g.GroupName,
                Course = g.Course,
                Specialty = g.Specialty.Name
            }).ToList();
        }

        private static void ValidateDates(DateTime start, DateTime end)
        {
            if (start > end)
                throw new ArgumentException("Дата начала больше даты окончания.");
        }

        private async Task<StudentGroup> GetGroupByName(string groupName)
        {
            var group = await _db.StudentGroups
                .FirstOrDefaultAsync(g => g.GroupName == groupName);

            if (group == null)
                throw new KeyNotFoundException($"Группа {groupName} не найдена.");

            return group;
        }

        private async Task<List<Schedule>> LoadSchedules(int groupId, DateTime start, DateTime end)
        {
            return await _db.Schedules
                .Where(s => s.GroupId == groupId &&
                           s.LessonDate >= start &&
                           s.LessonDate <= end)
                .Include(s => s.Weekday)
                .Include(s => s.LessonTime)
                .Include(s => s.Subject)
                .Include(s => s.Teacher)
                .Include(s => s.Classroom)
                .ThenInclude(c => c.Building)
                .OrderBy(s => s.LessonDate)
                .ThenBy(s => s.LessonTime.LessonNumber)
                .ThenBy(s => s.GroupPart)
                .ToListAsync();
        }

        private static List<ScheduleByDateDto> BuildScheduleDto(DateTime startDate, DateTime endDate, List<Schedule> schedules)
        {
            var scheduleByDate = schedules
                .GroupBy(s => s.LessonDate)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<ScheduleByDateDto>();

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                // Пропускаем воскресенье
                if (date.DayOfWeek == DayOfWeek.Sunday)
                    continue;

                if (!scheduleByDate.TryGetValue(date, out var daySchedules))
                {
                    // День без занятий
                    result.Add(new ScheduleByDateDto
                    {
                        LessonDate = date,
                        Weekday = GetRussianWeekday(date.DayOfWeek),
                        Lessons = new List<LessonDto>()
                    });
                }
                else
                {
                    // День с занятиями
                    result.Add(BuildDayDto(daySchedules));
                }
            }

            return result;
        }

        private static ScheduleByDateDto BuildDayDto(List<Schedule> daySchedules)
        {
            var lessons = daySchedules
                .GroupBy(s => new { s.LessonTime.LessonNumber, s.LessonTime.TimeStart, s.LessonTime.TimeEnd })
                .Select(BuildLessonDto)
                .ToList();

            return new ScheduleByDateDto
            {
                LessonDate = daySchedules.First().LessonDate,
                Weekday = daySchedules.First().Weekday.Name,
                Lessons = lessons
            };
        }

        private static LessonDto BuildLessonDto(IGrouping<dynamic, Schedule> lessonGroup)
        {
            var lessonDto = new LessonDto
            {
                LessonNumber = lessonGroup.Key.LessonNumber,
                Time = $"{lessonGroup.Key.TimeStart:hh\\:mm}-{lessonGroup.Key.TimeEnd:hh\\:mm}",
                GroupParts = new Dictionary<LessonGroupPart, LessonPartDto?>()
            };

            foreach (var part in lessonGroup)
            {
                lessonDto.GroupParts[part.GroupPart] = new LessonPartDto
                {
                    Subject = part.Subject.Name,
                    Teacher = $"{part.Teacher.LastName} {part.Teacher.FirstName} {part.Teacher.MiddleName ?? ""}".Trim(),
                    TeacherPosition = part.Teacher.Position,
                    Classroom = part.Classroom.RoomNumber,
                    Building = part.Classroom.Building.Name,
                    Address = part.Classroom.Building.Address
                };
            }

            // Если для пары нет FULL занятия, добавляем null для полноты
            if (!lessonDto.GroupParts.ContainsKey(LessonGroupPart.FULL))
                lessonDto.GroupParts.TryAdd(LessonGroupPart.FULL, null);

            return lessonDto;
        }

        private static string GetRussianWeekday(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Monday => "Понедельник",
                DayOfWeek.Tuesday => "Вторник",
                DayOfWeek.Wednesday => "Среда",
                DayOfWeek.Thursday => "Четверг",
                DayOfWeek.Friday => "Пятница",
                DayOfWeek.Saturday => "Суббота",
                _ => "Воскресенье"
            };
        }
    }
}