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
using Microsoft.AspNetCore;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Kubernetes operator <see cref="IHost"/> extension methods.
    /// </summary>
    public static class KubernetesOperatorExtensions
    {
        /// <summary>
        /// Configures the Kubernetes Operator.
        /// </summary>
        /// <param name="k8sBuilder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static IKubernetesOperatorHostBuilder ConfigureOperator(this IKubernetesOperatorHostBuilder k8sBuilder, Action<OperatorSettings> configure = null)
        {
            var operatorSettings = new OperatorSettings();

            configure?.Invoke(operatorSettings);
            k8sBuilder.AddOperatorSettings(operatorSettings);

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
        /// <param name="k8sBuilder"></param>
        /// <returns></returns>
        public static IKubernetesOperatorHostBuilder ConfigureNeonKube(this IKubernetesOperatorHostBuilder k8sBuilder)
        {
            k8sBuilder.ConfigureCertManager(configure =>
            {
                configure.CertificateDuration = TimeSpan.FromDays(90);
                configure.IssuerRef = new Resources.CertManager.IssuerRef()
                {
                    Name = "cluster-selfsigned-issuer",
                    Kind = "ClusterIssuer"
                };
            });

            return k8sBuilder;
        }

        /// <summary>
        /// Configures the host for deployment in NeonKUBE clusters.
        /// </summary>
        /// <param name="k8sBuilder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static IKubernetesOperatorHostBuilder ConfigureCertManager(
            this IKubernetesOperatorHostBuilder k8sBuilder,
            Action<CertManagerOptions> configure)
        {
            var certManagerOptions = new CertManagerOptions();

            configure?.Invoke(certManagerOptions);
            k8sBuilder.AddCertManagerOptions(certManagerOptions);

            return k8sBuilder;
        }

        /// <summary>
        /// Configures the startup class to use.
        /// </summary>
        /// <typeparam name="TStartup"></typeparam>
        /// <param name="k8sBuilder"></param>
        /// <returns></returns>
        public static IKubernetesOperatorHostBuilder UseStartup<TStartup>(this IKubernetesOperatorHostBuilder k8sBuilder)
        {
            k8sBuilder.UseStartup<TStartup>();
            return k8sBuilder;
        }

        /// <summary>
        /// Add a singleton to the service collection.
        /// </summary>
        /// <typeparam name="TSingleton"></typeparam>
        /// <param name="k8sBuilder"></param>
        /// <returns></returns>
        public static IKubernetesOperatorHostBuilder AddSingleton<TSingleton>(this IKubernetesOperatorHostBuilder k8sBuilder)
        {
            k8sBuilder.Services.AddSingleton(typeof(TSingleton));

            return k8sBuilder;
        }

        /// <summary>
        /// Add a singleton to the service collection.
        /// </summary>
        /// <typeparam name="TSingleton"></typeparam>
        /// <param name="k8sBuilder"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static IKubernetesOperatorHostBuilder AddSingleton<TSingleton>(this IKubernetesOperatorHostBuilder k8sBuilder, TSingleton instance)
        {
            k8sBuilder.Services.AddSingleton(typeof(TSingleton), instance);

            return k8sBuilder;
        }

        /// <summary>
        /// Add a singleton to the service collection.
        /// </summary>
        /// <param name="k8sBuilder"></param>
        /// <param name="type"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public static IKubernetesOperatorHostBuilder AddSingleton(this IKubernetesOperatorHostBuilder k8sBuilder, Type type, object instance)
        {
            k8sBuilder.Services.AddSingleton(type, instance);

            return k8sBuilder;
        }
    }
}
