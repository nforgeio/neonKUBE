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

using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Neon.Kube.Operator;
using Neon.Kube.Operator.Builder;
using Neon.Net;

namespace Neon.Kube.Xunit.Operator
{

    /// <inheritdoc/>
    public class KubernetesOperatorTestHostBuilder : IKubernetesOperatorHostBuilder
    {
        /// <summary>
        /// The service collection.
        /// </summary>
        public IServiceCollection Services { get; set; }
        
        private KubernetesOperatorTestHost operatorHost;

        /// <summary>
        /// Constructor.
        /// </summary>
        public KubernetesOperatorTestHostBuilder(string[] args = null)
        {
            this.operatorHost = new KubernetesOperatorTestHost(args);
            this.Services     = new ServiceCollection();
        }

        /// <inheritdoc/>
        public IKubernetesOperatorHost Build()
        {
            this.operatorHost.HostBuilder = new WebHostBuilder();

            this.operatorHost.HostBuilder.ConfigureServices(services =>
                    {
                        foreach (var s in Services)
                        {
                            services.Add(s);
                        }
                    })
                .UseStartup<TestKubernetesStartup>()
                .UseKestrel(options =>
                    {
                        options.Listen(IPAddress.Loopback, NetHelper.GetUnusedTcpPort());
                    });

            this.operatorHost.Host = this.operatorHost.HostBuilder.Build();
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
