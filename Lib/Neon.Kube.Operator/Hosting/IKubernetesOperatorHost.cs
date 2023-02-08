//-----------------------------------------------------------------------------
// FILE:	    IKubernetesOperatorHost.cs
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Neon.Kube.Operator.Builder;

using k8s.Models;
using k8s;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Kubernetes Operator Host.
    /// </summary>
    public interface IKubernetesOperatorHost
    {
        /// <summary>
        /// The host for the operator.
        /// </summary>
        IWebHost Host { get; set; }

        /// <summary>
        /// The host builder.
        /// </summary>
        IWebHostBuilder HostBuilder { get; set; }

        /// <summary>
        /// The Operator Settings.
        /// </summary>
        OperatorSettings OperatorSettings { get; set; }

        /// <summary>
        /// Cert Manager options.
        /// </summary>
        CertManagerOptions CertManagerOptions { get; set; }

        /// <summary>
        /// StartupType.
        /// </summary>
        Type StartupType { get; set; }

        /// <summary>
        /// SSL Cert.
        /// </summary>

        X509Certificate2 Certificate { get; set; }

        /// <summary>
        /// Run the Operator.
        /// </summary>
        /// <returns></returns>
        Task RunAsync();

        /// <summary>
        /// Run the Operator.
        /// </summary>
        /// <returns></returns>
        void Run();
    }
}
