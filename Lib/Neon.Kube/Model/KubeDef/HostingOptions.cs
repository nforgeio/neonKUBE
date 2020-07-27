//-----------------------------------------------------------------------------
// FILE:	    HostingOptions.cs
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
using System.Net.Sockets;
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
    /// Specifies the cloud or colocation/on-premise hosting settings.
    /// </summary>
    public class HostingOptions
    {
        //---------------------------------------------------------------------
        // Static members

        internal const string DefaultVmMemory = "4Gi";
        internal const string DefaultVmDisk   = "64Gi";

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor that initializes a <see cref="HostingEnvironments.Machine"/> provider.
        /// </summary>
        public HostingOptions()
        {
        }

        /// <summary>
        /// Identifies the cloud or other hosting platform.  This defaults to <see cref="HostingEnvironments.Machine"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Environment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "environment", ApplyNamingConventions = false)]
        [DefaultValue(HostingEnvironments.Machine)]
        public HostingEnvironments Environment { get; set; } = HostingEnvironments.Machine;

        /// <summary>
        /// Specifies the Amazon Web Services hosting settings.
        /// </summary>
        [JsonProperty(PropertyName = "Aws", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "aws", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public AwsOptions Aws { get; set; } = null;

        /// <summary>
        /// Specifies the Microsoft Azure hosting settings.
        /// </summary>
        [JsonProperty(PropertyName = "Azure", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "azure", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public AzureOptions Azure { get; set; } = null;

        /// <summary>
        /// Specifies the Google Cloud Platform hosting settings.
        /// </summary>
        [JsonProperty(PropertyName = "Google", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "google", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public GoogleOptions Google { get; set; } = null;

        /// <summary>
        /// Specifies the Hyper-V settings when hosting on remote Hyper-V servers.  
        /// This is typically used for production.
        /// </summary>
        [JsonProperty(PropertyName = "HyperV", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hyperV", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public HyperVOptions HyperV { get; set; } = null;

        /// <summary>
        /// Specifies the Hyper-V settings when hosting on the local workstation using the 
        /// Microsoft Hyper-V hypervisor.  This is typically used for development or
        /// test purposes.
        /// </summary>
        [JsonProperty(PropertyName = "HyperVDev", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hyperVDev", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public LocalHyperVOptions HyperVDev { get; set; } = null;

        /// <summary>
        /// Specifies the hosting settings when hosting directly on bare metal or virtual machines.
        /// </summary>
        [JsonProperty(PropertyName = "Machine", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "machine", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public MachineOptions Machine { get; set; } = null;

        /// <summary>
        /// Specifies the hosting settings when hosting on Citrix XenServer or the XCP-ng hypervisors.
        /// </summary>
        [JsonProperty(PropertyName = "XenServer", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "xenServer", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public XenServerOptions XenServer { get; set; } = null;

        /// <summary>
        /// Optionally identifies the target Hyper-V or XenServer hypervisor machines.
        /// </summary>
        [JsonProperty(PropertyName = "VmHosts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vmHosts", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<HypervisorHost> VmHosts { get; set; } = new List<HypervisorHost>();

        /// <summary>
        /// <para>
        /// The default username to use for connecting the hypervisor host machines specified by <see cref="VmHosts"/>.
        /// This may be overridden for specific hypervisor machines.  This defaults to <c>null</c>.
        /// </para>
        /// <note>
        /// This defaults to <b>root</b> for XenServer based environments.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "VmHostUsername", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vmHostUsername", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string VmHostUsername { get; set; }

        /// <summary>
        /// The default password to use for connecting the hypervisor host machines specified by <see cref="VmHosts"/>.
        /// This may be overridden for specific hypervisor machines within <see cref="VmHosts"/> items.  This defaults to <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "VmHostPassword", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vmHostPassword", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string VmHostPassword { get; set; }

        /// <summary>
        /// The default number of virtual processors to assign to each cluster virtual machine.
        /// </summary>
        [JsonProperty(PropertyName = "VmProcessors", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vmProcessors", ApplyNamingConventions = false)]
        [DefaultValue(4)]
        public int VmProcessors { get; set; } = 4;

        /// <summary>
        /// Specifies the default maximum amount of memory to allocate to each cluster virtual machine.  This is specified as a string
        /// that can be a byte count or a number with units like <b>512MiB</b>, <b>0.5GiB</b>, <b>2iGB</b>, or <b>1TiB</b>.  
        /// This defaults to <b>4GiB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "VmMemory", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vmMemory", ApplyNamingConventions = false)]
        [DefaultValue(DefaultVmMemory)]
        public string VmMemory { get; set; } = DefaultVmMemory;

        /// <summary>
        /// Specifies the maximum amount of memory to allocate to each cluster virtual machine.  This is specified as a string
        /// that can be a long byte count or a byte count or a number with units like <b>512MiB</b>, <b>0.5GiB</b>, <b>2GiB</b>, 
        /// or <b>1TiB</b>.  This defaults to <b>64GiB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "VmDisk", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vmDisk", ApplyNamingConventions = false)]
        [DefaultValue(DefaultVmDisk)]
        public string VmDisk { get; set; } = DefaultVmDisk;

        /// <summary>
        /// <para>
        /// Path to the folder where virtual machine hard drive folders are to be persisted.
        /// This defaults to the local Hyper-V folder for Windows.
        /// </para>
        /// <note>
        /// This is recognized only when deploying on a local Hyper-V hypervisor, typically
        /// for development and test purposes.  This is ignored when provisioning on remote
        /// Hyper-V instances or for cloud or bare machine environments.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "VmDriveFolder", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vmDriveFolder", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string VmDriveFolder { get; set; } = null;

        /// <summary>
        /// <para>
        /// The prefix to be prepended to virtual machine provisioned to hypervisors for the
        /// <see cref="HostingEnvironments.HyperV"/>, <see cref="HostingEnvironments.HyperVLocal"/>,
        /// and <see cref="HostingEnvironments.XenServer"/> environments.
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
        [JsonProperty(PropertyName = "VmNamePrefix", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vmNamePrefix", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string VmNamePrefix { get; set; }  = null;

        /// <summary>
        /// Returns the prefix to be used when provisioning virtual machines in hypervisor environments.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The prefix.</returns>
        public string GetVmNamePrefix(ClusterDefinition clusterDefinition)
        {
            if (VmNamePrefix == null)
            {
                return $"{clusterDefinition.Name}-".ToLowerInvariant();
            }
            else if (string.IsNullOrWhiteSpace(VmNamePrefix))
            {
                return string.Empty;
            }
            else
            {
                return $"{VmNamePrefix}-".ToLowerInvariant();
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the cluster will be hosted by a cloud provider like AWS, Azure or Google.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool IsCloudProvider
        {
            get
            {
                switch (Environment)
                {
                    case HostingEnvironments.HyperV:
                    case HostingEnvironments.HyperVLocal:
                    case HostingEnvironments.Machine:
                    case HostingEnvironments.XenServer:

                        return false;

                    case HostingEnvironments.Aws:
                    case HostingEnvironments.Azure:
                    case HostingEnvironments.Google:

                        return true;

                    default:

                        throw new NotImplementedException("Unexpected hosting environment.");
                }
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the cluster will be hosted by an on-premise (non-cloud) provider.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool IsOnPremiseProvider
        {
            get { return !IsCloudProvider; }
        }

        /// <summary>
        /// Returns <c>true</c> if the cluster will be hosted by a hypervisor provider
        /// that supports remote hosts.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool IsRemoteHypervisorProvider
        {
            get
            {
                switch (Environment)
                {
                    case HostingEnvironments.HyperV:
                    case HostingEnvironments.XenServer:

                        return true;

                    case HostingEnvironments.Aws:
                    case HostingEnvironments.Azure:
                    case HostingEnvironments.Google:
                    case HostingEnvironments.HyperVLocal:
                    case HostingEnvironments.Machine:

                        return false;

                    default:

                        throw new NotImplementedException("Unexpected hosting environment.");
                }
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

            switch (Environment)
            {
                case HostingEnvironments.Aws:

                    if (Aws == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(Aws)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Aws.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.Azure:

                    if (Azure == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(Azure)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Azure.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.Google:

                    if (Google == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(Google)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Google.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.HyperV:

                    HyperV = HyperV ?? new HyperVOptions();

                    HyperV.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.HyperVLocal:

                    HyperVDev = HyperVDev ?? new LocalHyperVOptions();

                    HyperVDev.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.Machine:

                    Machine = Machine ?? new MachineOptions();

                    Machine.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.XenServer:

                    XenServer = XenServer ?? new XenServerOptions();

                    XenServer.Validate(clusterDefinition);
                    break;

                default:

                    throw new NotImplementedException();
            }

            // Validate the VM name prefix.

            if (!string.IsNullOrWhiteSpace(VmNamePrefix))
            {
                if (!ClusterDefinition.IsValidName(VmNamePrefix))
                {
                    throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(VmNamePrefix)}={VmNamePrefix}] must include only letters, digits, underscores, or periods.");
                }
            }
        }

        /// <summary>
        /// Validates the Hypervisor related options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="remoteHypervisors">
        /// Indicates that we're going to be deploying to remote hypervisor
        /// host machines as opposed to the local workstation.
        /// </param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        internal void ValidateHypervisor(ClusterDefinition clusterDefinition, bool remoteHypervisors)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            if (VmProcessors <= 0)
            {
                throw new ClusterDefinitionException($"[{nameof(LocalHyperVOptions)}.{nameof(VmProcessors)}={VmProcessors}] must be positive.");
            }

            VmMemory = VmMemory ?? DefaultVmMemory;
            VmDisk   = VmDisk ?? DefaultVmDisk;
            VmHosts  = VmHosts ?? new List<HypervisorHost>();

            ClusterDefinition.ValidateSize(VmMemory, this.GetType(), nameof(VmMemory));
            ClusterDefinition.ValidateSize(VmDisk, this.GetType(), nameof(VmDisk));

            // Verify that the hypervisor host machines have unique names and addresses.

            var hostNameSet    = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var hostAddressSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var vmHost in clusterDefinition.Hosting.VmHosts)
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
            // hypervisors and also that each node definition specifies a host hyoervisor.

            if (remoteHypervisors)
            {
                if (clusterDefinition.Hosting.VmHosts.Count == 0)
                {
                    throw new ClusterDefinitionException($"At least one host XenServer must be specified in [{nameof(HostingOptions)}.{nameof(HostingOptions.VmHosts)}].");
                }

                foreach (var vmHost in VmHosts)
                {
                    vmHost.Validate(clusterDefinition);
                }

                foreach (var node in clusterDefinition.NodeDefinitions.Values)
                {
                    if (string.IsNullOrEmpty(node.VmHost))
                    {
                        throw new ClusterDefinitionException($"Node [{node.Name}] does not specify a host hypervisor with [{nameof(NodeDefinition.VmHost)}].");
                    }

                    if (!hostNameSet.Contains(node.VmHost))
                    {
                        throw new ClusterDefinitionException($"Node [{node.Name}] has [{nameof(HypervisorHost)}={node.VmHost}] which specifies a hypervisor host that was not found in [{nameof(HostingOptions)}.{nameof(HostingOptions.VmHosts)}].");
                    }
                }
            }
        }

        /// <summary>
        /// Clears all hosting provider details because they may
        /// include hosting related secrets.
        /// </summary>
        public void ClearSecrets()
        {
            Aws         = null;
            Azure       = null;
            Google      = null;
            HyperV      = null;
            HyperVDev = null;
            Machine     = null;
            XenServer   = null;
        }
    }
}
