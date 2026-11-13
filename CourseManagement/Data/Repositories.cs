using CourseManagement.Models;
using Dapper;
using Npgsql;

namespace CourseManagement.Data;

public sealed class DbSession
{
    public NpgsqlConnection Connection { get; }
    public NpgsqlTransaction? Transaction { get; set; }

    public DbSession(NpgsqlConnection connection) => Connection = connection;
}

public abstract class RepositoryBase
{
    private readonly DbSession _session;

    protected RepositoryBase(DbSession session) => _session = session;

    // SELECT * FROM v_...
    protected Task<IEnumerable<T>> QueryViewAsync<T>(string sql, object? param = null) =>
        _session.Connection.QueryAsync<T>(sql, param, _session.Transaction);

    // CALL sp_...(..., INOUT p_id): повертає id створеного запису
    protected Task<int> CallReturningIdAsync(string sql, object param) =>
        _session.Connection.ExecuteScalarAsync<int>(sql, param, _session.Transaction);

    // CALL sp_...(...): процедура без результату
    protected Task CallAsync(string sql, object param) =>
        _session.Connection.ExecuteAsync(sql, param, _session.Transaction);
}

public sealed class LookupRepository : RepositoryBase, ILookupRepository
{
    public LookupRepository(DbSession session) : base(session) { }

    public Task<IEnumerable<LookupItem>> GetCategoriesAsync() =>
        QueryViewAsync<LookupItem>("SELECT id, name FROM v_categories ORDER BY name");

    public Task<IEnumerable<LookupItem>> GetLevelsAsync() =>
        QueryViewAsync<LookupItem>("SELECT id, name FROM v_course_levels ORDER BY id");

    public Task<IEnumerable<LookupItem>> GetRolesAsync() =>
        QueryViewAsync<LookupItem>("SELECT id, name FROM v_roles ORDER BY id");
}

public sealed class UserRepository : RepositoryBase, IUserRepository
{
    public UserRepository(DbSession session) : base(session) { }

    public Task<IEnumerable<UserInfo>> GetActiveAsync() =>
        QueryViewAsync<UserInfo>("SELECT * FROM v_active_users ORDER BY id");

    public Task<int> CreateAsync(string email, string passwordHash, string firstName,
        string lastName, int roleId, int? actorId) =>
        CallReturningIdAsync(
            "CALL sp_create_user(@email, @passwordHash, @firstName, @lastName, @roleId, @actorId, @id)",
            new { email, passwordHash, firstName, lastName, roleId, actorId, id = (int?)null });

    public Task SoftDeleteAsync(int id, int actorId) =>
        CallAsync("CALL sp_soft_delete_user(@id, @actorId)", new { id, actorId });
}

public sealed class CourseRepository : RepositoryBase, ICourseRepository
{
    public CourseRepository(DbSession session) : base(session) { }

    public Task<IEnumerable<CourseInfo>> GetActiveAsync() =>
        QueryViewAsync<CourseInfo>("SELECT * FROM v_active_courses ORDER BY id");

    public Task<IEnumerable<CourseStructureRow>> GetStructureAsync(int courseId) =>
        QueryViewAsync<CourseStructureRow>(
            @"SELECT * FROM v_course_structure
              WHERE course_id = @courseId
              ORDER BY module_position, lesson_position",
            new { courseId });

    public Task<int> CreateAsync(string title, string? description, int categoryId,
        int levelId, decimal price, int actorId) =>
        CallReturningIdAsync(
            "CALL sp_create_course(@title, @description, @categoryId, @levelId, @price, @actorId, @id)",
            new { title, description, categoryId, levelId, price, actorId, id = (int?)null });

    public Task UpdateAsync(int id, string title, string? description, int categoryId,
        int levelId, decimal price, int actorId) =>
        CallAsync(
            "CALL sp_update_course(@id, @title, @description, @categoryId, @levelId, @price, @actorId)",
            new { id, title, description, categoryId, levelId, price, actorId });

    public Task AddInstructorAsync(int courseId, int userId, int actorId) =>
        CallAsync("CALL sp_add_course_instructor(@courseId, @userId, @actorId)",
            new { courseId, userId, actorId });

    public Task PublishAsync(int courseId, int actorId) =>
        CallAsync("CALL sp_publish_course(@courseId, @actorId)", new { courseId, actorId });

    public Task<int> AddModuleAsync(int courseId, string title, int? position, int actorId) =>
        CallReturningIdAsync(
            "CALL sp_add_module(@courseId, @title, @position, @actorId, @id)",
            new { courseId, title, position, actorId, id = (int?)null });

    public Task<int> AddLessonAsync(int moduleId, string title, string? content,
        int? durationMin, int? position, int actorId) =>
        CallReturningIdAsync(
            "CALL sp_add_lesson(@moduleId, @title, @content, @durationMin, @position, @actorId, @id)",
            new { moduleId, title, content, durationMin, position, actorId, id = (int?)null });

    public Task SoftDeleteAsync(int courseId, int actorId) =>
        CallAsync("CALL sp_soft_delete_course(@courseId, @actorId)", new { courseId, actorId });

    public Task RestoreAsync(int courseId, int actorId) =>
        CallAsync("CALL sp_restore_course(@courseId, @actorId)", new { courseId, actorId });
}

public sealed class EnrollmentRepository : RepositoryBase, IEnrollmentRepository
{
    public EnrollmentRepository(DbSession session) : base(session) { }

    public Task<IEnumerable<StudentProgressRow>> GetProgressAsync(int? courseId = null) =>
        QueryViewAsync<StudentProgressRow>(
            @"SELECT * FROM v_student_progress
              WHERE (@courseId IS NULL OR course_id = @courseId)
              ORDER BY course_title, student_name",
            new { courseId });

    public Task<int> EnrollAsync(int userId, int courseId, int actorId) =>
        CallReturningIdAsync(
            "CALL sp_enroll_student(@userId, @courseId, @actorId, @id)",
            new { userId, courseId, actorId, id = (int?)null });

    public Task<int> RegisterPaymentAsync(int enrollmentId, decimal amount, string status, int actorId) =>
        CallReturningIdAsync(
            "CALL sp_register_payment(@enrollmentId, @amount, @status, @actorId, @id)",
            new { enrollmentId, amount, status, actorId, id = (int?)null });

    public Task CompleteLessonAsync(int enrollmentId, int lessonId, int actorId) =>
        CallAsync("CALL sp_complete_lesson(@enrollmentId, @lessonId, @actorId)",
            new { enrollmentId, lessonId, actorId });

    public Task<int> IssueCertificateAsync(int enrollmentId, int actorId) =>
        CallReturningIdAsync(
            "CALL sp_issue_certificate(@enrollmentId, @actorId, @id)",
            new { enrollmentId, actorId, id = (int?)null });
}
