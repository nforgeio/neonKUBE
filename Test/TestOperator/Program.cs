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
            var k8s = KubernetesOperatorHost
                .CreateDefaultBuilder(args)
                .ConfigureOperator(configure =>
                {
                    configure.Port = 1234;
                    configure.AssemblyScanningEnabled = true;
                    configure.Name = "my-cool-operator";
                    configure.Namespace = "default";
                })
                .ConfigureNeonKube()
                .UseStartup<OperatorStartup>()
                .Build();

            await k8s.RunAsync();

        }
    }
}