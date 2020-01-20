//-----------------------------------------------------------------------------
// FILE:	    AzureOptions.cs
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
    /// Specifies the Microsoft Azure cluster hosting settings.
    /// </summary>
    public class AzureOptions
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public AzureOptions()
        {
        }

        /// <summary>
        /// Azure account subscription ID obtained from the Azure portal.
        /// </summary>
        [JsonProperty(PropertyName = "SubscriptionId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "subscriptionId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SubscriptionId { get; set; }

        /// <summary>
        /// Tenant ID generated when creating the neon tool's Azure service principal.
        /// </summary>
        [JsonProperty(PropertyName = "TenantId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "tenantId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string TenantId { get; set; }

        /// <summary>
        /// Application ID generated when creating the neon tool's Azure service principal. 
        /// </summary>
        [JsonProperty(PropertyName = "ApplicationId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "applicationId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ApplicationId { get; set; }

        /// <summary>
        /// Password generated when creating the neon tool's Azure service principal.
        /// </summary>
        [JsonProperty(PropertyName = "Password", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "password", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Password { get; set; }

        /// <summary>
        /// Azure resource group where all clusterv components are to be provisioned.  This defaults
        /// to the clusterv name but can be customized as required.
        /// </summary>
        [JsonProperty(PropertyName = "ResourceGroup", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "resourceGroup", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ResourceGroup { get; set; }

        /// <summary>
        /// Identifies the target Azure region (e.g. <b>westus</b>).
        /// </summary>
        [JsonProperty(PropertyName = "Region", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "region", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Region { get; set; }

        /// <summary>
        /// The DNS domain prefix for the public IP address to be assigned to the cluster.
        /// </summary>
        /// <remarks>
        /// <note>
        /// <b>Recomendation:</b> To ensure that there's no conflicts with other 
        /// services deployed to Azure by you or other companies, we recommend that
        /// you generate a GUID and assign it to this property.
        /// </note>
        /// <para>
        /// This must be unique across all services deployed to an Azure region (your
        /// services as well as any other Azure cluster).  The IP address will be exposed
        /// by the Azure DNS like:
        /// </para>
        /// <para>
        /// DOMAINLABEL.AZURE-REGION.cloudapp.azure.com
        /// </para>
        /// <para>
        /// For example, a public IP address with the <b>mycluster</b> deployed to the
        /// Azure <b>westus</b> region would have this DNS name:
        /// </para>
        /// <para>
        /// mycluster.westus.cloudapp.azure.com
        /// </para>
        /// <para>
        /// Labels can be up to 80 characters in length and may include letters, digits,
        /// dashes, underscores, and periods.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "DomainLabel", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "domainLabel", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string DomainLabel { get; set; }

        /// <summary>
        /// <para>
        /// Specifies whether a static external IP address will be created for the cluster.  A static
        /// IP address will never change and may be referenced via a DNS A record.  Static addresses
        /// may incur additional costs and Azure limits the number of static addresses that may be
        /// provisioned for a subscription.  This defaults to <c>false</c>.
        /// </para>
        /// <para>
        /// When this is <c>false</c>, a dynamic external address will be provisioned.  This may be
        /// referenced via a DNS CNAME record and the address may change from time-to-time.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "StaticClusterAddress", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "staticClusterAddress", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool StaticClusterAddress { get; set; } = false;

        /// <summary>
        /// <note>
        /// <b>IMPORTANT:</b> assigning public IP addresses to cluster nodes is not currently
        /// implemented.
        /// </note>
        /// <para>
        /// Specifies whether the cluster nodes should be provisioned with public IP addresses
        /// in addition to the cluster wide public IP addresses assigned to the traffic manager.
        /// This defaults to <c>false</c>.
        /// </para>
        /// <note>
        /// You will incur additional recuring costs for each public IP address.
        /// </note>
        /// </summary>
        /// <remarks>
        /// <para>
        /// There are two main reasons for enabling this.
        /// </para>
        /// <list type="number">
        /// <item>
        /// Outbound SNAT port exhaustion: This can occur when cluster nodes behind a load
        /// balancer have a high rate of outbound requests to the Internet.  The essential
        /// issue is that the traffic manager can NAT a maximum of 64K outbound connections
        /// for the entire cluster.  This is described in detail 
        /// <a href="https://docs.microsoft.com/en-us/azure/load-balancer/load-balancer-outbound-connections#load-balanced-vm-with-no-instance-level-public-ip-address">here</a>.
        /// Assigning a public IP address to each node removes this cluster level restriction
        /// such that each node can have up to 64K outbound connections.
        /// </item>
        /// <item>
        /// Occasionally, it's important to be able to reach specific cluster nodes directly
        /// from the Internet.
        /// </item>
        /// </list>
        /// <para>
        /// Enabling this directs the <b>neon-cli</b> to create a dynamic instance level IP
        /// address for each cluster node and add a public network interface to each cluster 
        /// virtual machine.
        /// </para>
        /// <note>
        /// The public network interface will be protected by a public security group that
        /// denies all inbound traffic and allows all outbound traffic by default.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "PublicNodeAddresses", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "publicNodeAddresses", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool PublicNodeAddresses { get; set; } = false;

        /// <summary>
        /// Specifies the target Azure environment.  This defaults to the 
        /// normal public Azure cloud.  See <see cref="AzureCloudEnvironment"/>
        /// for other possibilities.
        /// </summary>
        [JsonProperty(PropertyName = "Environment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "environment", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public AzureCloudEnvironment Environment { get; set; } = null;

        /// <summary>
        /// Specifies the number of Azure fault domains the cluster nodes should be
        /// distributed across.  This defaults to <b>2</b> which should not be increased
        /// without making sure that your subscription supports the increase (most won't).
        /// </summary>
        [JsonProperty(PropertyName = "FaultDomains", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "faultDomains", ApplyNamingConventions = false)]
        [DefaultValue(2)]
        public int FaultDomains { get; set; } = 2;

        /// <summary>
        /// <para>
        /// Specifies the number of Azure update domains the cluster nodes will 
        /// distributed across.  This defaults to <b>5</b>  You may customize this
        /// with a value in the range of <b>2</b>...<b>20</b>.
        /// </para>
        /// <note>
        /// Larger clusters should increase this value to avoid losing significant capacity
        /// as Azure updates its underlying infrastructure in an update domain requiring
        /// VM shutdown and restarts.  A value of <b>2</b> indicates that one half of the
        /// cluster servers may be restarted during an update domain upgrade.  A value
        /// of <b>20</b> indicates that one twentieth of your VMs may be recycled at a
        /// time.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "UpdateDomains", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "updateDomains", ApplyNamingConventions = false)]
        [DefaultValue(5)]
        public int UpdateDomains { get; set; } = 5;

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

            foreach (var ch in clusterDefinition.Name)
            {
                if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                {
                    continue;
                }

                throw new ClusterDefinitionException($"cluster name [{clusterDefinition.Name}] is not valid for Azure deployment.  Only letters, digits, dashes, or underscores are allowed.");
            }

            if (string.IsNullOrEmpty(SubscriptionId))
            {
                throw new ClusterDefinitionException($"Azure hosting [{nameof(SubscriptionId)}] property cannot be empty.");
            }

            if (string.IsNullOrEmpty(TenantId))
            {
                throw new ClusterDefinitionException($"Azure hosting [{nameof(TenantId)}] property cannot be empty.");
            }

            if (string.IsNullOrEmpty(ApplicationId))
            {
                throw new ClusterDefinitionException($"Azure hosting [{nameof(ApplicationId)}] property cannot be empty.");
            }

            if (string.IsNullOrEmpty(Password))
            {
                throw new ClusterDefinitionException($"Azure hosting [{nameof(Password)}] property cannot be empty.");
            }

            if (string.IsNullOrEmpty(Region))
            {
                throw new ClusterDefinitionException($"Azure hosting [{nameof(Region)}] property cannot be empty.");
            }

            if (string.IsNullOrEmpty(DomainLabel))
            {
                throw new ClusterDefinitionException($"Azure hosting [{nameof(DomainLabel)}] property cannot be empty.");
            }

            // Verify [ResourceGroup].

            if (string.IsNullOrEmpty(ResourceGroup))
            {
                ResourceGroup = clusterDefinition.Name;
            }

            if (ResourceGroup.Length > 64)
            {
                throw new ClusterDefinitionException($"Azure hosting [{nameof(ResourceGroup)}] property cannot be longer than 64 characters.");
            }

            if (!char.IsLetter(ResourceGroup.First()))
            {
                throw new ClusterDefinitionException($"Azure hosting [{nameof(ResourceGroup)}] property must begin with a letter.");
            }

            if (ResourceGroup.Last() == '_' || ResourceGroup.Last() == '-')
            {
                throw new ClusterDefinitionException($"Azure hosting [{nameof(ResourceGroup)}] property must not end with a dash or underscore.");
            }

            foreach (var ch in ResourceGroup)
            {
                if (!(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'))
                {
                    throw new ClusterDefinitionException($"Azure hosting [{nameof(ResourceGroup)}] property must include only letters, digits, dashes or underscores.");
                }
            }

            // Verify [Environment].

            if (Environment != null)
            {
                Environment.Validate(clusterDefinition);
            }

            // Check Azure cluster limits.

            if (clusterDefinition.Masters.Count() > KubeConst.MaxMasters)
            {
                throw new ClusterDefinitionException($"cluster master count [{clusterDefinition.Masters.Count()}] exceeds the [{KubeConst.MaxMasters}] limit for clusters.");
            }

            if (clusterDefinition.Nodes.Count() > AzureHelper.MaxClusterNodes)
            {
                throw new ClusterDefinitionException($"cluster node count [{clusterDefinition.Nodes.Count()}] exceeds the [{AzureHelper.MaxClusterNodes}] limit for clusters deployed to Azure.");
            }
        }
    }
}
