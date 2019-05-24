//-----------------------------------------------------------------------------
// FILE:	    WorkflowResetPoint.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Tasks;

using Neon.Cadence.Internal;

namespace Neon.Cadence
{
    /// <summary>
    /// Not sure what is.
    /// </summary>
    public class WorkflowResetPoint
    {
        /// <summary>
        /// Not sure what is.
        /// </summary>
        public string BinaryChecksum { get; internal set; }

        /// <summary>
        /// Not sure what is.
        /// </summary>
        public string RunId { get; internal set; }

        /// <summary>
        /// Not sure what is.
        /// </summary>
        public long FirstDecisionCompletedId { get; internal set; }

        /// <summary>
        /// Not sure what is.
        /// </summary>
        public DateTime CreatedTime { get; internal set; }

        /// <summary>
        /// Not sure what is.
        /// </summary>
        public DateTime ExpiringTime { get; internal set; }

        /// <summary>
        /// Not sure what is.
        /// </summary>
        public bool Resettable { get; internal set; }
    }
}
