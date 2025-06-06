//-----------------------------------------------------------------------------
// FILE:        HypervisorHostingOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using Neon.Kube.Config;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Specifies common options for on-premise hypervisor based hosting environments such as
    /// Hyper-V and XenServer.
    /// </summary>
    public class HypervisorHostingOptions
    {
        //---------------------------------------------------------------------
        // Static members

        internal const string DefaultMemory       = "16 GiB";
        internal const string DefaultBootDiskSize = "128 GiB";

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public HypervisorHostingOptions()
        {
        }

        /// <summary>
        /// Specifies one or more target XenServer hypervisor servers for XenServer cluster deployments.
        /// Cluster nodes will reference these hosts by name indicating that the node should be deployed
        /// on the target hyoervisor.  This is required for XenServer deployments.
        /// </summary>
        [JsonProperty(PropertyName = "Hosts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hosts", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<HypervisorHost> Hosts { get; set; } = new List<HypervisorHost>();

        /// <summary>
        /// <para>
        /// Optionally specifies the default username to use for connecting to hypervisor host machines specified by <see cref="Hosts"/>.
        /// This may be overridden for specific hypervisor machines.  This defaults to <c>null</c>.
        /// </para>
        /// <note>
        /// This defaults to <b>root</b> for XenServer based environments, <c>null</c> otherwise.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "HostUsername", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hostUsername", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string HostUsername { get; set; }

        /// <summary>
        /// Optionally specifies the default password to use for connecting to hypervisor host machines specified by <see cref="Hosts"/>.
        /// This may be overridden for specific hypervisor machines within <see cref="Hosts"/> items.  This defaults to <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "HostPassword", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hostPassword", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string HostPassword { get; set; }

        /// <summary>
        /// Specifies default number of VCPUs to assign to each cluster node virtual machine.  This defaults to <b>4</b>.
        /// </summary>
        [JsonProperty(PropertyName = "VCpus", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vcpus", ApplyNamingConventions = false)]
        [DefaultValue(4)]
        public int VCpus { get; set; } = 4;

        /// <summary>
        /// <para>
        /// Specifies the default amount of memory to allocate to each cluster virtual machine.  This is specified as a string
        /// that can be a byte count or a number with units like <b>512MiB</b>, <b>0.5GiB</b>, <b>2iGB</b>, or <b>1TiB</b>.  
        /// This defaults to <b>4GiB</b>.
        /// </para>
        /// <note>
        /// NeonKUBE requires that each control-plane and worker node have at least 4GiB of RAM.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Memory", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "memory", ApplyNamingConventions = false)]
        [DefaultValue(DefaultMemory)]
        public string Memory { get; set; } = DefaultMemory;

        /// <summary>
        /// Specifies the default size of the boot disk for cluster virtual machines.  This is specified as a string
        /// that can be a long byte count or a byte count or a number with units like <b>512MiB</b>, <b>0.5GiB</b>, <b>2GiB</b>, 
        /// or <b>1TiB</b>.  This defaults to <b>128GiB</b> and can be overriden for specific nodes via <see cref="HypervisorNodeOptions.BootDiskSize"/>.
        /// </summary>
        [JsonProperty(PropertyName = "BootDiskSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "bootDiskSize", ApplyNamingConventions = false)]
        [DefaultValue(DefaultBootDiskSize)]
        public string BootDiskSize { get; set; } = DefaultBootDiskSize;

        /// <summary>
        /// Optionally specifies the size for cluster node secondary data disks used for OpenEBS
        /// Mayastor storage. This defaults to <b>10 GiB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "MayastorDiskSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "mayastorDiskSize", ApplyNamingConventions = false)]
        [DefaultValue(KubeConst.DefaultMayastorDiskSize)]
        public string MayastorDiskSize { get; set; } = KubeConst.DefaultMayastorDiskSize;

        /// <summary>
        /// <para>
        /// Specifies the path to the location where virtual machine hard disk will be created.
        /// This defaults to the local Hyper-V folder for Windows.
        /// </para>
        /// <note>
        /// <para>
        /// This is currently recognized only when deploying on a local Hyper-V hypervisor.
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
        /// on-premise hosting environments like <see cref="HostingEnvironment.HyperV"/>
        /// and <see cref="HostingEnvironment.XenServer"/>.  This is used to avoid VM naming
        /// conflicts between different clusters running on the same host.
        /// </para>
        /// <para>
        /// When this is <c>null</c> (the default) or the empty string, the cluster name followed
        /// by a dash will prefix the provisioned virtual machine names.  When set to <b>"[none]"</b>,
        /// the VM named won't be prefixed, otherwise <see cref="NamePrefix"/> followed by a dash
        /// will be used as the VM prefix.
        /// </para>
        /// <note>
        /// Virtual machine name prefixes will always be converted to lowercase.
        /// </note>
        /// <note>
        /// This property is ignored for cloud hosting environments because cluster VMs will be
        /// isolated in their own resource groups.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "NamePrefix", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "namePrefix", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string NamePrefix { get; set; } = null;

        /// <summary>
        /// Returns the prefix to be used when provisioning virtual machines in hypervisor environments
        /// during cluster deployment.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        /// <returns>The prefix.</returns>
        public string GetVmNamePrefix(ClusterDefinition clusterDefinition)
        {
            // We don't add a prefix for the special NeonDESKTOP cluster.

            if (clusterDefinition.IsDesktop)
            {
                return String.Empty;
            }

            string prefix;

            if (string.IsNullOrEmpty(NamePrefix))
            {
                prefix = $"{clusterDefinition.Name}-".ToLowerInvariant();
            }
            else if (NamePrefix.Trim() == "[none]")
            {
                prefix = string.Empty;
            }
            else
            {
                prefix = $"{NamePrefix}-".ToLowerInvariant();
            }

            return prefix;
        }

        /// <summary>
        /// Returns the prefix to be used when provisioning virtual machines in hypervisor environments.
        /// </summary>
        /// <param name="configCluster">The cluster configuration (from kubeconfig).</param>
        /// <returns>The prefix.</returns>
        public string GetVmNamePrefix(KubeConfigCluster configCluster)
        {
            // We don't add a prefix for non-NeonKUBE clusters or the special NeonDESKTOP cluster.

            if (!configCluster.IsNeonKube || configCluster.IsNeonDesktop)
            {
                return String.Empty;
            }

            string prefix;

            if (string.IsNullOrEmpty(NamePrefix))
            {
                prefix = $"{configCluster.ClusterInfo.ClusterName}-".ToLowerInvariant();
            }
            else if (NamePrefix == "[none]")
            {
                prefix = string.Empty;
            }
            else
            {
                prefix = $"{NamePrefix}-".ToLowerInvariant();
            }

            return prefix;
        }

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values, as required.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var optionsPrefix = $"{nameof(ClusterDefinition.Hosting)}";

            // Validate the VM name prefix.

            if (!string.IsNullOrWhiteSpace(NamePrefix) && NamePrefix != "[none]")
            {
                if (!ClusterDefinition.IsValidName(NamePrefix))
                {
                    throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(NamePrefix)}={NamePrefix}] must include only letters, digits, underscores, or periods.");
                }
            }

            // Check the default number of cores.

            if (VCpus <= 0)
            {
                throw new ClusterDefinitionException($"[{nameof(HyperVHostingOptions)}.{nameof(VCpus)}={VCpus}] must be positive.");
            }

            // Check memory and disk sizes.

            Memory       ??= DefaultMemory;
            BootDiskSize ??= DefaultBootDiskSize;

            ClusterDefinition.ValidateSize(Memory, this.GetType(), $"{optionsPrefix}.{nameof(Memory)}");
            ClusterDefinition.ValidateSize(BootDiskSize, this.GetType(), $"{optionsPrefix}.{nameof(BootDiskSize)}");

            // Verify [MayastorDiskSize].

            MayastorDiskSize ??= KubeConst.DefaultMayastorDiskSize;

            if (!ByteUnits.TryParse(MayastorDiskSize, out var mayastorDiskSize) || mayastorDiskSize <= KubeConst.MinMayastorDiskSizeGib)
            {
                throw new ClusterDefinitionException($"[{optionsPrefix}.{nameof(MayastorDiskSize)}={MayastorDiskSize}] must be >= {KubeConst.MinMayastorDiskSizeGib} GiB.");
            }

            // Verify that the hypervisor host machines have unique names and addresses.

            var hostNameSet    = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var hostAddressSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            Hosts  = Hosts ?? new List<HypervisorHost>();

            foreach (var vmHost in clusterDefinition.Hosting.Hypervisor.Hosts)
            {
                if (hostNameSet.Contains(vmHost.Name))
                {
                    throw new ClusterDefinitionException($"Multiple hypervisor hosts are assigned the [{vmHost.Name}] name.");
                }

                hostNameSet.Add(vmHost.Name);

                if (hostAddressSet.Contains(vmHost.Address))
                {
                    throw new ClusterDefinitionException($"Multiple hypervisor hosts are assigned the [{vmHost.Address}] address.");
                }

                hostAddressSet.Add(vmHost.Address);
            }

            // Ensure that at least one hypervisor hosts have been specified if we're deploying
            // to remote hypervisors and that this host references are valid.

            if (clusterDefinition.Hosting.IsHostedHypervisor)
            {
                if (Hosts.Count == 0)
                {
                    
                    throw new ClusterDefinitionException($"At least one hypervisor host must be specified fpr the[{ clusterDefinition.Hosting.Environment }] environment.");
                }

                foreach (var vmHost in Hosts)
                {
                    vmHost.Validate(clusterDefinition);
                }
            }
        }

        /// <summary>
        /// Clears all hosting related secrets.
        /// </summary>
        public void ClearSecrets()
        {
            HostUsername = null;
            HostPassword = null;
        }
    }
}
