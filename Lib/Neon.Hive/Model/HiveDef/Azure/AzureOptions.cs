//-----------------------------------------------------------------------------
// FILE:	    AzureOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

namespace Neon.Hive
{
    /// <summary>
    /// Specifies the Microsoft Azure hive hosting settings.
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
        [DefaultValue(null)]
        public string SubscriptionId { get; set; }

        /// <summary>
        /// Tenant ID generated when creating the neon tool's Azure service principal.
        /// </summary>
        [JsonProperty(PropertyName = "TenantId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string TenantId { get; set; }

        /// <summary>
        /// Application ID generated when creating the neon tool's Azure service principal. 
        /// </summary>
        [JsonProperty(PropertyName = "ApplicationId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ApplicationId { get; set; }

        /// <summary>
        /// Password generated when creating the neon tool's Azure service principal.
        /// </summary>
        [JsonProperty(PropertyName = "Password", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Password { get; set; }

        /// <summary>
        /// Azure resource group where all hive components are to be provisioned.  This defaults
        /// to the hive name but can be customized as required.
        /// </summary>
        [JsonProperty(PropertyName = "ResourceGroup", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ResourceGroup { get; set; }

        /// <summary>
        /// Identifies the target Azure region (e.g. <b>westus</b>).
        /// </summary>
        [JsonProperty(PropertyName = "Region", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Region { get; set; }

        /// <summary>
        /// The DNS domain prefix for the public IP address to be assigned to the hive.
        /// </summary>
        /// <remarks>
        /// <note>
        /// <b>Recomendation:</b> To ensure that there's no conflicts with other 
        /// services deployed to Azure by you or other companies, we recommend that
        /// you generate a GUID and assign it to this property.
        /// </note>
        /// <para>
        /// This must be unique across all services deployed to an Azure region (your
        /// services as well as any other Azure hive).  The IP address will be exposed
        /// by the Azure DNS like:
        /// </para>
        /// <para>
        /// DOMAINLABEL.AZURE-REGION.cloudapp.azure.com
        /// </para>
        /// <para>
        /// For example, a public IP address with the <b>myhive</b> deployed to the
        /// Azure <b>westus</b> region would have this DNS name:
        /// </para>
        /// <para>
        /// myhive.westus.cloudapp.azure.com
        /// </para>
        /// <para>
        /// Labels can be up to 80 characters in length and may include letters, digits,
        /// dashes, underscores, and periods.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "DomainLabel", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string DomainLabel { get; set; }

        /// <summary>
        /// <para>
        /// Specifies whether a static external IP address will be created for the hive.  A static
        /// IP address will never change and may be referenced via a DNS A record.  Static addresses
        /// may incur additional costs and Azure limits the number of static addresses that may be
        /// provisioned for a subscription.  This defaults to <c>false</c>.
        /// </para>
        /// <para>
        /// When this is <c>false</c>, a dynamic external address will be provisioned.  This may be
        /// referenced via a DNS CNAME record and the address may change from time-to-time.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "StaticHiveAddress", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool StaticHiveAddress { get; set; } = false;

        /// <summary>
        /// <note>
        /// <b>IMPORTANT:</b> assigning public IP addresses to hive nodes is not currently
        /// implemented.
        /// </note>
        /// <para>
        /// Specifies whether the hive nodes should be provisioned with public IP addresses
        /// in addition to the hive wide public IP addresses assigned to the traffic manager.
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
        /// Outbound SNAT port exhaustion: This can occur when hive nodes behind a load
        /// balancer have a high rate of outbound requests to the Internet.  The essential
        /// issue is that the traffic manager can NAT a maximum of 64K outbound connections
        /// for the entire hive.  This is described in detail 
        /// <a href="https://docs.microsoft.com/en-us/azure/load-balancer/load-balancer-outbound-connections#load-balanced-vm-with-no-instance-level-public-ip-address">here</a>.
        /// Assigning a public IP address to each node removes this hive level restriction
        /// such that each node can have up to 64K outbound connections.
        /// </item>
        /// <item>
        /// Occasionally, it's important to be able to reach specific hive nodes directly
        /// from the Internet.
        /// </item>
        /// </list>
        /// <para>
        /// Enabling this directs the <b>neon-cli</b> to create a dynamic instance level IP
        /// address for each hive node and add a public network interface to each hive 
        /// virtual machine.
        /// </para>
        /// <note>
        /// The public network interface will be protected by a public security group that
        /// denies all inbound traffic and allows all outbound traffic by default.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "PublicNodeAddresses", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool PublicNodeAddresses { get; set; } = false;

        /// <summary>
        /// <para>
        /// neonHIVEs reserves some ports on the public Azure load balancer.
        /// The hive will reserve  <b>5 + node_count</b> ports beginning 
        /// at this port number which defaults to <b>37100</b>.  The first five
        /// ports will be used to direct OpenVPN client traffic to the VPN
        /// servers, and the remaining ports may be used to NAT SSH traffic
        /// to specific hive nodes.
        /// </para>
        /// <para>
        /// This port range should work for most hive deployments, but you
        /// may need to modify this if you need to expose services that expose
        /// one or more conflicting ports. 
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "FirstReservedPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(37100)]
        public int FirstReservedPort { get; set; } = 37100;

        /// <summary>
        /// Specifies the target Azure environment.  This defaults to the 
        /// normal public Azure cloud.  See <see cref="AzureCloudEnvironment"/>
        /// for other possibilities.
        /// </summary>
        [JsonProperty(PropertyName = "Environment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public AzureCloudEnvironment Environment { get; set; } = null;

        /// <summary>
        /// Specifies the number of Azure fault domains the hive nodes should be
        /// distributed across.  This defaults to <b>2</b> which should not be increased
        /// without making sure that your subscription supports the increase (most won't).
        /// </summary>
        [JsonProperty(PropertyName = "FaultDomains", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(2)]
        public int FaultDomains { get; set; } = 2;

        /// <summary>
        /// <para>
        /// Specifies the number of Azure update domains the hive nodes will 
        /// distributed across.  This defaults to <b>5</b>  You may customize this
        /// with a value in the range of <b>2</b>...<b>20</b>.
        /// </para>
        /// <note>
        /// Larger hives should increase this value to avoid losing significant capacity
        /// as Azure updates its underlying infrastructure in an update domain requiring
        /// VM shutdown and restarts.  A value of <b>2</b> indicates that one half of the
        /// hive servers may be restarted during an update domain upgrade.  A value
        /// of <b>20</b> indicates that one twentieth of your VMs may be recycled at a
        /// time.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "UpdateDomains", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(5)]
        public int UpdateDomains { get; set; } = 5;

        /// <summary>
        /// The first manager load balancer frontend port reserved for OpenVPN connections to individual manager nodes
        /// in the hive.  External port numbers in the range of <see cref="FirstVpnFrontendPort"/>...<see cref="LastVpnFrontendPort"/>
        /// inclusive are reserved for this.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public int FirstVpnFrontendPort
        {
            get { return FirstReservedPort; }
        }

        /// <summary>
        /// The last manager load balancer frontend port reserved for OpenVPN connections to individual manager nodes
        /// in the hive.  External port numbers in the range of <see cref="FirstVpnFrontendPort"/>...<see cref="LastVpnFrontendPort"/>
        /// inclusive are reserved for this.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public int LastVpnFrontendPort
        {
            get { return FirstVpnFrontendPort + HiveConst.MaxManagers - 1; }
        }

        /// <summary>
        /// The first load balancer frontend port reserved for SSH connections to individual nodes in the hive.
        /// External port numbers in the range of <see cref="FirstSshFrontendPort"/>...<see cref="LastSshFrontendPort"/>
        /// inclusive are reserved for this.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public int FirstSshFrontendPort
        {
            get { return LastVpnFrontendPort + 1; }
        }

        /// <summary>
        /// The last load balancer frontend port reserved for SSH connections to individual nodes in the hive.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public int LastSshFrontendPort
        {
            get { return FirstSshFrontendPort + AzureHelper.MaxHiveNodes - 1; }
        }

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="HiveDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(HiveDefinition hiveDefinition)
        {
            Covenant.Requires<ArgumentNullException>(hiveDefinition != null);

            foreach (var ch in hiveDefinition.Name)
            {
                if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                {
                    continue;
                }

                throw new HiveDefinitionException($"Hive name [{hiveDefinition.Name}] is not valid for Azure deployment.  Only letters, digits, dashes, or underscores are allowed.");
            }

            if (string.IsNullOrEmpty(SubscriptionId))
            {
                throw new HiveDefinitionException($"Azure hosting [{nameof(SubscriptionId)}] property cannot be empty.");
            }

            if (string.IsNullOrEmpty(TenantId))
            {
                throw new HiveDefinitionException($"Azure hosting [{nameof(TenantId)}] property cannot be empty.");
            }

            if (string.IsNullOrEmpty(ApplicationId))
            {
                throw new HiveDefinitionException($"Azure hosting [{nameof(ApplicationId)}] property cannot be empty.");
            }

            if (string.IsNullOrEmpty(Password))
            {
                throw new HiveDefinitionException($"Azure hosting [{nameof(Password)}] property cannot be empty.");
            }

            if (string.IsNullOrEmpty(Region))
            {
                throw new HiveDefinitionException($"Azure hosting [{nameof(Region)}] property cannot be empty.");
            }

            if (string.IsNullOrEmpty(DomainLabel))
            {
                throw new HiveDefinitionException($"Azure hosting [{nameof(DomainLabel)}] property cannot be empty.");
            }

            // Verify [ResourceGroup].

            if (string.IsNullOrEmpty(ResourceGroup))
            {
                ResourceGroup = hiveDefinition.Name;
            }

            if (ResourceGroup.Length > 64)
            {
                throw new HiveDefinitionException($"Azure hosting [{nameof(ResourceGroup)}] property cannot be longer than 64 characters.");
            }

            if (!char.IsLetter(ResourceGroup.First()))
            {
                throw new HiveDefinitionException($"Azure hosting [{nameof(ResourceGroup)}] property must begin with a letter.");
            }

            if (ResourceGroup.Last() == '_' || ResourceGroup.Last() == '-')
            {
                throw new HiveDefinitionException($"Azure hosting [{nameof(ResourceGroup)}] property must not end with a dash or underscore.");
            }

            foreach (var ch in ResourceGroup)
            {
                if (!(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'))
                {
                    throw new HiveDefinitionException($"Azure hosting [{nameof(ResourceGroup)}] property must include only letters, digits, dashes or underscores.");
                }
            }

            // Verify [Environment].

            if (Environment != null)
            {
                Environment.Validate(hiveDefinition);
            }

            // Check Azure hive limits.

            if (hiveDefinition.Managers.Count() > HiveConst.MaxManagers)
            {
                throw new HiveDefinitionException($"Hive manager count [{hiveDefinition.Managers.Count()}] exceeds the [{HiveConst.MaxManagers}] limit for neonHIVEs.");
            }

            if (hiveDefinition.Nodes.Count() > AzureHelper.MaxHiveNodes)
            {
                throw new HiveDefinitionException($"Hive node count [{hiveDefinition.Nodes.Count()}] exceeds the [{AzureHelper.MaxHiveNodes}] limit for neonHIVEs deployed to Azure.");
            }
        }
    }
}
