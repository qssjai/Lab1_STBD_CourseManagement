using System.Text;
using CourseManagement.Data;
using CourseManagement.Models;
using Dapper;
using Npgsql;

namespace CourseManagement;

internal static class Program
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=course_management;Username=postgres;Password=041006";

    private static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        DefaultTypeMap.MatchNamesWithUnderscores = true;

        try
        {
            await RunAsync();
        }
        catch (PostgresException ex)
        {
            Console.WriteLine($"Помилка БД: {ex.MessageText}");
        }
        catch (NpgsqlException ex)
        {
            Console.WriteLine($"Не вдалося підключитися до БД: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("Готово. Натисніть будь-яку клавішу...");
        Console.ReadKey();
    }

    private static async Task RunAsync()
    {
        int adminId, instructorId, studentId, categoryId, levelId;

        await using (var uow = new UnitOfWork(ConnectionString))
        {
            var users = (await uow.Users.GetActiveAsync()).ToList();
            adminId = users.First(u => u.Email == "admin@courses.ua").Id;
            instructorId = users.First(u => u.Email == "shevchuk@courses.ua").Id;
            studentId = users.First(u => u.Email == "dora@student.ua").Id;

            categoryId = (await uow.Lookups.GetCategoriesAsync()).First(c => c.Name == "Програмування").Id;
            levelId = (await uow.Lookups.GetLevelsAsync()).First(l => l.Name == "Початковий").Id;

            Header("Початковий каталог курсів (v_active_courses)");
            PrintCourses(await uow.Courses.GetActiveAsync());
        }

        int courseId = await CreateCourseWithContentAsync(adminId, instructorId, categoryId, levelId);
        await EnrollStudentAsync(adminId, studentId, courseId);
        await RollbackDemoAsync(instructorId, categoryId, levelId);
        await SoftDeleteDemoAsync(adminId, courseId);
    }

    private static async Task<int> CreateCourseWithContentAsync(
        int adminId, int instructorId, int categoryId, int levelId)
    {
        Header("Сценарій 1. Створення курсу з модулями та уроками в одній транзакції");

        int courseId;
        await using (var uow = new UnitOfWork(ConnectionString))
        {
            await uow.BeginAsync(instructorId);
            try
            {
                courseId = await uow.Courses.CreateAsync(
                    "Основи Git", "Робота з системою контролю версій", categoryId, levelId, 600m, instructorId);

                await uow.Courses.AddInstructorAsync(courseId, instructorId, adminId);

                int module1 = await uow.Courses.AddModuleAsync(courseId, "Початок роботи", null, instructorId);
                await uow.Courses.AddLessonAsync(module1, "Що таке Git", "Системи контролю версій", 20, null, instructorId);
                await uow.Courses.AddLessonAsync(module1, "Перший коміт", "init, add, commit", 25, null, instructorId);

                int module2 = await uow.Courses.AddModuleAsync(courseId, "Гілки", null, instructorId);
                await uow.Courses.AddLessonAsync(module2, "Branch і merge", "Робота з гілками", 30, null, instructorId);

                await uow.Courses.PublishAsync(courseId, instructorId);

                await uow.CommitAsync();
                Console.WriteLine($"Транзакцію зафіксовано. Створено курс з id = {courseId}.");
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
        }

        await using (var uow = new UnitOfWork(ConnectionString))
        {
            Console.WriteLine();
            Console.WriteLine("Структура курсу (v_course_structure):");
            foreach (var row in await uow.Courses.GetStructureAsync(courseId))
            {
                Console.WriteLine(
                    $"  Модуль {row.ModulePosition}. {row.ModuleTitle,-16} | " +
                    $"урок {row.LessonPosition}. {row.LessonTitle,-16} | {row.DurationMin} хв");
            }

            Console.WriteLine();
            Console.WriteLine("Каталог після створення (v_active_courses):");
            PrintCourses(await uow.Courses.GetActiveAsync());
        }

        return courseId;
    }

    private static async Task EnrollStudentAsync(int adminId, int studentId, int courseId)
    {
        Header("Сценарій 2. Запис студента, оплата, проходження курсу та сертифікат");

        await using (var uow = new UnitOfWork(ConnectionString))
        {
            await uow.BeginAsync(adminId);
            try
            {
                int enrollmentId = await uow.Enrollments.EnrollAsync(studentId, courseId, adminId);
                int paymentId = await uow.Enrollments.RegisterPaymentAsync(enrollmentId, 600m, "paid", adminId);
                Console.WriteLine($"Запис на курс: {enrollmentId}, платіж: {paymentId}.");

                var lessons = (await uow.Courses.GetStructureAsync(courseId))
                    .Where(r => r.LessonId.HasValue)
                    .ToList();

                foreach (var row in lessons)
                {
                    await uow.Enrollments.CompleteLessonAsync(enrollmentId, row.LessonId!.Value, studentId);
                    Console.WriteLine($"  Пройдено урок: {row.LessonTitle}");
                }

                int certificateId = await uow.Enrollments.IssueCertificateAsync(enrollmentId, adminId);
                Console.WriteLine($"Видано сертифікат з id = {certificateId}.");

                await uow.CommitAsync();
                Console.WriteLine("Транзакцію зафіксовано.");
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
        }

        await using (var uow = new UnitOfWork(ConnectionString))
        {
            Console.WriteLine();
            Console.WriteLine("Прогрес студента (v_student_progress):");
            foreach (var p in await uow.Enrollments.GetProgressAsync(courseId))
            {
                Console.WriteLine(
                    $"  {p.StudentName} | {p.CourseTitle} | статус: {p.Status} | " +
                    $"прогрес: {p.Progress}% | уроків: {p.CompletedLessons}/{p.TotalLessons}");
            }
        }
    }

    private static async Task RollbackDemoAsync(int instructorId, int categoryId, int levelId)
    {
        Header("Сценарій 3. Відкат транзакції при помилці");

        int before;
        await using (var uow = new UnitOfWork(ConnectionString))
        {
            before = (await uow.Courses.GetActiveAsync()).Count();
        }
        Console.WriteLine($"Курсів у каталозі до операції: {before}");

        await using (var uow = new UnitOfWork(ConnectionString))
        {
            await uow.BeginAsync(instructorId);
            try
            {
                int id = await uow.Courses.CreateAsync(
                    "Курс без уроків", null, categoryId, levelId, 0m, instructorId);
                Console.WriteLine($"Створено курс id = {id} (поки що в межах транзакції).");

                await uow.Courses.AddModuleAsync(id, "Порожній модуль", null, instructorId);

                // немає жодного уроку, тому процедура викличе помилку
                await uow.Courses.PublishAsync(id, instructorId);

                await uow.CommitAsync();
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"Помилка БД: {ex.MessageText}");
                await uow.RollbackAsync();
                Console.WriteLine("Транзакцію скасовано.");
            }
        }

        int after;
        await using (var uow = new UnitOfWork(ConnectionString))
        {
            after = (await uow.Courses.GetActiveAsync()).Count();
        }
        Console.WriteLine($"Курсів у каталозі після операції: {after} (має збігатися з {before}).");
    }

    private static async Task SoftDeleteDemoAsync(int adminId, int courseId)
    {
        Header("Сценарій 4. М'яке видалення курсу (soft delete)");

        await using (var uow = new UnitOfWork(ConnectionString))
        {
            Console.WriteLine($"Курсів у каталозі до видалення: {(await uow.Courses.GetActiveAsync()).Count()}");

            await uow.BeginAsync(adminId);
            try
            {
                await uow.Courses.SoftDeleteAsync(courseId, adminId);
                await uow.CommitAsync();
            }
            catch
            {
                await uow.RollbackAsync();
                throw;
            }
        }

        await using (var uow = new UnitOfWork(ConnectionString))
        {
            Console.WriteLine($"Курсів у каталозі після видалення: {(await uow.Courses.GetActiveAsync()).Count()}");
            Console.WriteLine($"Рядків структури видаленого курсу у view: {(await uow.Courses.GetStructureAsync(courseId)).Count()}");
        }
    }

    private static void Header(string text)
    {
        Console.WriteLine();
        Console.WriteLine($"--- {text} ---");
    }

    private static void PrintCourses(IEnumerable<CourseInfo> courses)
    {
        foreach (var c in courses)
        {
            Console.WriteLine(
                $"  {c.Id,3} | {c.Title,-30} | {c.Status,-9} | {c.Price,7} | " +
                $"студентів: {c.StudentsCount}, модулів: {c.ModulesCount}, уроків: {c.LessonsCount}");
        }
    }
}
