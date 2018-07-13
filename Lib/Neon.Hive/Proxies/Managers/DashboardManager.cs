//-----------------------------------------------------------------------------
// FILE:	    DashboardManager.cs
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
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Hive
{
    /// <summary>
    /// Handles hive dashboard related operations for a <see cref="HiveProxy"/>.
    /// </summary>
    public sealed class DashboardManager
    {
        private HiveProxy hive;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="hive">The parent <see cref="HiveProxy"/>.</param>
        internal DashboardManager(HiveProxy hive)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            this.hive = hive;
        }

        /// <summary>
        /// Removes a hive dashboard if it exists.
        /// </summary>
        /// <param name="name">The dashboard name.</param>
        public void Remove(string name)
        {
            Covenant.Requires<ArgumentException>(HiveDefinition.IsValidName(name));

            hive.Consul.Client.KV.Delete(GetDashboardConsulKey(name)).Wait();
        }

        /// <summary>
        /// Retrieves a hive dashboard.
        /// </summary>
        /// <param name="name">The dashboard name.</param>
        /// <returns>The dashboard if present or <c>null</c> if it doesn't exist.</returns>
        public HiveDashboard Get(string name)
        {
            Covenant.Requires<ArgumentException>(HiveDefinition.IsValidName(name), $"[{name}] is not a valid hive dashboard name.");

            return hive.Consul.Client.KV.GetObjectOrDefault<HiveDashboard>(GetDashboardConsulKey(name)).Result;

        }

        /// <summary>
        /// Lists the hive dashboards.
        /// </summary>
        /// <returns>The hive dashboards.</returns>
        public List<HiveDashboard> List()
        {
            var result = hive.Consul.Client.KV.ListOrDefault<HiveDashboard>(HiveConst.ConsulDashboardsKey).Result;

            if (result == null)
            {
                return new List<HiveDashboard>();
            }
            else
            {
                return result.ToList();
            }
        }

        /// <summary>
        /// Adds or updates a hive dashboard.
        /// </summary>
        /// <param name="dashboard">The dashboard.</param>
        /// <exception cref="HiveDefinitionException">Thrown if the dashboard is not valid.</exception>
        public void Set(HiveDashboard dashboard)
        {
            Covenant.Requires<ArgumentNullException>(dashboard != null);

            var errors = dashboard.Validate(hive.Definition);

            if (errors.Count > 0)
            {
                throw new HiveDefinitionException($"Invalid dashboard: {errors.First()}");
            }

            hive.Consul.Client.KV.PutObject(GetDashboardConsulKey(dashboard.Name), dashboard, Formatting.Indented).Wait();
        }

        /// <summary>
        /// Returns the Consul key for a dashboard based on its name.
        /// </summary>
        /// <param name="name">The dashboard name.</param>
        /// <returns>The Consul key path.</returns>
        private string GetDashboardConsulKey(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            return $"{HiveConst.ConsulDashboardsKey}/{name.ToLowerInvariant()}";
        }
    }
}
