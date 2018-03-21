//-----------------------------------------------------------------------------
// FILE:	    ProxyTcpBackend.cs
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

namespace Neon.Cluster
{
    /// <summary>
    /// Base class for proxy backends.
    /// </summary>
    public class ProxyBackend
    {
        /// <summary>
        /// The optional server backend server name.  The <b>neon-proxy-manager</b> will
        /// generate a unique name within the route if this isn't specified.
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
        /// Optionally identifies the Ansible host group to be targeted.  When this is not empty,
        /// all nodes within the group will be targeted unless <see cref="GroupLimit"/> is positive
        /// when up to <see cref="GroupLimit"/> randomly selected nodes will be targeted.
        /// </summary>
        [JsonProperty(PropertyName = "Group", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Group { get; set; } = null;

        /// <summary>
        /// Works in conjunction with <see cref="Group"/> to limit the number of Ansible
        /// group nodes to be targeted.  Setting this to zero (the default) indicates that
        /// all nodes in the group will be targeted.
        /// </summary>
        [JsonProperty(PropertyName = "GroupLimit", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int GroupLimit { get; set; } = 0;

        /// <summary>
        /// Validates the backend.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <param name="routeName">The parent route name.</param>
        public virtual void Validate(ProxyValidationContext context, string routeName)
        {
            if (!string.IsNullOrEmpty(Name) && !ClusterDefinition.IsValidName(Name))
            {
                context.Error($"Route [{routeName}] has backend server with invalid [{nameof(Name)}={Name}].");
            }

            if (!string.IsNullOrEmpty(Group))
            {
                if (!ClusterDefinition.IsValidName(Group))
                {
                    context.Error($"Route [{routeName}] has backend with [{nameof(Group)}={Group}] which is not a valid group name.");
                }

                if (GroupLimit < 0)
                {
                    context.Error($"Route [{routeName}] has backend with [{nameof(GroupLimit)}={GroupLimit}] which may not be less than zero.");
                }
            }
            else
            {
                if (string.IsNullOrEmpty(Server) ||
                    (!IPAddress.TryParse(Server, out var address) && !ClusterDefinition.DnsHostRegex.IsMatch(Server)))
                {
                    context.Error($"Route [{routeName}] has backend server [{Server}] which is not valid.  A DNS name or IP address was expected.");
                }
            }

            if (!NetHelper.IsValidPort(Port))
            {
                context.Error($"Route [{routeName}] has backend server with invalid [{nameof(Port)}={Port}] which is outside the range of valid TCP ports.");
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
        /// <returns>The selected cluster host node definitions.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the backend does not target a group.</exception>
        public List<NodeDefinition> SelectGroupNodes(Dictionary<string, List<NodeDefinition>> hostGroups)
        {
            Covenant.Requires<ArgumentNullException>(hostGroups != null);
            Covenant.Assert(hostGroups.Count(g => g.Key.Equals("all", StringComparison.InvariantCultureIgnoreCase)) > 0, "Expecting the [all] group to be present.");

            if (string.IsNullOrEmpty(Group))
            {
                throw new InvalidOperationException($"[{nameof(ProxyBackend)}.{nameof(Group)}()] works only for route backends that target a group.");
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