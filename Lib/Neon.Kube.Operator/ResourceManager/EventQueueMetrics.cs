//-----------------------------------------------------------------------------
// FILE:	    EventQueueMetrics.cs
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

namespace Neon.Kube.Operator.EventQueue
{
    internal class EventQueueMetrics<TEntity, TController>
        where TEntity : IKubernetesObject<V1ObjectMeta>
    {
        private const string prefix = "operator_eventqueue";
        private static readonly string[] LabelNames = { "operator", "controller", "kind", "group", "version" };
        public Counter.Child AddsTotal { get; private set; }
        public Counter.Child RetriesTotal { get; private set; }
        public Gauge.Child Depth { get; private set; }
        public Histogram.Child QueueDurationSeconds { get; private set; }
        public Histogram.Child WorkDurationSeconds { get; private set; }
        public Gauge.Child UnfinishedWorkSeconds { get; private set; }
        public Gauge.Child LongestRunningProcessorSeconds { get; private set; }
        public Gauge.Child ActiveWorkers { get; private set; }
        public Gauge.Child MaxActiveWorkers { get; private set; }
        public EventQueueMetrics(OperatorSettings operatorSettings) 
        {
            var crdMeta     = typeof(TEntity).GetKubernetesTypeMetadata();
            var labelValues = new string[] 
            { 
                operatorSettings.Name, 
                typeof(TController).Name.ToLower(), 
                crdMeta.PluralName, 
                crdMeta.Group, 
                crdMeta.ApiVersion 
            };

            AddsTotal = Metrics
                .CreateCounter(
                    name: $"{prefix}_adds_total",
                    help: "The total number of queued items.",
                    labelNames: LabelNames).
                WithLabels(labelValues);

            RetriesTotal = Metrics
                .CreateCounter(
                    name: $"{prefix}_retries_total",
                    help: "The total number of retries.",
                    labelNames: LabelNames).
                WithLabels(labelValues);

            Depth = Metrics
                .CreateGauge(
                    name: $"{prefix}_depth", 
                    help: "The current depth of the event queue.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            QueueDurationSeconds = Metrics
                .CreateHistogram(
                    name: $"{prefix}_queue_duration_seconds",
                    help: "How long in seconds an item stays in the event queue before being handled by the controller.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            WorkDurationSeconds = Metrics
                .CreateHistogram(
                    name: $"{prefix}_work_duration_seconds",
                    help: "How long in seconds it takes to process an item from the queue.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            UnfinishedWorkSeconds = Metrics
                .CreateGauge(
                    name: $"{prefix}_unfinished_work_seconds",
                    help: "The amount of seconds that work is in progress without completing. Large values indicate stuck threads.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            LongestRunningProcessorSeconds = Metrics
                .CreateGauge(
                    name: $"{prefix}_longest_running_processor_seconds",
                    help: "The amount of seconds that work is in progress without completing. Large values indicate stuck threads.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            ActiveWorkers = Metrics
                .CreateGauge(
                    name: $"{prefix}_active_workers",
                    help: "The number of currently active reconcilers.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            MaxActiveWorkers = Metrics
                .CreateGauge(
                    name: $"{prefix}_max_active_workers",
                    help: "Total number of reconciliations per controller.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

        }
    }
}
