//-----------------------------------------------------------------------------
// FILE:	    LoadBalancerTcpRule.cs
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

namespace Neon.Cluster
{
    /// <summary>
    /// Describes a rule that forwards TCP traffic from TCP frontends
    /// to TCP backends.
    /// </summary>
    public class LoadBalancerTcpRule : LoadBalancerRule
    {
        private List<LoadBalancerTcpBackend> selectedBackends;     // Used to cache selected backends

        /// <summary>
        /// Default constructor.
        /// </summary>
        public LoadBalancerTcpRule()
        {
            Mode = LoadBalancerMode.Tcp;
        }

        /// <summary>
        /// The load balancer frontend definitions.
        /// </summary>
        [JsonProperty(PropertyName = "Frontends", Required = Required.Always)]
        public List<LoadBalancerTcpFrontend> Frontends { get; set; } = new List<LoadBalancerTcpFrontend>();

        /// <summary>
        /// The load balancer backend definitions.
        /// </summary>
        [JsonProperty(PropertyName = "Backends", Required = Required.Always)]
        public List<LoadBalancerTcpBackend> Backends { get; set; } = new List<LoadBalancerTcpBackend>();

        /// <summary>
        /// The maximum overall number of connections to be allowed for this
        /// rule or zero if the number of connections will be limited
        /// to the overall pool of connections specified by <see cref="LoadBalancerSettings.MaxConnections"/>.
        /// This defaults to <b>0</b>.
        /// </summary>
        [JsonProperty(PropertyName = "MaxConnections", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int MaxConnections { get; set; } = 0;

        /// <summary>
        /// Validates the rule.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <param name="addImplicitFrontends">Optionally add any implicit frontends (e.g. for HTTPS redirect).</param>
        public override void Validate(LoadBalancerValidationContext context, bool addImplicitFrontends = false)
        {
            base.Validate(context, addImplicitFrontends);

            foreach (var frontend in Frontends)
            {
                frontend.Validate(context, this);
            }

            foreach (var backend in Backends)
            {
                backend.Validate(context, this);
            }

            // Verify that the ports are unique for each frontend.

            var frontendMap = new HashSet<int>();

            foreach (var frontend in Frontends)
            {
                var key = frontend.ProxyPort;

                if (frontendMap.Contains(key))
                {
                    context.Error($"TCP rule [{Name}] includes two or more frontends that map to port [{key}].");
                }

                frontendMap.Add(key);
            }

            if (MaxConnections < 0 || MaxConnections > ushort.MaxValue)
            {
                context.Error($"Rule [{Name}] specifies invalid [{nameof(MaxConnections)}={MaxConnections}].");
            }
        }

        /// <summary>
        /// Returns the list of backends selected to be targeted by processing any
        /// backends with <see cref="LoadBalancerBackend.Group"/> and <see cref="LoadBalancerBackend.GroupLimit"/>
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
        /// This method will compute the select the first time it's called on an
        /// instance and then return the same selected backends thereafter.
        /// </note>
        /// </remarks>
        public List<LoadBalancerTcpBackend> SelectBackends(Dictionary<string, List<NodeDefinition>> hostGroups)
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
            // same group could be considered to be a configuration problem).

            // NOTE:
            //
            // I'm treating a targeted host group that doesn't actually exist
            // as an empty group.  A case could be made to signal this as an
            // error or log a warning, but one could also argue that treating
            // this as a empty group makes logical sense (and it's much easier
            // to implement to boot).

            var processedGroups = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            selectedBackends = new List<LoadBalancerTcpBackend>();

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
    }
}
