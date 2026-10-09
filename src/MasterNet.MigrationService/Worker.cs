using MasterNet.Domain.Courses;
using MasterNet.Domain.Instructors;
using MasterNet.Domain.Prices;
using MasterNet.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

namespace MasterNet.MigrationService;

public class Worker(
        IServiceProvider serviceProvider,
        IHostApplicationLifetime hostApplicationLifetime
    ) : BackgroundService
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>
    /// Ejecuta el flujo principal del servicio: crea la base de datos si no existe,
    /// aplica las migraciones pendientes y carga los datos iniciales.
    /// </summary>
    /// <param name="stoppingToken">Token de cancelación del servicio hospedado.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Inicia una traza para registrar el proceso completo de migración y carga inicial.
        using var activity = ActivitySource
                              .StartActivity("Migrando Database", ActivityKind.Client);
        try
        {
            // Crea un ámbito de dependencias para resolver servicios scoped de forma segura.
            using var scope = serviceProvider.CreateScope();

            // Obtiene el contexto principal de Entity Framework desde el contenedor.
            var context = scope.ServiceProvider.GetRequiredService<MasterNetDbContext>();

            // Comprueba y crea la base de datos si todavía no existe.
            await EnsureDatabaseAsync(context, stoppingToken);

            // Aplica las migraciones pendientes sobre la base de datos.
            await RunMigrationAsync(context, stoppingToken);

            // Inserta los datos semilla iniciales si aún no se han cargado cursos.
            await SeedDataAsync(context, stoppingToken);


        }
        catch (Exception ex)
        {
            // Registra la excepción en la actividad de telemetría actual.
            activity?.AddException(ex);

            // Relanza la excepción para que el host pueda manejar el error correctamente.
            throw;
        }


        // Finaliza la aplicación una vez completado el trabajo del servicio.
        hostApplicationLifetime.StopApplication();

    }

    /// <summary>
    /// Garantiza que la base de datos exista antes de ejecutar las migraciones.
    /// </summary>
    /// <param name="dbContext">Contexto de acceso a datos.</param>
    /// <param name="cancellationToken">Token para cancelar la operación.</param>
    private static async Task EnsureDatabaseAsync(
        MasterNetDbContext dbContext,
        CancellationToken cancellationToken
        )
    {
        // Obtiene el servicio relacional encargado de crear físicamente la base de datos.
        var dbCreator = dbContext.GetService<IRelationalDatabaseCreator>();

        // Obtiene la estrategia de ejecución con reintentos configurada para la conexión.
        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            // Comprueba si la base de datos ya existe antes de intentar crearla.
            if (!await dbCreator.ExistsAsync(cancellationToken))
            {
                // Crea la base de datos cuando todavía no está disponible.
                await dbCreator.CreateAsync(cancellationToken);
            }

        });
    }


    /// <summary>
    /// Ejecuta las migraciones pendientes dentro de una transacción.
    /// </summary>
    /// <param name="context">Contexto de acceso a datos.</param>
    /// <param name="cancellationToken">Token para cancelar la operación.</param>
    private static async Task RunMigrationAsync(
        MasterNetDbContext context,
        CancellationToken cancellationToken
    )
    {
        // Obtiene la estrategia de ejecución resiliente para operaciones de base de datos.
        var strategy = context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            // Abre una transacción para ejecutar las migraciones de forma atómica.
            await using var transaction = await context.Database
                                                .BeginTransactionAsync(cancellationToken);

            // Aplica todas las migraciones pendientes definidas en el proyecto.
            await context.Database.MigrateAsync(cancellationToken);

            // Confirma la transacción una vez completadas las migraciones.
            await transaction.CommitAsync(cancellationToken);
        });

    }


    /// <summary>
    /// Inserta los datos semilla iniciales de instructores, precios, cursos y relaciones
    /// solo cuando la base de datos aún no contiene cursos.
    /// </summary>
    /// <param name="context">Contexto de acceso a datos.</param>
    /// <param name="cancellationToken">Token para cancelar la operación.</param>
    private static async Task SeedDataAsync(
        MasterNetDbContext context,
        CancellationToken cancellationToken
        )
    {
        // Verifica si ya existen cursos para evitar insertar datos duplicados.
        if (await context.Courses!.AnyAsync(cancellationToken))
        {
            // Sale del método si la base de datos ya fue inicializada previamente.
            return;
        }

        // Obtiene la estrategia de ejecución resiliente para el proceso de seeding.
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () => {
            // Abre una transacción para guardar todos los datos semilla como una sola unidad.
            await using var transaction = await context.Database
                                             .BeginTransactionAsync(cancellationToken);


            // Instructors
            var instructors = new List<Instructor>
            {
                // Define el primer instructor con un identificador fijo para relacionarlo después con cursos.
                new() { Id = Guid.Parse("a1111111-1111-1111-1111-111111111111"), FirstName = "John", LastName = "Smith", Degree = "PhD in Computer Science" },

                // Define el segundo instructor con un identificador fijo para mantener datos consistentes.
                new() { Id = Guid.Parse("a2222222-2222-2222-2222-222222222222"), FirstName = "Maria", LastName = "Garcia", Degree = "Master in Software Engineering" },

                // Define el tercer instructor especializado en inteligencia artificial.
                new() { Id = Guid.Parse("a3333333-3333-3333-3333-333333333333"), FirstName = "David", LastName = "Chen", Degree = "PhD in Artificial Intelligence" },

                // Define el cuarto instructor especializado en ciencia de datos.
                new() { Id = Guid.Parse("a4444444-4444-4444-4444-444444444444"), FirstName = "Sarah", LastName = "Johnson", Degree = "Master in Data Science" },

                // Define el quinto instructor especializado en ciberseguridad.
                new() { Id = Guid.Parse("a5555555-5555-5555-5555-555555555555"), FirstName = "Michael", LastName = "Brown", Degree = "PhD in Cybersecurity" },
            };

            // Agrega la colección completa de instructores al contexto de EF Core.
            context.Instructors!.AddRange(instructors);

            // Persiste los instructores para que puedan ser referenciados por otras tablas.
            await context.SaveChangesAsync(cancellationToken);

            // Prices
            var prices = new List<Price>
            {
                // Define el nivel de precio gratuito.
                new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Free", CurrentPrice = 0.00m, PromotionPrice = 0.00m },

                // Define el nivel de precio básico con su valor actual y promocional.
                new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Basic", CurrentPrice = 29.99m, PromotionPrice = 19.99m },

                // Define el nivel de precio estándar.
                new() { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Standard", CurrentPrice = 49.99m, PromotionPrice = 39.99m },

                // Define el nivel de precio premium.
                new() { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Name = "Premium", CurrentPrice = 99.99m, PromotionPrice = 79.99m },

                // Define el nivel de precio enterprise.
                new() { Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), Name = "Enterprise", CurrentPrice = 199.99m, PromotionPrice = 149.99m },
            };

            // Agrega la colección de precios al contexto.
            context.Prices!.AddRange(prices);

            // Guarda los precios para poder vincularlos posteriormente con cursos.
            await context.SaveChangesAsync(cancellationToken);

            // Courses
            var courses = new List<Course>
            {
                // Define el curso base de ASP.NET Core.
                new() { Id = Guid.Parse("c1111111-1111-1111-1111-111111111111"), Title = "ASP.NET Core Fundamentals", Description = "Learn the basics of ASP.NET Core web development", PublishedAt = new DateTime(2024, 1, 15) },

                // Define el curso avanzado de programación en C#.
                new() { Id = Guid.Parse("c2222222-2222-2222-2222-222222222222"), Title = "Advanced C# Programming", Description = "Master advanced C# concepts and patterns", PublishedAt = new DateTime(2024, 2, 1) },

                // Define el curso centrado en Blazor WebAssembly.
                new() { Id = Guid.Parse("c3333333-3333-3333-3333-333333333333"), Title = "Blazor WebAssembly Complete Guide", Description = "Build modern web applications with Blazor", PublishedAt = new DateTime(2024, 3, 10) },

                // Define el curso de Entity Framework Core.
                new() { Id = Guid.Parse("c4444444-4444-4444-4444-444444444444"), Title = "Entity Framework Core Mastery", Description = "Deep dive into EF Core and database design", PublishedAt = new DateTime(2024, 4, 5) },

                // Define el curso de microservicios con .NET.
                new() { Id = Guid.Parse("c5555555-5555-5555-5555-555555555555"), Title = "Microservices with .NET", Description = "Build scalable microservices architecture", PublishedAt = new DateTime(2024, 5, 20) },

                // Define el curso de Azure DevOps y automatización de despliegues.
                new() { Id = Guid.Parse("c6666666-6666-6666-6666-666666666666"), Title = "Azure DevOps and CI/CD", Description = "Implement continuous integration and deployment", PublishedAt = new DateTime(2024, 6, 15) },

                // Define el curso de Clean Architecture en .NET.
                new() { Id = Guid.Parse("c7777777-7777-7777-7777-777777777777"), Title = "Clean Architecture in .NET", Description = "Learn clean code principles and architecture", PublishedAt = new DateTime(2024, 7, 1) },

                // Define el curso de contenedores y orquestación con Docker y Kubernetes.
                new() { Id = Guid.Parse("c8888888-8888-8888-8888-888888888888"), Title = "Docker and Kubernetes for .NET", Description = "Containerize and orchestrate .NET applications", PublishedAt = new DateTime(2024, 8, 10) },
            };

            // Agrega la colección de cursos al contexto.
            context.Courses!.AddRange(courses);

            // Guarda los cursos antes de crear las relaciones con otras entidades.
            await context.SaveChangesAsync(cancellationToken);


            // Courses-Instructors
            var courseInstructors = new List<CourseInstructor>
            {
                // Asocia el curso ASP.NET Core con el primer instructor.
                new() { CourseId = Guid.Parse("c1111111-1111-1111-1111-111111111111"), InstructorId = Guid.Parse("a1111111-1111-1111-1111-111111111111") },

                // Asocia el mismo curso ASP.NET Core con el segundo instructor.
                new() { CourseId = Guid.Parse("c1111111-1111-1111-1111-111111111111"), InstructorId = Guid.Parse("a2222222-2222-2222-2222-222222222222") },

                // Asocia el curso avanzado de C# con el primer instructor.
                new() { CourseId = Guid.Parse("c2222222-2222-2222-2222-222222222222"), InstructorId = Guid.Parse("a1111111-1111-1111-1111-111111111111") },

                // Asocia el curso de Blazor con el segundo instructor.
                new() { CourseId = Guid.Parse("c3333333-3333-3333-3333-333333333333"), InstructorId = Guid.Parse("a2222222-2222-2222-2222-222222222222") },

                // Asocia el curso de EF Core con el tercer instructor.
                new() { CourseId = Guid.Parse("c4444444-4444-4444-4444-444444444444"), InstructorId = Guid.Parse("a3333333-3333-3333-3333-333333333333") },

                // Asocia el curso de microservicios con el cuarto instructor.
                new() { CourseId = Guid.Parse("c5555555-5555-5555-5555-555555555555"), InstructorId = Guid.Parse("a4444444-4444-4444-4444-444444444444") },

                // Asocia el curso de Azure DevOps con el quinto instructor.
                new() { CourseId = Guid.Parse("c6666666-6666-6666-6666-666666666666"), InstructorId = Guid.Parse("a5555555-5555-5555-5555-555555555555") },

                // Asocia el curso de Clean Architecture con el primer instructor.
                new() { CourseId = Guid.Parse("c7777777-7777-7777-7777-777777777777"), InstructorId = Guid.Parse("a1111111-1111-1111-1111-111111111111") },

                // Asocia el curso de Docker y Kubernetes con el quinto instructor.
                new() { CourseId = Guid.Parse("c8888888-8888-8888-8888-888888888888"), InstructorId = Guid.Parse("a5555555-5555-5555-5555-555555555555") },
            };

            // Agrega las relaciones muchos a muchos entre cursos e instructores.
            context.Set<CourseInstructor>().AddRange(courseInstructors);

            // Guarda las asociaciones entre cursos e instructores.
            await context.SaveChangesAsync(cancellationToken);

            // Courses-Prices
            var coursePrices = new List<CoursePrice>
            {
                // Asigna el precio Basic al curso de ASP.NET Core.
                new() { CourseId = Guid.Parse("c1111111-1111-1111-1111-111111111111"), PriceId = Guid.Parse("22222222-2222-2222-2222-222222222222") },

                // Asigna el precio Standard al curso avanzado de C#.
                new() { CourseId = Guid.Parse("c2222222-2222-2222-2222-222222222222"), PriceId = Guid.Parse("33333333-3333-3333-3333-333333333333") },

                // Asigna el precio Standard al curso de Blazor.
                new() { CourseId = Guid.Parse("c3333333-3333-3333-3333-333333333333"), PriceId = Guid.Parse("33333333-3333-3333-3333-333333333333") },

                // Asigna el precio Premium al curso de EF Core.
                new() { CourseId = Guid.Parse("c4444444-4444-4444-4444-444444444444"), PriceId = Guid.Parse("44444444-4444-4444-4444-444444444444") },

                // Asigna el precio Premium al curso de microservicios.
                new() { CourseId = Guid.Parse("c5555555-5555-5555-5555-555555555555"), PriceId = Guid.Parse("44444444-4444-4444-4444-444444444444") },

                // Asigna el precio Enterprise al curso de Azure DevOps.
                new() { CourseId = Guid.Parse("c6666666-6666-6666-6666-666666666666"), PriceId = Guid.Parse("55555555-5555-5555-5555-555555555555") },

                // Asigna el precio Standard al curso de Clean Architecture.
                new() { CourseId = Guid.Parse("c7777777-7777-7777-7777-777777777777"), PriceId = Guid.Parse("33333333-3333-3333-3333-333333333333") },

                // Asigna el precio Enterprise al curso de Docker y Kubernetes.
                new() { CourseId = Guid.Parse("c8888888-8888-8888-8888-888888888888"), PriceId = Guid.Parse("55555555-5555-5555-5555-555555555555") },
            };

            // Agrega las relaciones entre cursos y precios al contexto.
            context.Set<CoursePrice>().AddRange(coursePrices);

            // Guarda las asociaciones entre cursos y precios.
            await context.SaveChangesAsync(cancellationToken);

            // Confirma toda la transacción una vez insertados todos los datos y relaciones.
            await transaction.CommitAsync(cancellationToken);

        });
    }

}
