//-----------------------------------------------------------------------------
// FILE:	    SecurityOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
    /// Specifies cluster security options.
    /// </summary>
    public class SecurityOptions
    {
        private const int defaultPasswordLength = 20;

        /// <summary>
        /// cluster hosts are configured with a random root account password.
        /// This defaults to <b>20</b> characters.  The minumum length is <b>8</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PasswordLength", Required = Required.Default)]
        [YamlMember(Alias = "passwordLength", ApplyNamingConventions = false)]
        [DefaultValue(defaultPasswordLength)]
        public int PasswordLength { get; set; } = defaultPasswordLength;

        /// <summary>
        /// <para>
        /// Normally cluster nodes are configured such that the <b>sysadmin</b> user account 
        /// password is set to cryptographically random password with <see cref="PasswordLength"/>
        /// characters.  This happens during cluster provisioning.
        /// </para>
        /// <para>
        /// You can disable these secure passwords by setting <see cref="KeepNodePassword"/> to 
        /// <c>true</c>.  In general, this means that the <b>sysadmin</b> password will generally
        /// remain as the insecure <b>sysadmin0000</b> password.
        /// </para>
        /// <note>
        /// <b>WARNING:</b> Be very careful about enabling this for production clusters!
        /// </note>
        /// </summary>
        /// <remarks>
        /// <para>
        /// There are really only two scnerios where you'd want to enable this:
        /// </para>
        /// <list type="number">
        /// <item>
        /// <b>Cluster development and debugging:</b> Sometimes neonKUBE developers may wish to enable
        /// this while developing and debugging cluster deployment and other operations to make it easy
        /// to SSH into cluster nodes to poke around.
        /// </item>
        /// <item>
        /// <b>Bare metal (machine) deployments:</b> Cluster operators may wish to configure a secure
        /// <b>sysadmin</b> password when they manually configure the target node machines and/or 
        /// virtual machines.  Setting this to <c>true</c> will have the <see cref="HostingEnvironments.Machine"/>
        /// cluster provisioner to retain this secure password rather than setting a new one. 
        /// </item>
        /// </list>
        /// </remarks>
        [JsonProperty(PropertyName = "KeepNodePassword", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "keepNodePassword", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool KeepNodePassword { get; set; } = false;

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            if (PasswordLength < 8)
            {
                throw new ClusterDefinitionException($"[{nameof(NodeOptions)}.{nameof(PasswordLength)}={PasswordLength}] cannot be less than 8 characters.");
            }
        }
    }
}