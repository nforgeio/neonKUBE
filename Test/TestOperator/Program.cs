using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Neon.Kube.Operator;
using System.Threading.Tasks;

namespace TestOperator
{
    public static partial class Program
    {
        public static async Task Main(string[] args)
        {
            var k8s = KubernetesOperatorHost
                .CreateDefaultBuilder(args)
                .ConfigureHostDefaults(configure =>
                {
                    configure.ConfigureWebHostDefaults(webbuilder =>
                    {
                        webbuilder.UseStartup<OperatorStartup>();
                        webbuilder.UseKestrel(options =>
                        {
                            options.ListenAnyIP(1234);
                        });
                    });
                    configure.ConfigureLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddConsole();
                    });
                }).Build();

            await k8s.RunAsync();

        }
    }
}