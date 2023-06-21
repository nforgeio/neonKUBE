//-----------------------------------------------------------------------------
// FILE:        ServiceCollectionExtensions.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;

using k8s.Models;
using k8s;
using Neon.Kube.Operator.Builder;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Kubernetes operator <see cref="IServiceCollection"/> extension methods.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Kubernetes operator to the service collection.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <returns>The <see cref="OperatorBuilder"/>.</returns>
        public static IOperatorBuilder AddKubernetesOperator(this IServiceCollection services)
        {
            return new OperatorBuilder(services).AddOperatorBase();
        }

        /// <summary>
        /// Adds Kubernetes operator to the service collection.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="options">Optional options</param>
        /// <returns>The <see cref="OperatorBuilder"/>.</returns>
        public static IOperatorBuilder AddKubernetesOperator(this IServiceCollection services, Action<OperatorSettings> options)
        {
            var settings = new OperatorSettings();
            options?.Invoke(settings);

            services.AddSingleton(settings);

            return new OperatorBuilder(services).AddOperatorBase();
        }
    }
}
