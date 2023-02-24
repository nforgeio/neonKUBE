//-----------------------------------------------------------------------------
// FILE:	    ResourceManagerMetrics.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using Neon.Kube.Operator.Controller;
using Neon.Tasks;

using k8s;
using Prometheus;
using k8s.Models;

namespace Neon.Kube.Operator.ResourceManager
{
    /// <summary>
    /// Specifies metrics for a resource manager.  See the <see cref="ResourceManager{TResource, TController}"/>.
    /// </summary>
    /// <typeparam name="TEntity">Specifies the entity type.</typeparam>
    /// <typeparam name="TController">Specifies the controller type.</typeparam>
    internal class ResourceManagerMetrics<TEntity, TController>
        where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        where TController : IResourceController<TEntity>
    {
        private const string prefix = "operator_controller";

        private static readonly string[] LabelNames = { "operator", "controller", "kind", "group", "version", };

        /// <summary>
        /// Default constructor.
        /// </summary>
        static ResourceManagerMetrics()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="operatorSettings">Specifies the operator settings.</param>
        public ResourceManagerMetrics(OperatorSettings operatorSettings)
        {
            Covenant.Requires<ArgumentNullException>(operatorSettings != null, nameof(operatorSettings));

            var crdMeta     = typeof(TEntity).GetKubernetesTypeMetadata();
            var labelValues = new string[] { operatorSettings.Name, typeof(TController).Name.ToLower(), crdMeta.PluralName, crdMeta.Group, crdMeta.ApiVersion };

            IdleCounter = Metrics
                .CreateCounter(
                    name: $"{prefix}_idle_total",
                    help: "IDLE events handled by the controller.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            ReconcileEventsTotal = Metrics
                .CreateCounter(
                    name: $"{prefix}_reconcile_total",
                    help: "Total number of reconciliations per controller.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            DeleteEventsTotal = Metrics
                .CreateCounter(
                    name: $"{prefix}_delete_total",
                    help: "Total number of delete events per controller.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            StatusModifiedTotal = Metrics
                .CreateCounter(
                    name: $"{prefix}_statusmodify_total",
                    help: "Total number of status updates handled by the controller.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            FinalizeTotal = Metrics
                .CreateCounter(
                    name: $"{prefix}_finalize_total",
                    help: "Total number of finalize events handled by the controller.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            IdleErrorsTotal = Metrics
                .CreateCounter(
                    name: $"{prefix}_idle_errors_total",
                    help: "The number of errors that occured during idle.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            ReconcileErrorsTotal = Metrics
                .CreateCounter(
                    name: $"{prefix}_reconcile_errors_total",
                    help: "The number of exceptions thrown while handling reconcile events.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            DeleteErrorsTotal = Metrics
                .CreateCounter(
                    name: $"{prefix}_delete_errors_total",
                    help: "The number of exceptions thrown while handling delete events.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            StatusModifiedErrorsTotal = Metrics
                .CreateCounter(
                    name: $"{prefix}_statusmodified_errors_total",
                    help: "The number of exceptions thrown while handling status updates.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            FinalizeErrorsTotal = Metrics
                .CreateCounter(
                    name: $"{prefix}_finalize_errors_total",
                    help: "The number of exceptions thrown while handling finalize events.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            IdleTimeSeconds = Metrics
                .CreateHistogram(
                    name: $"{prefix}_idle_time_seconds",
                    help: "How long in seconds the operator spent processing idle requests.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            ReconcileTimeSeconds = Metrics
                .CreateHistogram(
                    name: $"{prefix}_reconcile_time_seconds",
                    help: "How long in seconds the operator spent reconciling resources.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            DeleteTimeSeconds = Metrics
                .CreateHistogram(
                    name: $"{prefix}_delete_time_seconds",
                    help: "How long in seconds the operator spent deleting resources.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            StatusModifiedTimeSeconds = Metrics
                .CreateHistogram(
                    name: $"{prefix}_statusmodified_time_seconds",
                    help: "How long in seconds the operator spent processing status updated requests.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            FinalizeTimeSeconds = Metrics
                .CreateHistogram(
                    name: $"{prefix}_finalize_time_seconds",
                    help: "How long in seconds the operator spent finalizing resources.",
                    labelNames: LabelNames)
                .WithLabels(labelValues);
        }

        /// <summary>
        /// <para>
        /// Metrics counter incremented whenever a IDLE event is passed to the operator.  This 
        /// defaults to a counter names <b>operator_idle</b> which is suitable for operator 
        /// applications with only a single control loop.  
        /// </para>
        /// <para>
        /// Operators with multiple control loops should consider setting this to a counter specific
        /// to each loop.
        /// </para>
        /// <note>
        /// This may also be set to <c>null</c> to disable counting.
        /// </note>
        /// </summary>
        public Counter.Child IdleCounter { get; private set; }

        /// <summary>
        /// <para>
        /// Metrics counter incremented whenever a RECONCILE event is passed to the operator.  This 
        /// defaults to a counter names <b>operator_reconcile</b> which is suitable for operator 
        /// applications with only a single control loop.  
        /// </para>
        /// <para>
        /// Operators with multiple control loops should consider setting this to a counter specific
        /// to each loop.
        /// </para>
        /// <note>
        /// This may also be set to <c>null</c> to disable counting.
        /// </note>
        /// </summary>
        public Counter.Child ReconcileEventsTotal { get; set; }

        /// <summary>
        /// <para>
        /// Metrics counter incremented whenever a DELETE event is passed to the operator.  This 
        /// defaults to a counter names <b>operator_delete</b> which is suitable for operator 
        /// applications with only a single control loop.  
        /// </para>
        /// <para>
        /// Operators with multiple control loops should consider setting this to a counter specific
        /// to each loop.
        /// </para>
        /// <note>
        /// This may also be set to <c>null</c> to disable counting.
        /// </note>
        /// </summary>
        public Counter.Child DeleteEventsTotal { get; set; }

        /// <summary>
        /// <para>
        /// Metrics counter incremented whenever a STATUS-MODIFIED event is passed to the operator.  This 
        /// defaults to a counter names <b>operator_statusmodify</b> which is suitable for operator 
        /// applications with only a single control loop.  
        /// </para>
        /// <para>
        /// Operators with multiple control loops should consider setting this to a counter specific
        /// to each loop.
        /// </para>
        /// <note>
        /// This may also be set to <c>null</c> to disable counting.
        /// </note>
        /// </summary>
        public Counter.Child StatusModifiedTotal { get; set; }

        /// <summary>
        /// <para>
        /// Metrics counter incremented whenever a FINALIZE event is passed to the operator.  This 
        /// defaults to a counter names <b>operator_finalize</b> which is suitable for operator 
        /// applications with only a single control loop.  
        /// </para>
        /// <para>
        /// Operators with multiple control loops should consider setting this to a counter specific
        /// to each loop.
        /// </para>
        /// <note>
        /// This may also be set to <c>null</c> to disable counting.
        /// </note>
        /// </summary>
        public Counter.Child FinalizeTotal { get; set; }

        /// <summary>
        /// <para>
        /// Metrics counter incremented whenever a IDLE event is passed to the operator.  This 
        /// defaults to a counter names <b>operator_idle</b> which is suitable for operator 
        /// applications with only a single control loop.  
        /// </para>
        /// <para>
        /// Operators with multiple control loops should consider setting this to a counter specific
        /// to each loop.
        /// </para>
        /// <note>
        /// This may also be set to <c>null</c> to disable counting.
        /// </note>
        /// </summary>
        public Counter.Child IdleErrorsTotal { get; set; }

        /// <summary>
        /// <para>
        /// Metrics counter incremented whenever an exception is thrown while handling a resource
        /// reconiciliation.  This defaults to a counter names <b>operator_reconcile_errors</b> which
        /// is suitable for operator applications with only a single control loop.  
        /// </para>
        /// <para>
        /// Operators with multiple control loops should consider setting this to a counter specific
        /// to each loop.
        /// </para>
        /// </summary>
        public Counter.Child ReconcileErrorsTotal { get; set; }

        /// <summary>
        /// <para>
        /// Metrics counter incremented whenever an exception is thrown while handling a resource
        /// deletion.  This defaults to a counter names <b>operator_delete_errors</b> which
        /// is suitable for operator applications with only a single control loop.  
        /// </para>
        /// <para>
        /// Operators with multiple control loops should consider setting this to a counter specific
        /// to each loop.
        /// </para>
        /// </summary>
        public Counter.Child DeleteErrorsTotal { get; set; }

        /// <summary>
        /// <para>
        /// Metrics counter incremented whenever an exception is thrown while handling a resource
        /// status modification.  This defaults to a counter names <b>operator_statusmodified_errors</b> which
        /// is suitable for operator applications with only a single control loop.  
        /// </para>
        /// <para>
        /// Operators with multiple control loops should consider setting this to a counter specific
        /// to each loop.
        /// </para>
        /// </summary>
        public Counter.Child StatusModifiedErrorsTotal { get; set; }

        /// <summary>
        /// <para>
        /// Metrics counter incremented whenever an exception is thrown while handling a resource
        /// finalize. This defaults to a counter named <b>operator_finalize_errors</b> which
        /// is suitable for operator applications with only a single control loop.  
        /// </para>
        /// <para>
        /// Operators with multiple control loops should consider setting this to a counter specific
        /// to each loop.
        /// </para>
        /// </summary>
        public Counter.Child FinalizeErrorsTotal { get; set; }

        /// <summary>
        /// <para>
        /// The time taken for IDLE calls.
        /// </para>
        /// </summary>
        public Histogram.Child IdleTimeSeconds { get; set; }

        /// <summary>
        /// <para>
        /// The time taken for RECONCILE calls.
        /// </para>
        /// </summary>
        public Histogram.Child ReconcileTimeSeconds { get; set; }

        /// <summary>
        /// <para>
        /// The time taken for DELETE calls.
        /// </para>
        /// </summary>
        public Histogram.Child DeleteTimeSeconds { get; set; }

        /// <summary>
        /// <para>
        /// The time taken for STATUS-MODIFIED calls.
        /// </para>
        /// </summary>
        public Histogram.Child StatusModifiedTimeSeconds { get; set; }

        /// <summary>
        /// <para>
        /// The time taken for FINALIZE calls.
        /// </para>
        /// </summary>
        public Histogram.Child FinalizeTimeSeconds { get; set; }

        /// <summary>
        /// <para>
        /// The number of currently active reconcilers.
        /// </para>
        /// </summary>
        public Gauge.Child ActiveReconciles { get; set; }

        /// <summary>
        /// <para>
        /// The maximum number of concurrent reconcilers.
        /// </para>
        /// </summary>
        public Gauge.Child MaxConcurrentReconciles { get; set; }
    }
}
