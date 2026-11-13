using CourseManagement.Models;

namespace CourseManagement.Data;

public interface ILookupRepository
{
    Task<IEnumerable<LookupItem>> GetCategoriesAsync();
    Task<IEnumerable<LookupItem>> GetLevelsAsync();
    Task<IEnumerable<LookupItem>> GetRolesAsync();
}

public interface IUserRepository
{
    Task<IEnumerable<UserInfo>> GetActiveAsync();
    Task<int> CreateAsync(string email, string passwordHash, string firstName,
        string lastName, int roleId, int? actorId);
    Task SoftDeleteAsync(int id, int actorId);
}

public interface ICourseRepository
{
    Task<IEnumerable<CourseInfo>> GetActiveAsync();
    Task<IEnumerable<CourseStructureRow>> GetStructureAsync(int courseId);

    Task<int> CreateAsync(string title, string? description, int categoryId,
        int levelId, decimal price, int actorId);
    Task UpdateAsync(int id, string title, string? description, int categoryId,
        int levelId, decimal price, int actorId);
    Task AddInstructorAsync(int courseId, int userId, int actorId);
    Task PublishAsync(int courseId, int actorId);

    Task<int> AddModuleAsync(int courseId, string title, int? position, int actorId);
    Task<int> AddLessonAsync(int moduleId, string title, string? content,
        int? durationMin, int? position, int actorId);

    Task SoftDeleteAsync(int courseId, int actorId);
    Task RestoreAsync(int courseId, int actorId);
}

public interface IEnrollmentRepository
{
    Task<IEnumerable<StudentProgressRow>> GetProgressAsync(int? courseId = null);
    Task<int> EnrollAsync(int userId, int courseId, int actorId);
    Task<int> RegisterPaymentAsync(int enrollmentId, decimal amount, string status, int actorId);
    Task CompleteLessonAsync(int enrollmentId, int lessonId, int actorId);
    Task<int> IssueCertificateAsync(int enrollmentId, int actorId);
}
