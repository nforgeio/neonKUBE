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
using System.Resources;

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
        /// Default constructor.
        /// </summary>
        public HostingOptions()
        {
        }

        /// <summary>
        /// Identifies the cloud or other hosting platform.  This defaults to <see cref="HostingEnvironment.Machine"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Environment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "environment", ApplyNamingConventions = false)]
        [DefaultValue(HostingEnvironment.Machine)]
        public HostingEnvironment Environment { get; set; } = HostingEnvironment.Machine;

        /// <summary>
        /// Specifies the Amazon Web Services hosting settings.
        /// </summary>
        [JsonProperty(PropertyName = "Aws", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "aws", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public AwsHostingOptions Aws { get; set; } = null;

        /// <summary>
        /// Specifies the Microsoft Azure hosting settings.
        /// </summary>
        [JsonProperty(PropertyName = "Azure", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "azure", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public AzureHostingOptions Azure { get; set; } = null;

        /// <summary>
        /// Specifies the Google Cloud Platform hosting settings.
        /// </summary>
        [JsonProperty(PropertyName = "Google", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "google", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public GoogleHostingOptions Google { get; set; } = null;

        /// <summary>
        /// Specifies the Hyper-V settings when hosting on remote Hyper-V servers.  
        /// This is typically used for production.
        /// </summary>
        [JsonProperty(PropertyName = "HyperV", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hyperV", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public HyperVHostingOptions HyperV { get; set; } = null;

        /// <summary>
        /// Specifies the Hyper-V settings when hosting on the local workstation using the 
        /// Microsoft Hyper-V hypervisor.  This is typically used for development or
        /// test purposes.
        /// </summary>
        [JsonProperty(PropertyName = "HyperVDev", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hyperVDev", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public LocalHyperVHostingOptions HyperVDev { get; set; } = null;

        /// <summary>
        /// Specifies the hosting settings when hosting directly on bare metal or virtual machines.
        /// </summary>
        [JsonProperty(PropertyName = "Machine", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "machine", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public MachineHostingOptions Machine { get; set; } = null;

        /// <summary>
        /// Specifies cloud related options for clusters to be deployed to one of the public cloud providers.
        /// </summary>
        [JsonProperty(PropertyName = "Cloud", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "cloud", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public CloudOptions Cloud { get; set; } = new CloudOptions();

        /// <summary>
        /// Specifies the hosting settings when hosting on Citrix XenServer or the XCP-ng hypervisors.
        /// </summary>
        [JsonProperty(PropertyName = "XenServer", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "xenServer", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public XenServerHostingOptions XenServer { get; set; } = null;

        /// <summary>
        /// Specifies common hosting settings for hypervisor based environments such as Hyper-V and XenServer.
        /// </summary>
        [JsonProperty(PropertyName = "VM", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vm", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public VmHostingOptions Vm { get; set; } = null;

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
                    case HostingEnvironment.HyperV:
                    case HostingEnvironment.HyperVLocal:
                    case HostingEnvironment.Machine:
                    case HostingEnvironment.XenServer:

                        return false;

                    case HostingEnvironment.Aws:
                    case HostingEnvironment.Azure:
                    case HostingEnvironment.Google:

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
                    case HostingEnvironment.HyperV:
                    case HostingEnvironment.XenServer:

                        return true;

                    case HostingEnvironment.Aws:
                    case HostingEnvironment.Azure:
                    case HostingEnvironment.Google:
                    case HostingEnvironment.HyperVLocal:
                    case HostingEnvironment.Machine:

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
                case HostingEnvironment.Aws:

                    if (Aws == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(Aws)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Aws.Validate(clusterDefinition);

                    Cloud = Cloud ?? new CloudOptions();
                    Cloud.Validate(clusterDefinition);
                    break;

                case HostingEnvironment.Azure:

                    if (Azure == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(Azure)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Azure.Validate(clusterDefinition);

                    Cloud = Cloud ?? new CloudOptions();
                    Cloud.Validate(clusterDefinition);
                    break;

                case HostingEnvironment.Google:

                    if (Google == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(Google)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Google.Validate(clusterDefinition);

                    Cloud = Cloud ?? new CloudOptions();
                    Cloud.Validate(clusterDefinition);
                    break;

                case HostingEnvironment.HyperV:

                    HyperV = HyperV ?? new HyperVHostingOptions();
                    HyperV.Validate(clusterDefinition);

                    Cloud = Cloud ?? new CloudOptions();
                    Cloud.Validate(clusterDefinition);

                    Vm = Vm ?? new VmHostingOptions();
                    Vm.Validate(clusterDefinition);
                    break;

                case HostingEnvironment.HyperVLocal:

                    HyperVDev = HyperVDev ?? new LocalHyperVHostingOptions();
                    HyperVDev.Validate(clusterDefinition);

                    Vm = Vm ?? new VmHostingOptions();
                    Vm.Validate(clusterDefinition);
                    break;

                case HostingEnvironment.Machine:

                    Machine = Machine ?? new MachineHostingOptions();
                    Machine.Validate(clusterDefinition);
                    break;

                case HostingEnvironment.XenServer:

                    XenServer = XenServer ?? new XenServerHostingOptions();
                    XenServer.Validate(clusterDefinition);

                    Cloud = Cloud ?? new CloudOptions();
                    Cloud.Validate(clusterDefinition);

                    Vm = Vm ?? new VmHostingOptions();
                    Vm.Validate(clusterDefinition);
                    break;

                default:

                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Clears all hosting provider details because they may
        /// include hosting related secrets.
        /// </summary>
        public void ClearSecrets()
        {
            Aws       = null;
            Azure     = null;
            Google    = null;
            HyperV    = null;
            HyperVDev = null;
            Machine   = null;
            Vm        = null;
            XenServer = null;
        }
    }
}
