//-----------------------------------------------------------------------------
// FILE:	    TrafficTcpRule.cs
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

using Neon.Common;

namespace Neon.Hive
{
    /// <summary>
    /// Describes a rule that forwards TCP traffic from TCP frontends
    /// to TCP backends.
    /// </summary>
    public class TrafficTcpRule : TrafficRule
    {
        private List<TrafficTcpBackend> selectedBackends;     // Used to cache selected backends

        /// <summary>
        /// Default constructor.
        /// </summary>
        public TrafficTcpRule()
        {
            base.Mode = TrafficMode.Tcp;
        }

        /// <summary>
        /// Enables the transmission of low-level keep-alive packets from HAProxy to both the
        /// client and backends to detect dropped connections.  The interval at which these
        /// packets are transmitted are determined by the operating system configuration.
        /// This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "KeepAlive", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool KeepAlive { get; set; } = false;

        /// <summary>
        /// The traffic manager frontend definitions.
        /// </summary>
        [JsonProperty(PropertyName = "Frontends", Required = Required.Always)]
        public List<TrafficTcpFrontend> Frontends { get; set; } = new List<TrafficTcpFrontend>();

        /// <summary>
        /// The traffic manager backend definitions.
        /// </summary>
        [JsonProperty(PropertyName = "Backends", Required = Required.Always)]
        public List<TrafficTcpBackend> Backends { get; set; } = new List<TrafficTcpBackend>();

        /// <summary>
        /// Validates the rule.
        /// </summary>
        /// <param name="context">The validation context.</param>
        public override void Validate(TrafficValidationContext context)
        {
            base.Validate(context);

            Frontends = Frontends ?? new List<TrafficTcpFrontend>();
            Backends  = Backends ?? new List<TrafficTcpBackend>();

            if (Frontends.Count == 0)
            {
                context.Error($"Rule [{Name}] has does not define a frontend.");
            }

            if (Backends.Count == 0)
            {
                context.Error($"Rule [{Name}] has does not define a backend.");
            }

            foreach (var frontend in Frontends)
            {
                frontend.Validate(context, this);
            }

            foreach (var backend in Backends)
            {
                backend.Validate(context, this);
            }

            // Verify that the ports are unique for each frontend and that none of these TCP
            // target one of the reserved HTTP/HTTPS proxy ports.

            var frontendMap = new HashSet<int>();

            foreach (var frontend in Frontends)
            {
                var key = frontend.ProxyPort;

                if (frontendMap.Contains(key))
                {
                    context.Error($"TCP rule [{Name}] includes two or more frontends that map to port [{key}].");
                }

                if (frontend.ProxyPort == HiveHostPorts.ProxyPublicHttp || frontend.ProxyPort == HiveHostPorts.ProxyPublicHttps ||
                    frontend.ProxyPort == HiveHostPorts.ProxyPublicHttp || frontend.ProxyPort == HiveHostPorts.ProxyPublicHttps)
                {
                    context.Error($"Rule [{Name}] has a TCP frontend with [{nameof(frontend.ProxyPort)}={frontend.ProxyPort}] that is incorrectly mapped to a reserved HTTP/HTTPS port.");
                }

                frontendMap.Add(key);
            }
        }

        /// <summary>
        /// Returns the list of backends selected to be targeted by processing any
        /// backends with <see cref="TrafficBackend.Group"/> and <see cref="TrafficBackend.GroupLimit"/>
        /// properties configured to dynamically select backend target nodes.
        /// </summary>
        /// <param name="hostGroups">
        /// Dictionary mapping host group names to the list of host node 
        /// definitions within the named group.
        /// </param>
        /// <returns>The list of selected backends.</returns>
        /// <remarks>
        /// <note>
        /// This is a somewhat specialized method used by the <b>neon-proxy-manager</b>
        /// when generating HAProxy configuration files.
        /// </note>
        /// <note>
        /// This method will compute the selected backends the first time it's called 
        /// on an instance and then return the same selected backends thereafter.
        /// </note>
        /// </remarks>
        public List<TrafficTcpBackend> SelectBackends(Dictionary<string, List<NodeDefinition>> hostGroups)
        {
            Covenant.Requires<ArgumentNullException>(hostGroups != null);

            if (selectedBackends != null)
            {
                return selectedBackends;   // Return the cached backends
            }

            if (Backends.Count(be => !string.IsNullOrEmpty(be.Group)) == 0)
            {
                // There is no group targeting so we can just return the 
                // backend definitions.

                return Backends;
            }

            // We actually need to select backends.  Any backend that doesn't
            // target a group will be added as-is and then we'll need to
            // process group targets to actually select the backend nodes.
            //
            // Note that we're only going to process the first backend that
            // targets any given group (multiple backends targeting the 
            // same group will be considered to be a configuration problem).

            // NOTE:
            //
            // I'm treating a targeted host group that doesn't actually exist
            // as an empty group.  A case could be made to signal this as an
            // error or log a warning, but one could also argue that treating
            // this as a empty group makes logical sense (and it's much easier
            // to implement to boot).

            var processedGroups = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            selectedBackends = new List<TrafficTcpBackend>();

            foreach (var backend in Backends)
            {
                if (string.IsNullOrEmpty(backend.Group))
                {
                    selectedBackends.Add(backend);
                }
                else if (!processedGroups.Contains(backend.Group))
                {
                    foreach (var groupNode in backend.SelectGroupNodes(hostGroups).OrderBy(n => n.Name))
                    {
                        var backendClone = NeonHelper.JsonClone(backend);

                        backendClone.Name   = groupNode.Name;
                        backendClone.Server = groupNode.PrivateAddress.ToString();

                        selectedBackends.Add(backendClone);
                    }

                    processedGroups.Add(backend.Group);
                }
            }

            return selectedBackends;
        }

        /// <inheritdoc/>
        public override void Normalize(bool isPublic)
        {
            base.Normalize(isPublic);

            if (!isPublic)
            {
                foreach (var frontend in Frontends)
                {
                    frontend.PublicPort = 0;
                }
            }
        }
    }
}
