//-----------------------------------------------------------------------------
// FILE:        HostingOptions.cs
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
using Neon.Kube.ClusterDef;
using Neon.Net;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Specifies the cloud or co-location/on-premise hosting settings.
    /// </summary>
    public class HostingOptions
    {
        //---------------------------------------------------------------------
        // Static members

        internal const string DefaultVmMemory = "4 GiB";
        internal const string DefaultVmDisk   = "64 GiB";

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public HostingOptions()
        {
        }

        /// <summary>
        /// Identifies the cloud or other hosting platform.  This defaults to <see cref="HostingEnvironment.Unknown"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Environment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "environment", ApplyNamingConventions = false)]
        [DefaultValue(HostingEnvironment.Unknown)]
        public HostingEnvironment Environment { get; set; } = HostingEnvironment.Unknown;

        /// <summary>
        /// Specifies the Amazon Web Services (AWS) hosting settings.
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
        /// Specifies the Hyper-V settings.
        /// </summary>
        [JsonProperty(PropertyName = "Hyperv", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hyperv", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public HyperVHostingOptions HyperV { get; set; } = null;

        /// <summary>
        /// Specifies the hosting settings when hosting directly on bare metal or virtual machines.
        /// </summary>
        [JsonProperty(PropertyName = "BareMetal", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "bareMetal", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public BareMetalHostingOptions BareMetal { get; set; } = null;

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
        /// Specifies common hosting settings for on-premise hypervisor environments like Hyper-V and XenServer.
        /// </summary>
        [JsonProperty(PropertyName = "Hypervisor", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hypervisor", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public HypervisorHostingOptions Hypervisor { get; set; } = null;

        /// <summary>
        /// Returns <c>true</c> if the cluster will be hosted by a cloud provider like AWS, Azure or Google.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool IsCloudProvider => KubeHelper.IsCloudEnvironment(Environment);

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
        /// Returns <c>true</c> if the cluster will be hosted on non-cloud hypervisors 
        /// like XenServer or Hyper-V.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool IsHostedHypervisor
        {
            get
            {
                switch (Environment)
                {
                    case HostingEnvironment.XenServer:

                        return true;

                    case HostingEnvironment.HyperV:

                        // $todo(jefflill):
                        //
                        // We should return [true] here but it will take some additional
                        // changes to continue support for provisioning on the local workstation
                        // vs. a remote Hyper-V host.
                        //
                        // We'll revisit this when we implement remote Hyper-C support:
                        //
                        //      https://github.com/nforgeio/neonKUBE/issues/1447

                        return false;

                    case HostingEnvironment.Aws:
                    case HostingEnvironment.Azure:
                    case HostingEnvironment.BareMetal:
                    case HostingEnvironment.Google:

                        return false;

                    default:

                        throw new NotImplementedException("Unexpected hosting environment.");
                }
            }
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

            switch (Environment)
            {
                case HostingEnvironment.Aws:

                    if (Aws == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(ClusterDefinition.Hosting)}.{nameof(Aws)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Aws.Validate(clusterDefinition);

                    Cloud ??= new CloudOptions();
                    Cloud.Validate(clusterDefinition);
                    break;

                case HostingEnvironment.Azure:

                    if (Azure == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(ClusterDefinition.Hosting)}.{nameof(Azure)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Azure.Validate(clusterDefinition);

                    Cloud ??= new CloudOptions();
                    Cloud.Validate(clusterDefinition);
                    break;

                case HostingEnvironment.BareMetal:

                    BareMetal = BareMetal ?? new BareMetalHostingOptions();
                    BareMetal.Validate(clusterDefinition);
                    break;

                case HostingEnvironment.Google:

                    if (Google == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(ClusterDefinition.Hosting)}.{nameof(Google)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Google.Validate(clusterDefinition);

                    Cloud ??= new CloudOptions();
                    Cloud.Validate(clusterDefinition);
                    break;

                case HostingEnvironment.HyperV:

                    HyperV = HyperV ?? new HyperVHostingOptions();
                    HyperV.Validate(clusterDefinition);

                    Hypervisor ??= new HypervisorHostingOptions();
                    Hypervisor.Validate(clusterDefinition);
                    break;

                case HostingEnvironment.XenServer:

                    Hypervisor ??= new HypervisorHostingOptions();
                    Hypervisor.Validate(clusterDefinition);
                    break;

                default:

                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Clears all hosting related secrets.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        public void ClearSecrets(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            clusterDefinition.Hosting.Aws?.ClearSecrets();
            clusterDefinition.Hosting.Azure?.ClearSecrets();
            clusterDefinition.Hosting.Google?.ClearSecrets();
            clusterDefinition.Hosting.HyperV?.ClearSecrets();
            clusterDefinition.Hosting.BareMetal?.ClearSecrets();
            clusterDefinition.Hosting.Hypervisor?.ClearSecrets();
            clusterDefinition.Hosting.XenServer?.ClearSecrets();
        }
    }
}
