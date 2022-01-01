//-----------------------------------------------------------------------------
// FILE:	    NeonServiceStatus.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Service;

namespace Neon.Service
{
    /// <summary>
    /// Enumerates the possible <see cref="NeonService"/> running states.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Most of these values are self-explanatory, but <see cref="Running"/> and
    /// <see cref="NotReady"/> may be a bit confusing.
    /// </para>
    /// <para>
    /// <see cref="Running"/> means that the service is healthy and is ready to
    /// process requests where as <see cref="NotReady"/> means that the service
    /// is healthy but is <b>not ready to process requests</b>.
    /// </para>
    /// <para>
    /// Most services are ready to accept traffic almost immediately after starting,
    /// so setting <see cref="Running"/> makes sense most of the time.  Some services
    /// though, may take some time after starting before being ready to process
    /// requests.  The problem is that the service typically has a limited amount
    /// of time before startup or liveliness probes will fail and resulting in the
    /// service termination.  Setting the <see cref="NotReady"/> state along with
    /// a readiness probe will prevent these other probes from terminating the service.
    /// </para>
    /// </remarks>
    public enum NeonServiceStatus
    {
        /// <summary>
        /// The service has not been started.
        /// </summary>
        [EnumMember(Value = "not-started")]
        NotStarted = 0,

        /// <summary>
        /// The service is in the process of starting but is not yet 
        /// fully initialized.
        /// </summary>
        [EnumMember(Value = "starting")]
        Starting,

        /// <summary>
        /// The service is running and ready for traffic.
        /// </summary>
        [EnumMember(Value = "running")]
        Running,

        /// <summary>
        /// Indicates that the service is running but it's not ready to receive
        /// external traffic.
        /// </summary>
        [EnumMember(Value = "not-ready")]
        NotReady,

        /// <summary>
        /// The service is running but is not healthy.
        /// </summary>
        [EnumMember(Value = "unhealthy")]
        Unhealthy,

        /// <summary>
        /// The service has terminated.
        /// </summary>
        [EnumMember(Value = "terminated")]
        Terminated
    }
}
