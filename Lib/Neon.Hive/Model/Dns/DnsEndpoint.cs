//-----------------------------------------------------------------------------
// FILE:	    DnsEndpoint.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// Describes a DNS endpoint made by a <see cref="DnsEntry"/>.
    /// </summary>
    public class DnsEndpoint
    {
        /// <summary>
        /// Specifies the target host IP address, or FQDN, or a target host group
        /// by specifying <b>group=NAME</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Target", Required = Required.Always)]
        public string Target { get; set; }

        /// <summary>
        /// Optionally specifies that the target should be health checked via ICMP ping.
        /// This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Check", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Check { get; set; } = false;

        /// <summary>
        /// Attempts to extract the node group name from <see cref="Target"/> if present.
        /// </summary>
        /// <returns>The group name or <c>null</c>.</returns>
        public string GetGroupName()
        {
            if (Target.StartsWith("group="))
            {
                return Target.Substring("group=".Length);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Validates the DNS endpoint.  Any warning/errors will be appended to <paramref name="warnings"/>.
        /// </summary>
        /// <param name="warnings">Any warnings will be appended here.</param>
        /// <param name="hiveDefinition">The current hive definition,</param>
        /// <param name="nodeGroups">The hive node groups.</param>
        /// <param name="entryHostname">The parent <see cref="DnsEntry"/>'s hostname.</param>
        public void Validate(List<string> warnings, HiveDefinition hiveDefinition, Dictionary<string, List<NodeDefinition>> nodeGroups, string entryHostname)
        {
            Covenant.Requires<ArgumentException>(hiveDefinition != null);
            Covenant.Requires<ArgumentException>(nodeGroups != null);

            if (string.IsNullOrEmpty(Target))
            {
                warnings.Add($"Invalid [{nameof(DnsEndpoint)}.{nameof(Target)}={Target}] for [{nameof(DnsEntry)}={entryHostname}].");
            }

            var groupName = GetGroupName();

            if (groupName != null)
            {
                if (string.IsNullOrEmpty(groupName))
                {
                    warnings.Add($"Invalid [{nameof(DnsEndpoint)}.{nameof(Target)}={Target}] for [{nameof(DnsEntry)}={entryHostname}].");
                }
                else if (!nodeGroups.ContainsKey(groupName))
                {
                    warnings.Add($"Node group [{groupName}] not found for [{nameof(DnsEntry)}={entryHostname}].");
                }
            }
            else
            {
                if (!IPAddress.TryParse(Target, out var address) && !HiveDefinition.DnsHostRegex.IsMatch(Target))
                {
                    warnings.Add($"Invalid [{nameof(DnsEndpoint)}.{nameof(Target)}={Target}] is not a valid IP address or DNS hostname for [{nameof(DnsEntry)}={entryHostname}].");
                }
            }
        }
    }
}
