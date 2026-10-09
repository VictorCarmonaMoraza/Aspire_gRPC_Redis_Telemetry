using MasterNet.MigrationSQLService; // Importa el espacio de nombres donde se encuentra el servicio Worker.
using MasterNet.Persistence; // Importa el contexto de acceso a datos de la aplicación.
using Microsoft.Extensions.DependencyInjection; // Importa las extensiones para registrar servicios en el contenedor de dependencias.
using Microsoft.Extensions.Hosting; // Importa las utilidades para crear y ejecutar aplicaciones basadas en host.

var builder = Host.CreateApplicationBuilder(args); // Crea el constructor principal de la aplicación usando los argumentos de inicio.

builder.Services.AddHostedService<Worker>(); // Registra el servicio en segundo plano Worker para que se ejecute con el host.

builder.AddServiceDefaults(); // Aplica la configuración por defecto compartida por los servicios de la solución.

builder.AddSqlServerDbContext<MasterNetDbContext>(connectionName: "MasterNetDB"); // Registra el contexto de base de datos SQL Server usando la conexión llamada MasterNetDB.

var host = builder.Build(); // Construye la instancia final del host con todos los servicios y configuraciones registradas.

host.Run(); // Inicia la ejecución de la aplicación y mantiene el servicio activo hasta su detención.
