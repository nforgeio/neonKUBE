//-----------------------------------------------------------------------------
// FILE:	    VmNodeOptions.cs
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
    /// Specifies common node options for on-premise hypervisor based hosting environments such as
    /// Hyper-V and XenServer.
    /// </summary>
    public class VmNodeOptions
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public VmNodeOptions()
        {
        }

        /// <summary>
        /// Identifies the hypervisor instance where this node is to be provisioned for Hyper-V
        /// or XenServer based clusters.  This name must map to the name of one of the <see cref="VmHostingOptions.Hosts"/>
        /// when set.
        /// </summary>
        [JsonProperty(PropertyName = "Host", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "host", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Host { get; set; } = null;

        /// <summary>
        /// <para>
        /// Specifies the number of processors to assigned to this node when provisioned on a hypervisor.  This
        /// defaults to the value specified by <see cref="VmHostingOptions.Cores"/>.
        /// </para>
        /// <note>
        /// neonKUBE requires that each master and worker node have at least 4 CPUs.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Cores", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "cores", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int Cores { get; set; } = 0;

        /// <summary>
        /// <para>
        /// Specifies the amount of memory to allocate to this node when provisioned on a hypervisor.  
        /// This is specified as a string that can be a byte count or a number with units like <b>512MB</b>, 
        /// <b>0.5GB</b>, <b>2GB</b>, or <b>1TB</b>.  This defaults to the value specified by 
        /// <see cref="VmHostingOptions.Memory"/>.
        /// </para>
        /// <note>
        /// neonKUBE requires that each master and worker node have at least 4GiB of RAM.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Memory", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "memory", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Memory { get; set; } = null;

        /// <summary>
        /// The size of operating system disk for this node when when provisioned on a hypervisor.  This is specified 
        /// as a string that can be a byte count or a number with units like <b>512MB</b>, <b>0.5GB</b>, <b>2GB</b>, or <b>1TB</b>.  This 
        /// defaults to the value specified by <see cref="VmHostingOptions.OsDisk"/>.
        /// </summary>
        [JsonProperty(PropertyName = "OsDisk", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "osDisk", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string OsDisk { get; set; } = null;

        /// <summary>
        /// Specifies the size of the second block device to be created for this node when it is
        /// enabled for OpenEBS.  This is specified as a string that can be a byte count or a number with 
        /// units like <b>512MiB</b>, <b>0.5GiB</b>, <b>2iGB</b>, or <b>1TiB</b>.  This defaults
        /// to the value specified by <see cref="VmHostingOptions.OpenEbsDisk"/>.
        /// </summary>
        [JsonProperty(PropertyName = "OpenEbsDisk", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "openEbsDisk", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string OpenEbsDisk { get; set; } = null;

        /// <summary>
        /// Returns the maximum number CPU cores to allocate for this node when
        /// hosted on a hypervisor.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The number of cores.</returns>
        public int GetCores(ClusterDefinition clusterDefinition)
        {
            if (Cores != 0)
            {
                return Cores;
            }
            else
            {
                return clusterDefinition.Hosting.Vm.Cores;
            }
        }

        /// <summary>
        /// Returns the maximum number of bytes of memory allocate to for this node when
        /// hosted on a hypervisor.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The size in bytes.</returns>
        public long GetMemory(ClusterDefinition clusterDefinition)
        {
            if (!string.IsNullOrEmpty(Memory))
            {
                return ClusterDefinition.ValidateSize(Memory, this.GetType(), nameof(Memory));
            }
            else
            {
                return ClusterDefinition.ValidateSize(clusterDefinition.Hosting.Vm.Memory, clusterDefinition.Hosting.GetType(), nameof(clusterDefinition.Hosting.Vm.Memory));
            }
        }

        /// <summary>
        /// Returns the size of the operating system disk to be created for this node when
        /// hosted on a hypervisor.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The size in bytes.</returns>
        public long GetOsDisk(ClusterDefinition clusterDefinition)
        {
            if (!string.IsNullOrEmpty(OsDisk))
            {
                return ClusterDefinition.ValidateSize(OsDisk, this.GetType(), nameof(OsDisk));
            }
            else
            {
                return ClusterDefinition.ValidateSize(clusterDefinition.Hosting.Vm.OsDisk, clusterDefinition.Hosting.GetType(), nameof(clusterDefinition.Hosting.Vm.OsDisk));
            }
        }

        /// <summary>
        /// Returns the size of the OpenEBS cStor disk to be created for this node when
        /// hosted on a hypervisor.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The size in bytes.</returns>
        public long GetOpenEbsDisk(ClusterDefinition clusterDefinition)
        {
            const string minOpenEbsSize = "32 GiB";

            switch (clusterDefinition.Storage.OpenEbs.Engine)
            {
                case OpenEbsEngine.cStor:
                case OpenEbsEngine.Mayastor:

                if (!string.IsNullOrEmpty(OpenEbsDisk))
                {
                    return ClusterDefinition.ValidateSize(OpenEbsDisk, this.GetType(), nameof(OpenEbsDisk), minimum: minOpenEbsSize);
                }
                else
                {
                    return ClusterDefinition.ValidateSize(clusterDefinition.Hosting.Vm.OpenEbsDisk, clusterDefinition.Hosting.GetType(), nameof(clusterDefinition.Hosting.Vm.OpenEbsDisk), minimum: minOpenEbsSize);
                }

                default:

                    return 0;
            }
        }

        /// <summary>
        /// Validates the node definition.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="nodeName">The node name.</param>
        /// <exception cref="ArgumentException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition, string nodeName)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var nodeDefinitionPrefix = $"{nameof(ClusterDefinition.NodeDefinitions)}";

            if (clusterDefinition.Hosting.IsHostedHypervisor)
            {
                if (string.IsNullOrEmpty(Host))
                {
                    throw new ClusterDefinitionException($"Node [{nodeName}] does not specify a hypervisor [{nodeDefinitionPrefix}.{nameof(NodeDefinition.Vm.Host)}].");
                }
                else if (clusterDefinition.Hosting.Vm.Hosts.FirstOrDefault(h => h.Name.Equals(Host, StringComparison.InvariantCultureIgnoreCase)) == null)
                {
                    throw new ClusterDefinitionException($"Node [{nodeName}] references hypervisor [{Host}] which is not defined in [{nameof(ClusterDefinition.Hosting)}.{nameof(ClusterDefinition.Hosting.Vm)}.{nameof(ClusterDefinition.Hosting.Vm.Hosts)}].");
                }
            }

            if (Memory != null)
            {
                ClusterDefinition.ValidateSize(Memory, this.GetType(), $"{nodeDefinitionPrefix}.{nameof(Memory)}");
            }

            if (OsDisk != null)
            {
                ClusterDefinition.ValidateSize(OsDisk, this.GetType(), $"{nodeDefinitionPrefix}.{nameof(OsDisk)}");
            }

            if (OpenEbsDisk != null)
            {
                ClusterDefinition.ValidateSize(OpenEbsDisk, this.GetType(), $"{nodeDefinitionPrefix}.{nameof(OpenEbsDisk)}");
            }
        }
    }
}
