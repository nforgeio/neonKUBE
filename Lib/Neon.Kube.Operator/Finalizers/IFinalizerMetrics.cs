//-----------------------------------------------------------------------------
// FILE:	    IFinalizerMetrics.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Diagnostics;
using Neon.Tasks;
using Neon.Kube.Operator.ResourceManager;

using k8s;
using k8s.Models;

using KellermanSoftware.CompareNetObjects;

using Prometheus;
using Microsoft.AspNetCore.Mvc;

namespace Neon.Kube.Operator.Finalizer
{
    /// <summary>
    /// Describes a finalizer metrics implementation.
    /// </summary>
    /// <typeparam name="TEntity">Specifies the entity type.</typeparam>
    internal interface IFinalizerMetrics<TEntity> : IFinalizerMetrics
        where TEntity : IKubernetesObject<V1ObjectMeta>
    {
    }

    /// <summary>
    /// Describes finalizer metrics.
    /// </summary>
    internal interface IFinalizerMetrics
    {
        /// <summary>
        /// $todo(marcusbooyah): Documentation
        /// </summary>
        ICounter RegistrationsTotal { get; }

        /// <summary>
        /// $todo(marcusbooyah): Documentation
        /// </summary>
        IHistogram RegistrationTimeSeconds { get; }

        /// <summary>
        /// $todo(marcusbooyah): Documentation
        /// </summary>
        ICounter RemovalsTotal { get; }

        /// <summary>
        /// $todo(marcusbooyah): Documentation
        /// </summary>
        IHistogram RemovalTimeSeconds { get; }

        /// <summary>
        /// $todo(marcusbooyah): Documentation
        /// </summary>
        IGauge FinalizingCount { get; }

        /// <summary>
        /// $todo(marcusbooyah): Documentation
        /// </summary>
        ICounter FinalizedTotal { get; }

        /// <summary>
        /// $todo(marcusbooyah): Documentation
        /// </summary>
        IHistogram FinalizeTimeSeconds { get; }
    }
}
