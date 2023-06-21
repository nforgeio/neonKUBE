//-----------------------------------------------------------------------------
// FILE:        KubernetesOperatorHostBuilder.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Neon.Common;
using Neon.Kube.Operator.Builder;

using k8s.Models;
using k8s;

using Prometheus;
using System.Diagnostics;

namespace Neon.Kube.Operator
{
    /// <inheritdoc/>
    public class KubernetesOperatorHostBuilder : IKubernetesOperatorHostBuilder
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The SDK Version info Gauge.
        /// </summary>
        public static Gauge BuildInfo = Metrics.CreateGauge("operator_version_info", "Operator SDK Version", new string[] { "operator", "version" });

        //---------------------------------------------------------------------
        // Instance members

        private KubernetesOperatorHost operatorHost;

        /// <summary>
        /// Constructor.
        /// </summary>
        public KubernetesOperatorHostBuilder(string[] args = null)
        {
            this.operatorHost = new KubernetesOperatorHost(args);
            this.Services     = new ServiceCollection();
        }

        /// <inheritdoc/>
        public IServiceCollection Services { get; set; }

        /// <inheritdoc/>
        public IKubernetesOperatorHost Build()
        {
            var version = GetType().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? 
                TraceContext.Version.ToString();

            BuildInfo.WithLabels(new string[] { operatorHost.OperatorSettings.Name, version }).IncTo(1);

            this.operatorHost.HostBuilder = new WebHostBuilder();

            this.operatorHost.HostBuilder
                .ConfigureServices(services =>
                {
                    services.AddSingleton<OperatorSettings>(this.operatorHost.OperatorSettings);

                    if (this.operatorHost.CertManagerOptions != null)
                    {
                        services.AddSingleton<CertManagerOptions>(this.operatorHost.CertManagerOptions);
                    }

                    foreach (var s in this.Services)
                    {
                        services.Add(s);
                    }
                })
                .UseKestrel(options =>
                {
                    if (!NeonHelper.IsDevWorkstation)
                    {
                        options.ConfigureHttpsDefaults(o =>
                        {
                            o.ServerCertificateSelector = (connectionContext, name) =>
                            {
                                return this.operatorHost.Certificate;
                            };
                        });
                        options.Listen(this.operatorHost.OperatorSettings.ListenAddress, this.operatorHost.OperatorSettings.Port, o =>
                        {
                            o.UseHttps(httpsOptions =>
                            {
                                httpsOptions.ServerCertificateSelector = (connectionContext, name) =>
                                {
                                    return this.operatorHost.Certificate;
                                };
                            });
                        });
                    }
                })
                .UseStartup(this.operatorHost.StartupType);
        

            return operatorHost;
        }

        /// <inheritdoc/>
        public void AddOperatorSettings(OperatorSettings operatorSettings)
        {
            this.operatorHost.OperatorSettings = operatorSettings;
        }

        /// <inheritdoc/>
        public void AddCertManagerOptions(CertManagerOptions certManagerOptions)
        {
            this.operatorHost.CertManagerOptions = certManagerOptions;
        }

        /// <inheritdoc/>
        public void UseStartup<TStartup>()
        {
            this.operatorHost.StartupType = typeof(TStartup);
        }
    }
}
