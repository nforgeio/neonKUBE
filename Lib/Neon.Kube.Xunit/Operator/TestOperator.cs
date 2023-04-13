// FILE:	    TestOperator.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Neon.Kube.Operator;
using Neon.Kube.Operator.Builder;

using k8s;

// $todo(marcusbooyah): Would it make more sense for this to be in a test project?

namespace Neon.Kube.Xunit.Operator
{
    /// <inheritdoc/>
    public class TestOperator : ITestOperator
    {
        private KubernetesOperatorTestHost          host;
        private KubernetesOperatorTestHostBuilder   hostBuilder;
        private IOperatorBuilder                    operatorBuilder;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="k8sConfig">Specifies the LKubernetes configuration.</param>
        public TestOperator(KubernetesClientConfiguration k8sConfig)
        {
            Covenant.Requires<ArgumentNullException>(k8sConfig != null, nameof(k8sConfig));

            var operatorSettings = new OperatorSettings()
            {
                Port                          = 1234,
                AssemblyScanningEnabled       = false,
                Name                          = "my-cool-operator",
                WatchNamespace                = "default",
                KubernetesClientConfiguration = k8sConfig
            };

            hostBuilder = (KubernetesOperatorTestHostBuilder)KubernetesOperatorTestHost
                .CreateDefaultBuilder()
                .ConfigureOperator(configure =>
                {
                    configure.AssemblyScanningEnabled = operatorSettings.AssemblyScanningEnabled;
                    configure.KubernetesClientConfiguration = operatorSettings.KubernetesClientConfiguration;
                });

            hostBuilder.Services.AddSingleton(operatorSettings);

            operatorBuilder = hostBuilder.Services.AddKubernetesOperator();
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddController<T>()
            where T : class
        {
            return operatorBuilder.AddController<T>(leaderElectionDisabled: true);
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddFinalizer<T>()
            where T : class
        {
            return operatorBuilder.AddFinalizer<T>();
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddMutatingWebhook<T>()
            where T : class
        {
            return operatorBuilder.AddMutatingWebhook<T>();
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddValidatingWebhook<T>()
            where T : class
        {
            return operatorBuilder.AddValidatingWebhook<T>();
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddNgrokTunnnel()
        {
            return operatorBuilder.AddNgrokTunnnel();
        }

        /// <inheritdoc/>
        public async Task StartAsync()
        {
            await host.RunAsync();
        }

        /// <inheritdoc/>
        public void Start()
        {
            host = (KubernetesOperatorTestHost)hostBuilder.Build();
            host.Host.Start();
        }

        /// <inheritdoc/>
        public T GetController<T>()
        {
            return host.Host.Services.GetService<T>();
        }

        /// <inheritdoc/>
        public T GetFinalizer<T>()
        {
            return host.Host.Services.GetService<T>();
        }

        /// <inheritdoc/>
        public T GetMutatingWebhook<T>()
        {
            return host.Host.Services.GetService<T>();
        }

        /// <inheritdoc/>
        public T GetValidatingWebhook<T>()
        {
            return host.Host.Services.GetService<T>();
        }
    }
}
