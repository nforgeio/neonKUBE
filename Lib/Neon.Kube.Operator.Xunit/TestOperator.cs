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

namespace Neon.Kube.Operator.Xunit
{
    /// <inheritdoc/>
    public class TestOperator : ITestOperator
    {
        private IHost host { get; set; }
        private IHostBuilder hostBuilder { get; set; }

        private KubernetesClientConfiguration k8sConfig { get; set; }

        /// <inheritdoc/>
        public IServiceCollection Services { get; }

        /// <inheritdoc/>
        public IOperatorBuilder Builder { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="k8sConfig"></param>
        public TestOperator(KubernetesClientConfiguration k8sConfig)
        {
            this.Services = new ServiceCollection();
            this.Builder = this.Services.AddKubernetesOperator(options =>
            {
                options.AssemblyScanningEnabled = false;
                options.KubernetesClientConfiguration = k8sConfig;
            });
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddController<T>()
            where T : class
        {
            return Builder.AddController<T>(leaderElectionDisabled: true);
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddFinalizer<T>()
            where T : class
        {
            return Builder.AddFinalizer<T>();
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddMutatingWebhook<T>()
            where T : class
        {
            return Builder.AddMutatingWebhook<T>();
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddValidatingWebhook<T>()
            where T : class
        {
            return Builder.AddValidatingWebhook<T>();
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddNgrokTunnnel()
        {
            return Builder.AddNgrokTunnnel();
        }

        /// <inheritdoc/>
        public async Task StartAsync()
        {
            this.hostBuilder = new HostBuilder()
                .ConfigureServices(services =>
                {
                    foreach (var s in Services)
                    {
                        services.Add(s);
                    }
                });
            this.host = hostBuilder.Build();
            await host.StartAsync();
        }

        /// <inheritdoc/>
        public void Start()
        {
            this.hostBuilder = new HostBuilder()
                .ConfigureServices(services =>
                {
                    foreach (var s in Services)
                    {
                        services.Add(s);
                    }
                });
            this.host = hostBuilder.Build();
            this.host.Start();
        }

        /// <inheritdoc/>
        public T GetController<T>()
        {
            return host.Services.GetService<T>();
        }

        /// <inheritdoc/>
        public T GetFinalizer<T>()
        {
            return host.Services.GetService<T>();
        }

        /// <inheritdoc/>
        public T GetMutatingWebhook<T>()
        {
            return host.Services.GetService<T>();
        }

        /// <inheritdoc/>
        public T GetValidatingWebhook<T>()
        {
            return host.Services.GetService<T>();
        }
    }
}
