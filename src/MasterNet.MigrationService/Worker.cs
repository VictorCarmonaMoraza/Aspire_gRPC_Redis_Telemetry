using Microsoft.Extensions.Hosting;

namespace MasterNet.MigrationService
{
    public class Worker: BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            throw new NotImplementedException();
        }
    }
}
