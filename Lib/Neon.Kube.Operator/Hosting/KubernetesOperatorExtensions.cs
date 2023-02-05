//-----------------------------------------------------------------------------
// FILE:	    KubernetesOperatorExtensions.cs
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

using Neon.Kube.Operator.Builder;

using k8s.Models;
using k8s;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Kubernetes operator <see cref="IHost"/> extension methods.
    /// </summary>
    public static class KubernetesOperatorExtensions
    {
        /// <summary>
        /// Configures defaults for the Kubernetes Host.
        /// </summary>
        /// <param name="k8sBuilder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static IKubernetesOperatorHostBuilder ConfigureHostDefaults(this IKubernetesOperatorHostBuilder k8sBuilder, Action<IHostBuilder> configure)
        {
            var hostBuilder = Host.CreateDefaultBuilder();

            configure?.Invoke(hostBuilder);
            k8sBuilder.AddHostBuilder(hostBuilder);

            return k8sBuilder;
        }

        /// <summary>
        /// Builds the host.
        /// </summary>
        /// <param name="k8sBuilder"></param>
        /// <returns></returns>
        public static IKubernetesOperatorHost Build(this IKubernetesOperatorHostBuilder k8sBuilder)
        {
            return k8sBuilder.Build();
        }

        /// <summary>
        /// Configures the host for deployment in NeonKUBE clusters.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IKubernetesOperatorHostBuilder ConfigureNeonKube(this IKubernetesOperatorHostBuilder builder)
        {
            return builder;
        }
    }
}
