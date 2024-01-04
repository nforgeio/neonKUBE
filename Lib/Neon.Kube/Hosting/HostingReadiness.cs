// -----------------------------------------------------------------------------
// FILE:	    HostingReadiness.cs
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

using Neon.Kube;

namespace Neon.Kube.Hosting
{
    /// <summary>
    /// Describes a hosting manager's readiness to deploy a cluster.
    /// </summary>
    public class HostingReadiness
    {
        private List<HostingReadinessProblem>   problems = new List<HostingReadinessProblem>();

        /// <summary>
        /// Set to <c>true</c> when the hosting manager has verified that the
        /// environment is ready to deploy the cluster or <c>false</c> when there
        /// are one or more problems.  This is initialized to <c>true</c> by the
        /// constructor and will be set to <c>false</c> when problems are added.
        /// </summary>
        public bool IsReady { get; set; } = true;

        /// <summary>
        /// Adds a problem to the instance and also set <see cref="IsReady"/><c>=false</c>.
        /// </summary>
        /// <param name="type">Specifies the problem type.</param>
        /// <param name="details">Specifies the problem details.</param>
        public void AddProblem(string type, string details)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(type), nameof(type));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(details), nameof(details));

            IsReady = false;

            problems.Add(new HostingReadinessProblem(type, details));
        }

        /// <summary>
        /// Lists any detected problems.
        /// </summary>
        public IReadOnlyList<HostingReadinessProblem> Problems => problems;

        /// <summary>
        /// Throws a <see cref="HostingReadinessException"/> including this instance
        /// when the hosting manager isn't ready.
        /// </summary>
        public void ThrowIfNotReady()
        {
            if (!IsReady)
            {
                throw new HostingReadinessException(ToString(), this);
            }
        }

        /// <summary>
        /// Renders any problems into a string suitable for use as an exception message.
        /// </summary>
        /// <returns>The formatted string.</returns>
        public override string ToString()
        {
            if (problems.Count == 0)
            {
                return "Hosting is ready";
            }

            var sb = new StringBuilder();

            sb.AppendLine($"Hosting is not ready due to [{problems.Count}] problems:");

            foreach (var problem in problems)
            {
                sb.AppendLine($"{problem.Type}: {problem.Details}");
            }

            return sb.ToString();
        }
    }
}
