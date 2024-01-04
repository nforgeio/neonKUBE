// -----------------------------------------------------------------------------
// FILE:	    HostingReadinessProblem.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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

namespace Neon.Kube.Hosting
{
    /// <summary>
    /// Details a problem reported by a hosting manager preventing a
    /// cluster deployment.
    /// </summary>
    public class HostingReadinessProblem
    {
        /// <summary>
        /// Identifies AWS related problems.
        /// </summary>
        public const string AwsType = "aws";

        /// <summary>
        /// Identifies Azure related problems.
        /// </summary>
        public const string AzureType = "azure";

        /// <summary>
        /// Identifies cluster definition related problems.
        /// </summary>
        public const string ClusterDefinitionType = "cluster-def";

        /// <summary>
        /// Identifies Hyper-V related problems.
        /// </summary>
        public const string HyperVType = "hyper-v";

        /// <summary>
        /// Identifies XenServer related problems.
        /// </summary>
        public const string XenServerType = "xenserver";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="type">Identifies the problem type.</param>
        /// <param name="details">Specifies the problem details.</param>
        public HostingReadinessProblem(string type, string details)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(type), nameof(type));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(details), nameof(details));

            this.Type    = type;
            this.Details = details;
        }

        /// <summary>
        /// Returns the problem type.
        /// </summary>
        public string Type { get; private set; }

        /// <summary>
        /// Returns the problem details.
        /// </summary>
        public string Details { get; private set; }
    }
}
