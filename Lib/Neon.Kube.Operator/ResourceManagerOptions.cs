//-----------------------------------------------------------------------------
// FILE:	    ResourceManagerOptions.cs
// CONTRIBUTOR: Jeff Lill
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
using Neon.Tasks;

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
        /// Specifies whether Kubernetes custom resources should be created.
        /// </summary>
        public bool ManageCustomResourceDefinitions { get; set; } = true;

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
