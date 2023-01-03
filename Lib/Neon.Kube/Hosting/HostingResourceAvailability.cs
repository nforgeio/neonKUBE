//-----------------------------------------------------------------------------
// FILE:	    HostingResourceAvailability.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Collections;
using Neon.Common;
using Neon.Net;
using Neon.XenServer;
using Neon.IO;
using Neon.SSH;

using k8s.Models;

namespace Neon.Kube.Hosting
{
    /// <summary>
    /// Returned by <see cref="IHostingManager.GetResourceAvailabilityAsync(long, long)"/> indicating whether a hosting
    /// environment has sufficient resources available to deploy a cluster.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="CanBeDeployed"/> property returns <c>true</c> when the environment has sufficent
    /// resources to deploy the cluster.
    /// </para>
    /// </remarks>
    public class HostingResourceAvailability
    {
        /// <summary>
        /// Returns <c>true</c> when the cluster can be deployed.
        /// </summary>
        [JsonProperty(PropertyName = "CanBeDeployed", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "canBeDeployed", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool CanBeDeployed
        {
            get
            {
                if (Constraints == null)
                {
                    return false;
                }

                return !Constraints.Any(constraint => constraint.Value.Count > 0);
            }
        }

        /// <summary>
        /// Details the constraints preventing the cluster from being deployed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is a dictionary mapping hosting environment entity names to one or more resource constraints
        /// preventing cluster deployment.  The entity names are hosting environment specific and will refer
        /// to things like virtualization hosts for environments like Hyper-V and XenServer, server pools for
        /// bare metal, or datacenters for clouds like AWS, Azure, and Google.
        /// </para>
        /// <para>
        /// Each constraint includes information about the constrained resource as well as the cluster nodes
        /// that exceeded the constraint.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "Constraints", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "constraints", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Dictionary<string, List<HostingResourceConstraint>> Constraints { get; set; } = new Dictionary<string, List<HostingResourceConstraint>>(StringComparer.InvariantCultureIgnoreCase);
    }
}
