//-----------------------------------------------------------------------------
// FILE:	    HypervisorHost.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Kube
{
    /// <summary>
    /// Describes the location and credentials required to connect to
    /// a specific Hyper-V or XenServer hypervisor machine for cluster 
    /// provisioning.
    /// </summary>
    public class HypervisorHost
    {
        /// <summary>
        /// The XenServer hostname.  This is used to by <see cref="NodeDefinition"/> instances
        /// to specify where a cluster node is to be provisioned.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// The IP address or FQDN of the hypervisor machine.
        /// </summary>
        [JsonProperty(PropertyName = "Address", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "address", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Address { get; set; }

        /// <summary>
        /// The custom username to use when connecting to the hypervisor machine.  This
        /// overrides <see cref="VmHostingOptions.HostUsername"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Username", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "username", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Username { get; set; }

        /// <summary>
        /// The custom password to use when connecting to the hypervisor machine.  This
        /// overrides <see cref="VmHostingOptions.HostPassword"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Password", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "password", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Password { get; set; }

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw new ClusterDefinitionException($"[{nameof(HypervisorHost)}.{nameof(Name)}] is required when specifying a hypervisor host.");
            }

            if (string.IsNullOrEmpty(Address))
            {
                throw new ClusterDefinitionException($"[{nameof(HypervisorHost)}.{nameof(Address)}] is required when specifying a hypervisor host.");
            }

            if (string.IsNullOrEmpty(Username) && string.IsNullOrEmpty(clusterDefinition.Hosting.Vm.HostUsername))
            {
                throw new ClusterDefinitionException($"[{nameof(HypervisorHost)}.{nameof(Username)}] is required when specifying a hypervisor host and no default username is specified by [{nameof(HostingOptions)}.{nameof(HostingOptions.Vm.HostUsername)}].");
            }

            if (string.IsNullOrEmpty(Password) && string.IsNullOrEmpty(clusterDefinition.Hosting.Vm.HostPassword))
            {
                throw new ClusterDefinitionException($"[{nameof(HypervisorHost)}.{nameof(Password)}] is required when specifying a hypervisor host and no default password is specified by [{nameof(HostingOptions)}.{nameof(HostingOptions.Vm.HostPassword)}].");
            }
        }
    }
}
