using Microsoft.Extensions.Hosting; // Importa la clase base para servicios en segundo plano.

namespace MasterNet.MigrationSQLService // Define el espacio de nombres del servicio de migración SQL.
{
    public class Worker : BackgroundService // Declara un servicio en segundo plano que se ejecutará con el host.
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) // Define la lógica principal que se ejecuta cuando arranca el servicio.
        {
            throw new NotImplementedException(); // Indica que la implementación del trabajo aún no ha sido desarrollada.
        }
    }
}
