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
        /// Application ID for the application created to manage Azure access to neonKUBE provisioning and management tools.. 
        /// </summary>
        [JsonProperty(PropertyName = "AppId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "appId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string AppId { get; set; }

        /// <summary>
        /// Password generated when creating the neon tool's Azure service principal.
        /// </summary>
        [JsonProperty(PropertyName = "AppPassword", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "appPassword", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string AppPassword { get; set; }

        /// <summary>
        /// Azure resource group where all cluster components are to be provisioned.  This defaults
        /// to "neon-" plus the cluster name but can be customized as required.
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
        /// <para>
        /// neonKUBE cluster VMs are all deployed within the same Azure <a href="https://azure.microsoft.com/en-us/blog/introducing-proximity-placement-groups/">placement group</a>
        /// by default.  This ensures the smallest possible network latency between the cluster VMs.
        /// </para>
        /// <note>
        /// <para>
        /// Proximity placement groups have one downside: they make it more likely that Azure
        /// may not be able to find enough unused VMs to satisfy the proximity constraints.  This
        /// can happen when you first provision a cluster or later on when you try to scale one.
        /// </para>
        /// <para>
        /// For neonKUBE clusters the additional risk of an Azure provisioning failure is going
        /// to be very low due to how we use availablity sets, which is as similar deployment
        /// constraint: master nodes are deployed to one availability set and workers to another.
        /// Without a proximity placement group, Azure could deploy the masters to one datacenter
        /// and the workers to another.  This wasn't that likely in the past but as Azure has
        /// added more datacenters, the chance of this happening has increased.
        /// </para>
        /// <para>
        /// Adding the proximity placement constrain, requires that Azure deploy both the masters
        /// and workers in the same datacenter.  So say your cluster has 3 masters and 50 workers.
        /// With proximity placement enabled, the Azure region will need to have a datacenter with
        /// 53 VMs available with the specified sizes.  With proximity placement disabled, Azure
        /// could deploy the 3 masters in one datacenter and the 50 workers in another.
        /// </para>
        /// </note>
        /// <para>
        /// This property defaults to <c>false</c>.  You can disable the proximity placement
        /// constraint by setting this to <c>true</c>.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "DisableProximityPlacement", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "disableProximityPlacement", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool DisableProximityPlacement { get; set; } = false;

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
        [JsonProperty(PropertyName = "StaticAddress", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "staticAddress", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool StaticAddress { get; set; } = false;

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
        /// <para>
        /// Specifies the number of Azure fault domains the worker nodes should be
        /// distributed across.  This defaults to <b>3</b> which should not be increased
        /// without making sure that your subscription supports the increase (most won't).
        /// </para>
        /// <note>
        /// Manager nodes will always be provisioned in three fault domains to ensure
        /// that there will always be a quorum after any single fault domain failure.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "FaultDomains", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "faultDomains", ApplyNamingConventions = false)]
        [DefaultValue(3)]
        public int FaultDomains { get; set; } = 3;

        /// <summary>
        /// <para>
        /// Specifies the number of Azure update domains the cluster workers will 
        /// distributed across.  This defaults to <b>20</b>  You may customize this
        /// with a value in the range of <b>2</b>...<b>20</b>.
        /// </para>
        /// <para>
        /// Azure automatically distributes VMs across the specified number of update
        /// domains and when it's necessary to perform planned maintenance on the underlying
        /// hardware or to relocate a VM to another host, Azure gaurantees that it will
        /// reboot hosts in only one update domain at a time and then wait 30 minutes between
        /// update domains to give the application a chance to stablize.
        /// </para>
        /// <para>
        /// A value of <b>2</b> indicates that one half of the cluster servers may be rebooted
        /// at the same time during an update domain upgrade.  A value of <b>20</b> indicates 
        /// that one twentieth of your VMs may be rebooted at a time.
        /// </para>
        /// <note>
        /// <para>
        /// There's no way to specifically assign cluster nodes to specific update domains
        /// in Azure.  This would have been nice for a cluster hosting replicated database
        /// nodes where we'd like to assign replica nodes to different update domains such
        /// that all data would still be available while an update domain was being rebooted.
        /// </para>
        /// <para>
        /// I imagine Azure doesn't allow this due to the difficuilty of ensuring these
        /// constraints across a very large number of customer deployments.  Azure also
        /// mentions that the disruption of a VM for planned maintenance can be slight
        /// because VMs can be relocated from one host to another while still running.
        /// </para>
        /// </note>
        /// <note>
        /// Manager nodes are always deployed with 20 update domains and since no cluster
        /// should ever need anywhere close this number of managers, we'll be ensured
        /// that only a single manager will be rebooted together during planned Azure
        /// maintenance and the 30 minutes Azure waits after rebooting an update domain
        /// gives the rebooted manager a chance to rejoin the other managers and catch
        /// up on any changes that happened while it was offline.
        /// </note>
        /// <note>
        /// neonKUBE deploys manager and worker nodes in separate Azure availability zones.
        /// This means that there will always be a quorum of managers available as any one
        /// update zone is rebooted.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "UpdateDomains", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "updateDomains", ApplyNamingConventions = false)]
        [DefaultValue(20)]
        public int UpdateDomains { get; set; } = 20;

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

            if (string.IsNullOrEmpty(AppId))
            {
                throw new ClusterDefinitionException($"Azure hosting [{nameof(AppId)}] property cannot be empty.");
            }

            if (string.IsNullOrEmpty(AppPassword))
            {
                throw new ClusterDefinitionException($"Azure hosting [{nameof(AppPassword)}] property cannot be empty.");
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
