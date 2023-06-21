using System;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OperatorTemplate
{
    /// <summary>
    /// Hosts the operator entry point.
    /// </summary>
    public static partial class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
               .ConfigureWebHostDefaults(webBuilder =>
               {
                   webBuilder.UseStartup<Startup>();
               });

            host.ConfigureLogging(
                logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                });

            await host.Build().RunAsync();
        }
    }
}
