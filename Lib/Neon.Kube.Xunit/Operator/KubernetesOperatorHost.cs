//-----------------------------------------------------------------------------
// FILE:	    KubernetesOperatorHost.cs
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
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Neon.Diagnostics;
using Neon.Kube.Operator.Commands.Generate;
using Neon.Kube.Operator;
using Neon.Kube.Operator.Builder;
using Microsoft.AspNetCore.Hosting;
using Neon.Common;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Neon.Kube.Resources.CertManager;
using Neon.Tasks;
using System.Xml.Linq;
using k8s;
using Neon.Kube.Operator.Rbac;
using OpenTelemetry.Resources;
using System.Diagnostics;

namespace Neon.Kube.Xunit.Operator
{
    /// <summary>
    /// Kubernetes operator Host.
    /// </summary>
    public class KubernetesOperatorTestHost : IKubernetesOperatorHost
    {
        /// <inheritdoc/>
        public IWebHost Host { get; set; }

        /// <inheritdoc/>
        public IWebHostBuilder HostBuilder { get; set; }

        /// <inheritdoc/>
        public CertManagerOptions CertManagerOptions { get; set; }

        /// <inheritdoc/>
        public OperatorSettings OperatorSettings { get; set; }

        /// <inheritdoc/>
        public Type StartupType { get; set; }

        /// <inheritdoc/>
        public X509Certificate2 Certificate { get; set; }

        private string[] args { get; set; }

        /// <summary>
        /// Consructor.
        /// </summary>
        /// <param name="args"></param>
        public KubernetesOperatorTestHost(string[] args = null)
        {
            this.args = args;
        }


        /// <inheritdoc/>
        public static KubernetesOperatorTestHostBuilder CreateDefaultBuilder(string[] args = null)
        {
            var builder = new KubernetesOperatorTestHostBuilder(args);
            return builder;
        }

        /// <inheritdoc/>
        public void Run()
        {
            Host.Start();
        }

        /// <inheritdoc/>
        public async Task RunAsync()
        {
            await Task.CompletedTask;

            throw new NotImplementedException();
        }
    }
}
