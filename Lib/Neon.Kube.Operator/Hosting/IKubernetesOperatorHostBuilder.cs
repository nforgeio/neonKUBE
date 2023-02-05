//-----------------------------------------------------------------------------
// FILE:	    IKubernetesOperatorHostBuilder.cs
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
    /// Kubernetes Operator host builder.
    /// </summary>
    public interface IKubernetesOperatorHostBuilder
    {
        internal void AddHostBuilder(IHostBuilder hostBuilder);
        internal void AddOperatorSettings(OperatorSettings operatorSettings);
        internal void AddCertManagerOptions(CertManagerOptions certManagerOptions);
        internal void UseStartup<TStartup>();

        /// <summary>
        /// Build the host.
        /// </summary>
        /// <returns></returns>
        IKubernetesOperatorHost Build();
    }
}
