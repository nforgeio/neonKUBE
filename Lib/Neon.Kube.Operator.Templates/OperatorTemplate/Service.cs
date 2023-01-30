using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Net.Sockets;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Operator;
using Neon.Kube.Resources;
using Neon.Kube.Resources.CertManager;
using Neon.Net;
using System.Net.Http;
using Neon.Retry;
using Neon.Service;
using Neon.Tasks;

using k8s;
using k8s.Models;

namespace OperatorTemplate
{
    public partial class Service : NeonService
    {
        /// <summary>
        /// Kubernetes client.
        /// </summary>
        public IKubernetes K8s;

        // private fields
        private IWebHost webHost;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        public Service(string name)
            : base(name, version: KubeVersions.NeonKube, new NeonServiceOptions() { MetricsPrefix = "neonnodeagent" })
        {
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected async override Task<int> OnRunAsync()
        {
            K8s = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig(), new KubernetesRetryHandler());

            await CheckCertificateAsync();

            // Start the web service.
            var port = 443;

            if (NeonHelper.IsDevWorkstation)
            {
                port = 11006;
            }

            webHost = new WebHostBuilder()
                .ConfigureAppConfiguration(
                    (hostingcontext, config) =>
                    {
                        config.Sources.Clear();
                    })
                .UseStartup<OperatorStartup>()
                .UseKestrel(options => {
                    options.ConfigureEndpointDefaults(o =>
                    {
                        o.UseHttps(Certificate);
                    });
                    options.Listen(IPAddress.Any, port);

                })
                .ConfigureServices(services => services.AddSingleton(typeof(Service), this))
                .UseStaticWebAssets()
            .Build();

            // Indicate that the service is running.

            await StartedAsync();

            _ = webHost.RunAsync();

            // Handle termination gracefully.

            await Terminator.StopEvent.WaitAsync();
            Terminator.ReadyToExit();

            return 0;
        }
    }
}