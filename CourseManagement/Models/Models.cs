namespace CourseManagement.Models;

// v_categories, v_course_levels, v_roles
public class LookupItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// v_active_users
public class UserInfo
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
}

// v_active_courses
public class CourseInfo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string LevelName { get; set; } = string.Empty;
    public long StudentsCount { get; set; }
    public long ModulesCount { get; set; }
    public long LessonsCount { get; set; }
    public decimal? AvgRating { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
}

// v_course_structure (уроки можуть бути відсутні, тому поля уроку nullable)
public class CourseStructureRow
{
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public int ModuleId { get; set; }
    public string ModuleTitle { get; set; } = string.Empty;
    public int ModulePosition { get; set; }
    public int? LessonId { get; set; }
    public string? LessonTitle { get; set; }
    public int? LessonPosition { get; set; }
    public int? DurationMin { get; set; }
}

// v_student_progress
public class StudentProgressRow
{
    public int EnrollmentId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Progress { get; set; }
    public long CompletedLessons { get; set; }
    public long TotalLessons { get; set; }
}
