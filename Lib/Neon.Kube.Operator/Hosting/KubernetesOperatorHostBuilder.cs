//-----------------------------------------------------------------------------
// FILE:	    KubernetesOperatorHostBuilder.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

namespace Neon.Kube.Operator
{

    /// <inheritdoc/>
    public class KubernetesOperatorHostBuilder : IKubernetesOperatorHostBuilder
    {
        private KubernetesOperatorHost operatorHost;

        public IServiceCollection Services { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public KubernetesOperatorHostBuilder(string[] args = null)
        {
            this.operatorHost = new KubernetesOperatorHost(args);
            this.Services     = new ServiceCollection();
        }

        /// <inheritdoc/>
        public IKubernetesOperatorHost Build()
        {
            this.operatorHost.HostBuilder = new WebHostBuilder();

            this.operatorHost.HostBuilder
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton<OperatorSettings>(this.operatorHost.OperatorSettings);
                        services.AddSingleton<CertManagerOptions>(this.operatorHost.CertManagerOptions);

                        foreach (var s in this.Services)
                        {
                            services.Add(s);
                        }
                    })
                    .UseStartup(this.operatorHost.StartupType);

            if (!NeonHelper.IsDevWorkstation)
            {
                this.operatorHost.HostBuilder.UseKestrel(options =>
                {
                    options.ConfigureEndpointDefaults(o =>
                    {
                        o.UseHttps(this.operatorHost.Certificate);
                    });

                    options.Listen(this.operatorHost.OperatorSettings.ListenAddress, this.operatorHost.OperatorSettings.Port);
                });
            }
            else
            {
                this.operatorHost.HostBuilder.UseKestrel();
            }

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
