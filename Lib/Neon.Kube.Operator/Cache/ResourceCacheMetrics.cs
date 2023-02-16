//-----------------------------------------------------------------------------
// FILE:	    ResourceCacheMetrics.cs
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

namespace Neon.Kube.Operator.Cache
{
    internal class ResourceCacheMetrics<TEntity>
        where TEntity : IKubernetesObject<V1ObjectMeta>
    {
        private const string prefix = "operator_cache";

        private static readonly string[] LabelNames = { "operator", "kind", "group", "version" };
        public Counter.Child ItemsTotal { get; private set; }
        public Gauge.Child ItemsCount { get; private set; }
        public Counter.Child HitsTotal { get; private set; }

        public ResourceCacheMetrics(OperatorSettings operatorSettings) 
        {
            var crdMeta     = typeof(TEntity).GetKubernetesTypeMetadata();
            var labelValues = new string[] { operatorSettings.Name, crdMeta.PluralName, crdMeta.Group, crdMeta.ApiVersion };

            ItemsTotal = Metrics
                .CreateCounter(
                    name: $"{prefix}_items_total",
                    help: "The total number of cached items", 
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            ItemsCount = Metrics
                .CreateGauge(
                    name: $"{prefix}_items_count",
                    help: "The current number of cached items",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            HitsTotal  = Metrics
                .CreateCounter(
                    name: $"{prefix}_hits_total",
                    help: "The total number of cache hits",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

        }
    }
}
