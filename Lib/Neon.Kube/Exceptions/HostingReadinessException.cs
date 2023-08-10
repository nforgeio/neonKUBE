// -----------------------------------------------------------------------------
// FILE:	    HostingReadinessException.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.ClusterDef;
using Neon.Kube.Hosting;
using Neon.Kube.Setup;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// Thrown when a hosting manager's <see cref="IHostingManager.CheckDeploymentReadinessAsync(ClusterDefinition)"/>
    /// method detects a problem.  Examine the <see cref="Readiness"/> property for information about the problems.
    /// </summary>
    public class HostingReadinessException : NeonKubeException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">Specifies the exception message.</param>
        /// <param name="readiness">Specifies the readiness problems.</param>
        public HostingReadinessException(string message, HostingReadiness readiness)
            : base(message)
        {
            Covenant.Requires<ArgumentNullException>(readiness != null, nameof(readiness));

            this.Readiness = readiness;
        }

        /// <summary>
        /// Returns information about the readiness problems.
        /// </summary>
        public HostingReadiness Readiness { get; private set; }
    }
}
