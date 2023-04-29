// -----------------------------------------------------------------------------
// FILE:	    HostingDeployment.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Kube.ClusterDef;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube.Deployment
{
    /// <summary>
    /// Holds information about the environment hosting the cluster.
    /// </summary>
    public class HostingDeployment
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public HostingDeployment()
        {
        }

        /// <summary>
        /// Constructs an instance by extracting values from a <see cref="ClusterDefinition"/>.
        /// </summary>
        /// <param name="clusterDefinition">Specifies thje cluster definition.</param>
        public HostingDeployment(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            this.Aws         = clusterDefinition.Hosting.Aws;
            this.Azure       = clusterDefinition.Hosting.Azure;
            this.BareMetal   = clusterDefinition.Hosting.BareMetal;
            this.Cloud       = clusterDefinition.Hosting.Cloud;
            this.Environment = clusterDefinition.Hosting.Environment;
            this.HyperV      = clusterDefinition.Hosting.HyperV;
            this.Hypervisor  = clusterDefinition.Hosting.Hypervisor;
            this.XenServer   = clusterDefinition.Hosting.XenServer;
        }

        /// <summary>
        /// Identifies the cloud or other hosting platform.  This defaults to <see cref="HostingEnvironment.Unknown"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Environment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "environment", ApplyNamingConventions = false)]
        [DefaultValue(HostingEnvironment.Unknown)]
        public HostingEnvironment Environment { get; set; } = HostingEnvironment.Unknown;

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
    }
}

