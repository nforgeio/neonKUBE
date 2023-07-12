using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Neon.Kube.Operator;
using System.Threading.Tasks;

namespace TestOperator
{
    public static partial class Program
    {
        public static string Namespace = null;
        public static async Task Main(string[] args)
        {
            var operatorHost = KubernetesOperatorHost
                .CreateDefaultBuilder(args)
                .ConfigureOperator(settings =>
                {
                    settings.WatchNamespace = "default,services";
                })
                .ConfigureNeonKube()
                .UseStartup<OperatorStartup>()
                .Build();

            await operatorHost.RunAsync();
        }
    }
}
