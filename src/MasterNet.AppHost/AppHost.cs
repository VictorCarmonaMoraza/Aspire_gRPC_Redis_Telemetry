public partial class Program
{
    private static void Main(string[] args)
    {
        // Crea el builder principal de la aplicación distribuida usando los argumentos de inicio.
        var builder = DistributedApplication.CreateBuilder(args);

        // Define un parámetro secreto para la contraseña del contenedor de SQL Server.
        var password = builder.AddParameter("password", secret: true);

        // Registra una instancia de SQL Server llamada "server" escuchando en el puerto 1433.
        // Se marca como persistente para que el contenedor no se destruya al detener la app.
        var server = builder.AddSqlServer("server", password, 1433)
            .WithLifetime(ContainerLifetime.Persistent);

        // Crea la base de datos "MasterNetDB" dentro del servidor SQL registrado.
        var db = server.AddDatabase("MasterNetDB");

        // Registra el proyecto de la API con el nombre "api".
        var api = builder.AddProject<Projects.MasterNet_WebApi>("api")
            // Le pasa a la API la referencia de la base de datos para que pueda usarla.
            .WithReference(db)
            // Hace que la API espere a que la base de datos esté lista antes de arrancar.
            .WaitFor(db);

        // Registra el proyecto cliente.
        // Le inyecta la referencia a la API, espera a que esté disponible y expone endpoints HTTP externos.
        builder.AddProject<Projects.MasterNet_Client>("client")
            .WithReference(api)
            .WaitFor(api)
            .WithExternalHttpEndpoints();

        // Registra el proyecto de migración de base de datos.
        builder.AddProject<Projects.MasterNet_MigrationService>("migration")
            .WithReference(db)
            .WaitFor(db)
            .WithParentRelationship(server);

        // Construye toda la aplicación distribuida y la ejecuta.
        builder.Build().Run();
    }
}