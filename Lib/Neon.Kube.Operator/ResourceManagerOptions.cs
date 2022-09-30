//-----------------------------------------------------------------------------
// FILE:	    ResourceManagerOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using Neon.Tasks;

using KubeOps.Operator;
using KubeOps.Operator.Builder;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Entities;

using k8s;
using Prometheus;
using k8s.Models;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Specifies options for a resource manager.  See the <see cref="ResourceManager{TResource, TController}"/>
    /// remarks for more information.
    /// </summary>
    public class ResourceManagerOptions
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ResourceManagerOptions()
        {
        }

        /// <summary>
        /// Specifies the interval at which reconcile events indicating that nothing has changed will
        /// be raised.  These IDLE events are a good time for controllers to operate on the entire set 
        /// of resources.  This defaults to <b>1 minutes</b>.
        /// </summary>
        public TimeSpan IdleInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Specifies the minimum timeout to before retrying after an error.  Timeouts will start
        /// at <see cref="ErrorMinRequeueInterval"/> and increase to <see cref="ErrorMaxRequeueInterval"/>
        /// until the error is resolved.  This defaults to <b>15 seconds</b>.
        /// </summary>
        public TimeSpan ErrorMinRequeueInterval { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Specifies the maximum timeout to before retrying after an error.  Timeouts will start
        /// at <see cref="ErrorMinRequeueInterval"/> and increase to <see cref="ErrorMaxRequeueInterval"/>
        /// until the error is resolved.  This defaults to <b>10 minutes</b>.
        /// </summary>
        public TimeSpan ErrorMaxRequeueInterval { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Specifies the maximum number of attempts to retry after an error.
        /// This defaults to <b>10</b>.
        /// </summary>
        public int ErrorMaxRetryCount { get; set; } = 10;

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
        public Counter IdleCounter { get; set; } = Metrics.CreateCounter("operator_idle", "IDLE events handled by the controller.");

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
        public Counter ReconcileCounter { get; set; } = Metrics.CreateCounter("operator_reconcile", "RECONCILE events handled by the controller.");

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
        public Counter DeleteCounter { get; set; } = Metrics.CreateCounter("operator_delete", "DELETE events handled by the controller.");

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
        public Counter StatusModifyCounter { get; set; } = Metrics.CreateCounter("operator_statusmodify", "STATUS-MODIFY events handled by the controller.");

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
        public Counter IdleErrorCounter { get; set; } = Metrics.CreateCounter("operator_idle", "IDLE events handled by the controller.");

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
        public Counter ReconcileErrorCounter { get; set; } = Metrics.CreateCounter("operator_reconcile_errors", "Exceptions thrown while handling RECONCILE events.");

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
        public Counter DeleteErrorCounter { get; set; } = Metrics.CreateCounter("operator_delete_errors", "Exceptions thrown while handling DELETE events.");

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
        public Counter StatusModifyErrorCounter { get; set; } = Metrics.CreateCounter("operator_statusmodified_errors", "Exceptions thrown while handling STATUS-MODIFIED events.");

        /// <summary>
        /// Validates the option properties.
        /// </summary>
        /// <exception cref="ValidationException">Thrown when any of the properties are invalid.</exception>
        public void Validate()
        {
            if (ErrorMinRequeueInterval < TimeSpan.Zero)
            {
                throw new ValidationException($"[{nameof(ErrorMinRequeueInterval)}={ErrorMinRequeueInterval}] cannot be less than zero.");
            }

            if (ErrorMaxRequeueInterval < TimeSpan.Zero)
            {
                throw new ValidationException($"[{nameof(ErrorMaxRequeueInterval)}={ErrorMaxRequeueInterval}] cannot be less than zero.");
            }
        }
    }
}
