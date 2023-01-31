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
    public class ResourceManagerMetrics<TEntity, TController>
        where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        where TController : IResourceController<TEntity>
    {
        private static string metricsPrefix;

        /// <summary>
        /// Default constructor.
        /// </summary>
        static ResourceManagerMetrics()
        {
            metricsPrefix = $"{typeof(TController).Name}_{typeof(TEntity).Name}".ToLower();
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ResourceManagerMetrics()
        {
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
        public Counter IdleCounter { get; set; } = Metrics.CreateCounter($"{metricsPrefix}_idle", "IDLE events handled by the controller.");

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
        public Counter ReconcileCounter { get; set; } = Metrics.CreateCounter($"{metricsPrefix}_reconcile", "RECONCILE events handled by the controller.");

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
        public Counter DeleteCounter { get; set; } = Metrics.CreateCounter($"{metricsPrefix}_delete", "DELETE events handled by the controller.");

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
        public Counter StatusModifyCounter { get; set; } = Metrics.CreateCounter($"{metricsPrefix}_statusmodify", "STATUS-MODIFY events handled by the controller.");

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
        public Counter FinalizeCounter { get; set; } = Metrics.CreateCounter($"{metricsPrefix}_finalize", "FINALIZE events handled by the controller.");

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
        public Counter IdleErrorCounter { get; set; } = Metrics.CreateCounter($"{metricsPrefix}_idle", "IDLE events handled by the controller.");

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
        public Counter ReconcileErrorCounter { get; set; } = Metrics.CreateCounter($"{metricsPrefix}_reconcile_errors", "Exceptions thrown while handling RECONCILE events.");

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
        public Counter DeleteErrorCounter { get; set; } = Metrics.CreateCounter($"{metricsPrefix}_delete_errors", "Exceptions thrown while handling DELETE events.");

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
        public Counter StatusModifyErrorCounter { get; set; } = Metrics.CreateCounter($"{metricsPrefix}_statusmodified_errors", "Exceptions thrown while handling STATUS-MODIFIED events.");

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
        public Counter FinalizeErrorCounter { get; set; } = Metrics.CreateCounter($"{metricsPrefix}_finalize_errors", "Exceptions thrown while handling FINALIZING events.");
    }
}
