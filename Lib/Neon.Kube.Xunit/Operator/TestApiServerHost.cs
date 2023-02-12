// FILE:	    TestApiServerHost.cs
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

namespace Neon.Kube.Xunit.Operator
{

    /// <inheritdoc/>
    public class TestApiServerHost : ITestApiServerHost
    {
        private readonly IHost host;
        private bool disposedValue;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="kubeConfig"></param>
        /// <param name="client"></param>
        public TestApiServerHost(IHost host, K8SConfiguration kubeConfig, IKubernetes client)
        {
            this.host = host;
            KubeConfig = kubeConfig;
            Client = client;
            Services = new ServiceCollection();
        }

        /// <inheritdoc/>
        public IServiceCollection Services { get; }

        /// <inheritdoc/>
        public ITestApiServer Cluster => host.Services.GetRequiredService<ITestApiServer>();

        /// <inheritdoc/>
        public K8SConfiguration KubeConfig { get; }

        /// <inheritdoc/>
        public IKubernetes Client { get; }

        /// <inheritdoc/>
        IServiceProvider IHost.Services => throw new NotImplementedException();

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken cancellationToken = default) => host.StartAsync(cancellationToken);

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken = default) => host.StartAsync(cancellationToken);

        /// <inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    host.Dispose();
                }

                disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
