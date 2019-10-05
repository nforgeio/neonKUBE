//-----------------------------------------------------------------------------
// FILE:	    LocalHyperVOptions.cs
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

using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Specifies hosting settings for the local Microsoft Hyper-V hypervisor.
    /// </summary>
    public class LocalHyperVOptions
    {
        private const string defaultHostVhdxUri = "https://s3-us-west-2.amazonaws.com/neonforge/kube/hyperv-ubuntu-18.04.latest.vhdx";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public LocalHyperVOptions()
        {
        }

        /// <summary>
        /// <para>
        /// URI to the zipped VHDX image with the base cluster host operating system.  This defaults to
        /// <b>https://s3-us-west-2.amazonaws.com/neonforge/neoncluster/neon-Ubuntu-18.04.latest.vhdx</b>
        /// which is the latest supported Ubuntu 16.04 image.
        /// </para>
        /// <note>
        /// Production cluster definitions should be configured with an VHDX with a specific version
        /// of the host operating system to ensure that cluster nodes are provisioned with the same
        /// operating system version.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "HostVhdxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hostVhdxUri", ApplyNamingConventions = false)]
        [DefaultValue(defaultHostVhdxUri)]
        public string HostVhdxUri { get; set; } = defaultHostVhdxUri;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            if (string.IsNullOrEmpty(HostVhdxUri) || !Uri.TryCreate(HostVhdxUri, UriKind.Absolute, out Uri uri))
            {
                throw new ClusterDefinitionException($"[{nameof(LocalHyperVOptions)}.{nameof(HostVhdxUri)}] is required when deploying to Hyper-V.");
            }

            clusterDefinition.ValidatePrivateNodeAddresses();                                           // Private node IP addresses must be assigned and valid.
            clusterDefinition.Hosting.ValidateHypervisor(clusterDefinition, remoteHypervisors: false);  // Hypervisor options must be valid.
        }
    }
}
