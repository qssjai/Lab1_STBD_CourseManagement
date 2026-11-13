using Dapper;
using Npgsql;

namespace CourseManagement.Data;

public interface IUnitOfWork : IAsyncDisposable
{
    ILookupRepository Lookups { get; }
    IUserRepository Users { get; }
    ICourseRepository Courses { get; }
    IEnrollmentRepository Enrollments { get; }

    Task BeginAsync(int? actorId = null);
    Task CommitAsync();
    Task RollbackAsync();
}

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly DbSession _session;

    public ILookupRepository Lookups { get; }
    public IUserRepository Users { get; }
    public ICourseRepository Courses { get; }
    public IEnrollmentRepository Enrollments { get; }

    public UnitOfWork(string connectionString)
    {
        _session = new DbSession(new NpgsqlConnection(connectionString));

        Lookups = new LookupRepository(_session);
        Users = new UserRepository(_session);
        Courses = new CourseRepository(_session);
        Enrollments = new EnrollmentRepository(_session);
    }

    public async Task BeginAsync(int? actorId = null)
    {
        if (_session.Transaction != null)
            throw new InvalidOperationException("Транзакція вже розпочата.");

        if (_session.Connection.State != System.Data.ConnectionState.Open)
            await _session.Connection.OpenAsync();

        _session.Transaction = await _session.Connection.BeginTransactionAsync();

        if (actorId.HasValue)
        {
            await _session.Connection.ExecuteAsync(
                "SELECT set_config('app.current_user_id', @id, true)",
                new { id = actorId.Value.ToString() },
                _session.Transaction);
        }
    }

    public async Task CommitAsync()
    {
        if (_session.Transaction == null)
            throw new InvalidOperationException("Немає активної транзакції.");

        await _session.Transaction.CommitAsync();
        await _session.Transaction.DisposeAsync();
        _session.Transaction = null;
    }

    public async Task RollbackAsync()
    {
        if (_session.Transaction == null)
            return;

        await _session.Transaction.RollbackAsync();
        await _session.Transaction.DisposeAsync();
        _session.Transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        // якщо транзакцію не завершили, скасовуємо її
        await RollbackAsync();
        await _session.Connection.DisposeAsync();
    }
}
