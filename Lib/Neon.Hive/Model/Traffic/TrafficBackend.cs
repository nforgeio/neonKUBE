//-----------------------------------------------------------------------------
// FILE:	    TrafficBackend.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// Base class for traffic manager backends.
    /// </summary>
    public class TrafficBackend
    {
        /// <summary>
        /// The optional server backend server name.  The <b>neon-proxy-manager</b> will
        /// generate a unique name within the rule if this isn't specified.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Name { get; set; } = null;

        /// <summary>
        /// The IP address or DNS name of the backend server where traffic to be forwarded.
        /// </summary>
        [JsonProperty(PropertyName = "Server", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Server { get; set; }

        /// <summary>
        /// The TCP port on the backend server where the traffic is to be forwarded.
        /// </summary>
        [JsonProperty(PropertyName = "Port", Required = Required.Always)]
        public int Port { get; set; }

        /// <summary>
        /// Optionally identifies the hive host group to be targeted.  When this is not empty,
        /// all nodes within the group will be targeted unless <see cref="GroupLimit"/> is positive
        /// when up to <see cref="GroupLimit"/> randomly selected nodes will be targeted.
        /// </summary>
        [JsonProperty(PropertyName = "Group", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Group { get; set; } = null;

        /// <summary>
        /// Works in conjunction with <see cref="Group"/> to limit the number of hive
        /// group nodes to be targeted.  Setting this to zero (the default) indicates that
        /// all nodes in the group will be targeted.
        /// </summary>
        [JsonProperty(PropertyName = "GroupLimit", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int GroupLimit { get; set; } = 0;

        /// <summary>
        /// The maximum number of connections to be allowed for this
        /// backend server or zero if the number of connections is to be
        /// unlimited.  This defaults to <b>0</b>.
        /// </summary>
        [JsonProperty(PropertyName = "MaxConnections", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int MaxConnections { get; set; } = 0;

        /// <summary>
        /// Validates the backend.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <param name="ruleName">The parent rule name.</param>
        public virtual void Validate(TrafficValidationContext context, string ruleName)
        {
            if (!string.IsNullOrEmpty(Name) && !HiveDefinition.IsValidName(Name))
            {
                context.Error($"Rule [{ruleName}] has backend server with invalid [{nameof(Name)}={Name}].");
            }

            if (!string.IsNullOrEmpty(Group))
            {
                if (!HiveDefinition.IsValidName(Group))
                {
                    context.Error($"Rule [{ruleName}] has backend with [{nameof(Group)}={Group}] which is not a valid group name.");
                }

                if (GroupLimit < 0)
                {
                    context.Error($"Rule [{ruleName}] has backend with [{nameof(GroupLimit)}={GroupLimit}] which may not be less than zero.");
                }
            }
            else
            {
                if (string.IsNullOrEmpty(Server) ||
                    (!IPAddress.TryParse(Server, out var address) && !HiveDefinition.DnsHostRegex.IsMatch(Server)))
                {
                    context.Error($"Rule [{ruleName}] has backend server [{Server}] which is not valid.  A DNS name or IP address was expected.");
                }
            }

            if (!NetHelper.IsValidPort(Port))
            {
                context.Error($"Rule [{ruleName}] has backend server with invalid [{nameof(Port)}={Port}] which is outside the range of valid TCP ports.");
            }

            if (MaxConnections < 0)
            {
                context.Error($"Rule [{ruleName}] has backend server with invalid [{nameof(MaxConnections)}={MaxConnections}].");
            }
        }

        /// <summary>
        /// Selects target node backends based on the <see cref="Group"/> and <see cref="GroupLimit"/> properties 
        /// and the host groups passed.  This works only for backends that target a group.
        /// </summary>
        /// <param name="hostGroups">
        /// Dictionary mapping host group names to the list of host node 
        /// definitions within the named group.
        /// </param>
        /// <returns>The selected hive host node definitions.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the backend does not target a group.</exception>
        public List<NodeDefinition> SelectGroupNodes(Dictionary<string, List<NodeDefinition>> hostGroups)
        {
            Covenant.Requires<ArgumentNullException>(hostGroups != null);
            Covenant.Assert(hostGroups.Count(g => g.Key.Equals(HiveHostGroups.All, StringComparison.InvariantCultureIgnoreCase)) > 0, "Expecting the [all] group to be present.");

            if (string.IsNullOrEmpty(Group))
            {
                throw new InvalidOperationException($"[{nameof(TrafficBackend)}.{nameof(Group)}()] works only for rule backends that target a group.");
            }

            if (!hostGroups.TryGetValue(Group, out var groupNodes))
            {
                // The group doesn't exist so return an empty list.

                return new List<NodeDefinition>();
            }

            if (GroupLimit == 0 || GroupLimit >= groupNodes.Count)
            {
                // If there is no group limit or the limit is greater than or equal
                // to the number of group nodes, so just return the group nodes.

                return groupNodes;
            }
            else
            {
                // Randomly select [GroupLimit] nodes.

                return groupNodes.SelectRandom(GroupLimit).ToList();
            }
        }
    }
}