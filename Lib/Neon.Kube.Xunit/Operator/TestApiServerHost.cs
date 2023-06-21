// FILE:        TestApiServerHost.cs
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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Neon.Kube.Operator;
using Neon.Kube.Operator.Builder;

using k8s.KubeConfigModels;
using k8s;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Diagnostics.Contracts;

namespace Neon.Kube.Xunit.Operator
{
    /// <inheritdoc/>
    public class TestApiServerHost : ITestApiServerHost
    {
        private readonly IHost  host;
        private bool            isDisposed;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="host">Specifies the host.</param>
        /// <param name="kubeConfig">Specifies the Kubernmetes configuration.</param>
        /// <param name="k8s">Specifies the Kubernetes client.</param>
        public TestApiServerHost(IHost host, K8SConfiguration kubeConfig, IKubernetes k8s)
        {
            Covenant.Requires<ArgumentNullException>(host != null, nameof(host));
            Covenant.Requires<ArgumentNullException>(kubeConfig != null, nameof(kubeConfig));
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            this.host       = host;
            this.KubeConfig = kubeConfig;
            this.K8s        = k8s;
            this.Services   = new ServiceCollection();
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~TestApiServerHost()
        {
            Dispose(disposing: false);
        }

        /// <inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    host.Dispose();
                }

                isDisposed = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public IServiceCollection Services { get; }

        /// <inheritdoc/>
        public ITestApiServer Cluster => host.Services.GetRequiredService<ITestApiServer>();

        /// <inheritdoc/>
        public K8SConfiguration KubeConfig { get; }

        /// <inheritdoc/>
        public IKubernetes K8s { get; }

        /// <inheritdoc/>
        IServiceProvider IHost.Services => throw new NotImplementedException();

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken cancellationToken = default) => host.StartAsync(cancellationToken);

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken = default) => host.StartAsync(cancellationToken);
    }
}
