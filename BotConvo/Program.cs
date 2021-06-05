using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace BotConvo
{
    class Program
    {
        static async Task Main(string[] _)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            var orchestrator = new Orchestrator(configuration);
            await orchestrator.Initialize();

            while (true) ;
        }
    }
}
