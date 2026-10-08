using MasterNet.MigrationService;
using MasterNet.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Muestra en consola un mensaje indicando que comienza el proceso de carga o migración de la base de datos.
Console.WriteLine("Empezamos cargando la base de datos");

// Crea el builder del host para configurar servicios y dependencias de la aplicación.
var builder = Host.CreateApplicationBuilder(args);

// Registra el servicio en segundo plano que ejecutará la lógica principal del proceso.
builder.Services.AddHostedService<Worker>();

// Agrega la configuración común de servicios por defecto del proyecto distribuido.
builder.AddServiceDefaults();

// Registra el DbContext de SQL Server usando la conexión llamada "MasterNetDB".
builder.AddSqlServerDbContext<MasterNetDbContext>(connectionName: "MasterNetDB");

// Construye el host con toda la configuración definida.
var host = builder.Build();

// Inicia la ejecución de la aplicación y mantiene el servicio en funcionamiento.
host.Run();
