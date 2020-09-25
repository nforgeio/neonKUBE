//-----------------------------------------------------------------------------
// FILE:	    VmHostingOptions.cs
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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Specifies common options for on-premise hypervisor based hosting environments such as
    /// Hyper-V and XenServer.
    /// </summary>
    public class VmHostingOptions
    {
        //---------------------------------------------------------------------
        // Static members

        internal const string DefaultMemory      = "4 GiB";
        internal const string DefaultOsDisk      = "128 GiB";
        internal const string DefaultOpenEbsDisk = "128 GiB";

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public VmHostingOptions()
        {
        }

        /// <summary>
        /// Optionally identifies the target Hyper-V or XenServer hypervisor machines.
        /// </summary>
        [JsonProperty(PropertyName = "Hosts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hosts", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<HypervisorHost> Hosts { get; set; } = new List<HypervisorHost>();

        /// <summary>
        /// <para>
        /// The default username to use for connecting the hypervisor host machines specified by <see cref="Hosts"/>.
        /// This may be overridden for specific hypervisor machines.  This defaults to <c>null</c>.
        /// </para>
        /// <note>
        /// This defaults to <b>root</b> for XenServer based environments.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "HostUsername", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hostUsername", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string HostUsername { get; set; }

        /// <summary>
        /// The default password to use for connecting the hypervisor host machines specified by <see cref="Hosts"/>.
        /// This may be overridden for specific hypervisor machines within <see cref="Hosts"/> items.  This defaults to <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "HostPassword", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hostPassword", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string HostPassword { get; set; }

        /// <summary>
        /// The default number of virtual processors to assign to each cluster virtual machine.
        /// </summary>
        [JsonProperty(PropertyName = "Processors", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "processors", ApplyNamingConventions = false)]
        [DefaultValue(4)]
        public int Processors { get; set; } = 4;

        /// <summary>
        /// Specifies the default amount of memory to allocate to each cluster virtual machine.  This is specified as a string
        /// that can be a byte count or a number with units like <b>512MiB</b>, <b>0.5GiB</b>, <b>2iGB</b>, or <b>1TiB</b>.  
        /// This defaults to <b>4GiB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Memory", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "memory", ApplyNamingConventions = false)]
        [DefaultValue(DefaultMemory)]
        public string Memory { get; set; } = DefaultMemory;

        /// <summary>
        /// Specifies the default size of the operating system disk for cluster virtual machines.  This is specified as a string
        /// that can be a long byte count or a byte count or a number with units like <b>512MiB</b>, <b>0.5GiB</b>, <b>2GiB</b>, 
        /// or <b>1TiB</b>.  This defaults to <b>128GiB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "OsDisk", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "osDisk", ApplyNamingConventions = false)]
        [DefaultValue(DefaultOsDisk)]
        public string OsDisk { get; set; } = DefaultOsDisk;

        /// <summary>
        /// Specifies the default size of the second block device to be created for nodes enabled for
        /// OpenEBS.  This is specified as a string that can be a byte count or a number with 
        /// units like <b>512MiB</b>, <b>0.5GiB</b>, <b>2iGB</b>, or <b>1TiB</b>.  This defaults
        /// to <b>128GiB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "OpenEbsDisk", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "openEbsDisk", ApplyNamingConventions = false)]
        [DefaultValue(DefaultOpenEbsDisk)]
        public string OpenEbsDisk { get; set; } = DefaultOpenEbsDisk;

        /// <summary>
        /// <para>
        /// Path to the location where virtual machine hard disk will be created.
        /// This defaults to the local Hyper-V folder for Windows.
        /// </para>
        /// <note>
        /// <para>
        /// This is currently recognized only when deploying on a local Hyper-V hypervisor.
        /// Eventually, you'll be able to specify a XenServer storage repository.
        /// </para>
        /// <para>
        /// <a href="https://github.com/nforgeio/neonKUBE/issues/996">Issue #996</a>
        /// </para>
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "DiskLocation", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "diskLocation", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string DiskLocation { get; set; } = null;

        /// <summary>
        /// <para>
        /// The prefix to be prepended to virtual machine provisioned to hypervisors for the
        /// <see cref="HostingEnvironment.HyperV"/>, <see cref="HostingEnvironment.HyperVLocal"/>,
        /// and <see cref="HostingEnvironment.XenServer"/> environments.
        /// </para>
        /// <para>
        /// When this is <c>null</c> (the default), the cluster name followed by a dash will 
        /// prefix the provisioned virtual machine names.  When this is a non-empty string, the
        /// value followed by a dash will be used.  If this is empty or whitespace, machine
        /// names will not be prefixed.
        /// </para>
        /// <note>
        /// Virtual machine name prefixes will always be converted to lowercase.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "NamePrefix", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "namePrefix", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string NamePrefix { get; set; }  = null;

        /// <summary>
        /// Returns the prefix to be used when provisioning virtual machines in hypervisor environments.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The prefix.</returns>
        public string GetVmNamePrefix(ClusterDefinition clusterDefinition)
        {
            if (NamePrefix == null)
            {
                return $"{clusterDefinition.Name}-".ToLowerInvariant();
            }
            else if (string.IsNullOrWhiteSpace(NamePrefix))
            {
                return string.Empty;
            }
            else
            {
                return $"{NamePrefix}-".ToLowerInvariant();
            }
        }

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

            // Validate the VM name prefix.

            if (!string.IsNullOrWhiteSpace(NamePrefix))
            {
                if (!ClusterDefinition.IsValidName(NamePrefix))
                {
                    throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(NamePrefix)}={NamePrefix}] must include only letters, digits, underscores, or periods.");
                }
            }

            if (Processors <= 0)
            {
                throw new ClusterDefinitionException($"[{nameof(LocalHyperVHostingOptions)}.{nameof(Processors)}={Processors}] must be positive.");
            }

            Memory = Memory ?? DefaultMemory;
            OsDisk = OsDisk ?? DefaultOsDisk;
            Hosts  = Hosts ?? new List<HypervisorHost>();

            ClusterDefinition.ValidateSize(Memory, this.GetType(), nameof(Memory));
            ClusterDefinition.ValidateSize(OsDisk, this.GetType(), nameof(OsDisk));
            ClusterDefinition.ValidateSize(OpenEbsDisk, this.GetType(), nameof(OpenEbsDisk));

            // Verify that the hypervisor host machines have unique names and addresses.

            var hostNameSet    = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var hostAddressSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var vmHost in clusterDefinition.Hosting.Vm.Hosts)
            {
                if (hostNameSet.Contains(vmHost.Name))
                {
                    throw new ClusterDefinitionException($"One or more hypervisor hosts are assigned the [{vmHost.Name}] name.");
                }

                hostNameSet.Add(vmHost.Name);

                if (hostAddressSet.Contains(vmHost.Address))
                {
                    throw new ClusterDefinitionException($"One or more hypervisor hosts are assigned the [{vmHost.Address}] address.");
                }

                hostAddressSet.Add(vmHost.Address);
            }

            // Ensure that some hypervisor hosts have been specified if we're deploying to remote
            // hypervisors and also that each node definition specifies a host hypervisor.

            if (clusterDefinition.Hosting.IsRemoteHypervisorProvider)
            {
                if (clusterDefinition.Hosting.Vm.Hosts.Count == 0)
                {
                    throw new ClusterDefinitionException($"At least one host XenServer must be specified in [{nameof(HostingOptions)}.{nameof(HostingOptions.Vm.Hosts)}].");
                }

                foreach (var vmHost in Hosts)
                {
                    vmHost.Validate(clusterDefinition);
                }

                foreach (var node in clusterDefinition.Nodes)
                {
                    if (string.IsNullOrEmpty(node.Vm?.Host))
                    {
                        throw new ClusterDefinitionException($"Node [{node.Name}] does not specify a host hypervisor with [{nameof(NodeDefinition.Vm.Host)}].");
                    }

                    if (!hostNameSet.Contains(node.Vm.Host))
                    {
                        throw new ClusterDefinitionException($"Node [{node.Name}] has [{nameof(HypervisorHost)}={node.Vm.Host}] which specifies a hypervisor host that was not found in [{nameof(HostingOptions)}.{nameof(HostingOptions.Vm.Hosts)}].");
                    }
                }
            }
        }
    }
}
