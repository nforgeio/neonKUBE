//-----------------------------------------------------------------------------
// FILE:	    IDependentResource.cs
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
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube.Operator.Attributes;
using Neon.Tasks;

using k8s;
using k8s.Models;

using Prometheus;
using Neon.Kube.Resources;

namespace Neon.Kube.Operator.ResourceManager
{
    /// <summary>
    /// Defines a dependent resource.
    /// </summary>
    public interface IDependentResource
    {
        /// <summary>
        /// Returns the namespace for the dependent resource.
        /// </summary>
        /// <returns></returns>
        string Namespace();

        /// <summary>
        /// The scope of the dependent resource. This is either Namespaced or Cluster.
        /// </summary>
        EntityScope Scope { get; set; }

        /// <summary>
        /// Gets the Entity <see cref="Type"/>
        /// </summary>
        /// <returns></returns>
        Type GetEntityType();

        /// <summary>
        /// Returns the entity <see cref="KubernetesEntityAttribute"/>
        /// </summary>
        /// <returns></returns>
        KubernetesEntityAttribute GetKubernetesEntityAttribute();
    }
}
