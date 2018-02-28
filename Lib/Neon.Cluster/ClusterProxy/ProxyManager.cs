//-----------------------------------------------------------------------------
// FILE:	    ProxyManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Cluster
{
    /// <summary>
    /// Handles cluster proxy and routing related operations for a <see cref="ClusterProxy"/>.
    /// </summary>
    public sealed class ProxyManager
    {
        private const string proxyManagerPrefix = "neon/service/neon-proxy-manager";
        private const string vaultCertPrefix    = "neon-secret/cert";

        private ClusterProxy cluster;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="cluster">The parent <see cref="ClusterProxy"/>.</param>
        /// <param name="name">The proxy name (<b>public</b> or <b>private</b>).</param>
        internal ProxyManager(ClusterProxy cluster, string name)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);
            Covenant.Requires<ArgumentException>(name == "public" || name == "private");

            this.cluster = cluster;
            this.Name    = name;
        }

        /// <summary>
        /// Returns the proxy name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the Consul key for the proxy's global settings.
        /// </summary>
        /// <returns>The Consul key path.</returns>
        private string GetProxySettingsKey()
        {
            return $"{proxyManagerPrefix}/conf/{Name}/settings";
        }

        /// <summary>
        /// Returns the Consul key for a proxy route.
        /// </summary>
        /// <param name="routeName">The route name.</param>
        /// <returns>The Consul key path.</returns>
        private string GetProxyRouteKey(string routeName)
        {
            return $"{proxyManagerPrefix}/conf/{Name}/routes/{routeName}";
        }

        /// <summary>
        /// Returns the proxy settings.
        /// </summary>
        /// <returns>The <see cref="ProxySettings"/>.</returns>
        public ProxySettings GetSettings()
        {
            return cluster.Consul.KV.GetObject<ProxySettings>(GetProxySettingsKey()).Result;
        }

        /// <summary>
        /// Updates the proxy settings.
        /// </summary>
        /// <param name="settings">The new settings.</param>
        public void UpdateSettings(ProxySettings settings)
        {
            Covenant.Requires<ArgumentNullException>(settings != null);

            cluster.Consul.KV.PutObject(GetProxySettingsKey(), settings, Formatting.Indented).Wait();
        }

        /// <summary>
        /// Returns the proxy definition including its settings and routes.
        /// </summary>
        /// <returns>The <see cref="ProxyDefinition"/>.</returns>
        /// <exception cref="NeonClusterException">Thrown if the proxy definition could not be loaded.</exception>
        public ProxyDefinition GetDefinition()
        {
            // Fetch the proxy settings and all of its routes to create a full [ProxyDefinition].

            var proxyDefinition  = new ProxyDefinition() { Name = this.Name };
            var proxySettingsKey = GetProxySettingsKey();

            if (cluster.Consul.KV.Exists(proxySettingsKey).Result)
            {
                proxyDefinition.Settings = NeonHelper.JsonDeserialize<ProxySettings>(cluster.Consul.KV.GetString(proxySettingsKey).Result);
            }
            else
            {
                throw new NeonClusterException($"Settings for proxy [{Name}] do not exist or could not be loaded.");
            }

            foreach (var routeName in ListRoutes())
            {
                var route = GetRoute(routeName);

                proxyDefinition.Routes.Add(route.Name, route);
            }

            return proxyDefinition;
        }

        /// <summary>
        /// Forces the <b>neon-proxy-manager</b> to regenerate the configuration for the proxy.
        /// </summary>
        public void Build()
        {
            cluster.Consul.KV.PutString($"{proxyManagerPrefix}/proxies/{Name}/hash", Convert.ToBase64String(new byte[16])).Wait();
            cluster.Consul.KV.PutString($"{proxyManagerPrefix}/conf/reload", DateTime.UtcNow).Wait();
        }

        /// <summary>
        /// Deletes a proxy route if it exists.
        /// </summary>
        /// <param name="routeName">The route name.</param>
        /// <returns><c>true</c> if the route existed and was deleted, <c>false</c> if it didn't exist.</returns>
        public bool RemoveRoute(string routeName)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(routeName));

            var routeKey = GetProxyRouteKey(routeName);

            if (cluster.Consul.KV.Exists(routeKey).Result)
            {
                cluster.Consul.KV.Delete(routeKey);

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns a proxy route if it exists.
        /// </summary>
        /// <param name="routeName">The route name.</param>
        /// <returns>The <see cref="ProxyRoute"/> or <c>null</c>.</returns>
        public ProxyRoute GetRoute(string routeName)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(routeName));

            var routeKey = GetProxyRouteKey(routeName);

            if (cluster.Consul.KV.Exists(routeKey).Result)
            {
                return ProxyRoute.ParseJson(cluster.Consul.KV.GetString(routeKey).Result);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Adds or updates a proxy route.
        /// </summary>
        /// <param name="route">The route definition.</param>
        /// <returns>
        /// <c>true</c> if it existed and was updated, <b>false</b>
        /// if the proxy didn't already exist and was added.
        /// </returns>
        /// <exception cref="ClusterDefinitionException">Thrown if the route is not valid.</exception>
        public bool PutRoute(ProxyRoute route)
        {
            Covenant.Requires<ArgumentNullException>(route != null);
            Covenant.Requires<ArgumentNullException>(ClusterDefinition.IsValidName(route.Name));

            var routeKey = GetProxyRouteKey(route.Name);
            var update   = cluster.Consul.KV.Exists(routeKey).Result;

            // Load the the full proxy definition and cluster certificates, add/replace
            // the route being set and then verify that the route is OK.

            var proxyDefinition = GetDefinition();
            var certificates    = cluster.Certificate.GetAll();

            proxyDefinition.Routes[route.Name] = route;
            proxyDefinition.Validate(certificates);

            var validationContext = proxyDefinition.Validate(certificates);

            validationContext.ThrowIfErrors();

            // Save the route to the cluster.

            cluster.Consul.KV.PutObject(routeKey, route, Formatting.Indented).Wait();

            return update;
        }

        /// <summary>
        /// Lists the names of the proxy routes.
        /// </summary>
        /// <returns>The <see cref="IEnumerable{T}"/> of route names.</returns>
        public IEnumerable<string> ListRoutes()
        {
            var routesResponse = cluster.Consul.KV.List($"{proxyManagerPrefix}/conf/{Name}/routes/").Result.Response;

            if (routesResponse != null)
            {
                var names = new List<string>();

                foreach (var keyPair in cluster.Consul.KV.List($"{proxyManagerPrefix}/conf/{Name}/routes/").Result.Response)
                {
                    names.Add(keyPair.Key.Substring(keyPair.Key.LastIndexOf('/') + 1));
                }

                return names;
            }
            else
            {
                return new string[0];
            }
        }
    }
}
