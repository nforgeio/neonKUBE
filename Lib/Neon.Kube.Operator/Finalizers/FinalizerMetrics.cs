//-----------------------------------------------------------------------------
// FILE:	    FinalizerMetrics.cs
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
using System.Diagnostics.Metrics;

namespace Neon.Kube.Operator.Finalizer
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    internal class FinalizerMetrics<TEntity> : IFinalizerMetrics<TEntity>
        where TEntity : IKubernetesObject<V1ObjectMeta>
    {
        private const string prefix = "operator_finalizer";
        private static readonly string[] LabelNames = { "operator", "finalizer", "kind", "group", "version" };
        public ICounter RegistrationsTotal { get; }
        public IHistogram RegistrationTimeSeconds { get; }
        public ICounter RemovalsTotal { get; }
        public IHistogram RemovalTimeSeconds { get; }
        public IGauge FinalizingCount { get; }
        public ICounter FinalizedTotal { get; }
        public IHistogram FinalizeTimeSeconds { get; }
        public FinalizerMetrics(
            OperatorSettings operatorSettings,
            Type finalizerType) 
        {
            var crdMeta     = typeof(TEntity).GetKubernetesTypeMetadata();
            var labelValues = new string[] { operatorSettings.Name, finalizerType.Name.ToLower(), crdMeta.PluralName, crdMeta.Group, crdMeta.ApiVersion };

            RegistrationsTotal = Metrics
                .CreateCounter(
                    name: $"{prefix}_registrations_total",
                    help: "The total number of finalizer registrations.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            RegistrationTimeSeconds = Metrics
                .CreateHistogram(
                    name: $"{prefix}_registration_time_seconds",
                    help: "The time taken to register finalizers.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            RemovalsTotal = Metrics
                .CreateCounter(
                    name: $"{prefix}_removals_total",
                    help: "The total number of finalizer removals. Incremented after the finalizer has been run on a resource.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            RemovalTimeSeconds = Metrics
                .CreateHistogram(
                    name: $"{prefix}_removal_time_seconds",
                    help: "The time taken to remove finalizers from resources. This is after the resource has been finalized and is being removed via the apiserver.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            FinalizingCount = Metrics
                .CreateGauge(
                    name: $"{prefix}_finalizing_count",
                    help: "The number of finalizers currently running.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            FinalizedTotal = Metrics
                .CreateCounter(
                    name: $"{prefix}_finalized_total",
                    help: "The total number of resources finalized.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            FinalizeTimeSeconds = Metrics
                .CreateHistogram(
                    name: $"{prefix}_finalize_time_seconds",
                    help: "The time taken to finalize resources.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);
        }
    }
}
