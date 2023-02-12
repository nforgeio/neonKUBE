//-----------------------------------------------------------------------------
// FILE:	    GlauthController.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Extensions.Logging;

using JsonDiffPatch;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Glauth;
using Neon.Kube.Operator.ResourceManager;
using Neon.Kube.Operator.Controller;
using Neon.Retry;
using Neon.Tasks;
using Neon.Time;

using k8s;
using k8s.Autorest;
using k8s.Models;

using Newtonsoft.Json;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;

using Npgsql;
using Neon.Kube.Operator.Rbac;
using Neon.Kube.Resources.Cluster;
using Neon.Kube.Resources;
using Neon.Kube.Operator.Attributes;

namespace NeonClusterOperator
{
    /// <summary>
    /// Manages Glauth LDAP database.
    /// </summary>
    [Controller(ManageCustomResourceDefinitions = false)]
    [RbacRule<V1Secret>(
        Verbs = RbacVerb.Get, 
        Scope = EntityScope.Namespaced,
        Namespace = KubeNamespace.NeonSystem,
        ResourceNames = "neon-admin.neon-system-db.credentials.postgresql,glauth-users,glauth-groups")]
    public class GlauthController : IResourceController<V1Secret>
    {
        //---------------------------------------------------------------------
        // Static members

        private static string connectionString;

        /// <summary>
        /// Starts the controller.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task StartAsync(IServiceProvider serviceProvider)
        {
            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("start", attributes => attributes.Add("customresource", nameof(V1Secret)));

                var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<GlauthController>();

                var k8s = serviceProvider.GetRequiredService<IKubernetes>();

                var secret = await k8s.CoreV1.ReadNamespacedSecretAsync("neon-admin.neon-system-db.credentials.postgresql", KubeNamespace.NeonSystem);

                var password = Encoding.UTF8.GetString(secret.Data["password"]);

                connectionString = $"Host={KubeService.NeonSystemDb}.{KubeNamespace.NeonSystem};Username={KubeConst.NeonSystemDbAdminUser};Password={password};Database=glauth";

                logger?.LogDebugEx(() => $"ConnectionString: [{connectionString}]");
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes               k8s;
        private readonly ILogger<GlauthController> logger;
        private readonly Service                   service;

        /// <summary>
        /// Constructor.
        /// </summary>
        public GlauthController(
            IKubernetes k8s, 
            ILogger<GlauthController> logger,
            Service service)
        {
            Covenant.Requires(k8s != null, nameof(k8s));
            Covenant.Requires(logger != null, nameof(logger));

            this.k8s     = k8s;
            this.logger  = logger;
            this.service = service;
        }

        /// <summary>
        /// Filter out secrets that we aren't interested in.
        /// </summary>
        /// <param name="resource"></param>
        /// <returns></returns>
        public static bool Filter(V1Secret resource)
        {
            return (resource.Name() == "glauth-users" || resource.Name() == "glauth-groups");
        }

        /// <summary>
        /// Called periodically to allow the operator to perform global events.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task IdleAsync()
        {
            await SyncContext.Clear;

            logger?.LogInformationEx("[IDLE]");
        }

        /// <inheritdoc/>
        public async Task<ResourceControllerResult> ReconcileAsync(V1Secret resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("reconcile", attributes => attributes.Add("resource", nameof(V1Secret)));

                switch (resource.Name())
                {
                    case "glauth-users":

                        await UpdateGlauthUsersAsync(resource);
                        break;

                    case "glauth-groups":

                        await UpdateGlauthGroupsAsync(resource);
                        break;

                    default:
                        break;

                }

                logger?.LogInformationEx(() => $"RECONCILED: {resource.Name()}");

                return null;
            }
        }

        /// <inheritdoc/>
        public async Task DeletedAsync(V1Secret resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                logger?.LogInformationEx(() => $"DELETED: {resource.Name()}");
            }
        }

        /// <inheritdoc/>
        public async Task OnPromotionAsync()
        {
            await SyncContext.Clear;

            logger?.LogInformationEx(() => $"PROMOTED");
        }

        /// <inheritdoc/>
        public async Task OnDemotionAsync()
        {
            await SyncContext.Clear;

            logger?.LogInformationEx(() => $"DEMOTED");
        }

        /// <inheritdoc/>
        public async Task OnNewLeaderAsync(string identity)
        {
            await SyncContext.Clear;

            logger?.LogInformationEx(() => $"NEW LEADER: {identity}");
        }

        private async Task UpdateGlauthUsersAsync(V1Secret resource)
        {
            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                foreach (var user in resource.Data.Keys)
                {
                    using (var userActivity = TelemetryHub.ActivitySource?.StartActivity("AddUser"))
                    {
                        var userData     = NeonHelper.YamlDeserialize<GlauthUser>(Encoding.UTF8.GetString(resource.Data[user]));
                        var name         = userData.Name;
                        var givenname    = userData.Name;
                        var mail         = userData.Mail ?? $"{userData.Name}@{service.ClusterInfo.Domain}";
                        var uidnumber    = userData.UidNumber;
                        var primarygroup = userData.PrimaryGroup;
                        var passsha256   = CryptoHelper.ComputeSHA256String(userData.Password);

                        await using (var cmd = new NpgsqlCommand(
                            $@"INSERT INTO users(name, givenname, mail, uidnumber, primarygroup, passsha256)
                            VALUES('{name}','{givenname}','{mail}','{uidnumber}','{primarygroup}','{passsha256}')
                                ON CONFLICT (name) DO UPDATE
                                    SET givenname    = '{givenname}',
                                        mail         = '{mail}',
                                        uidnumber    = '{uidnumber}',
                                        primarygroup = '{primarygroup}',
                                        passsha256   = '{passsha256}';", conn))
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }

                        if (userData.Capabilities != null)
                        {
                            using (var userCapabilityActivity = TelemetryHub.ActivitySource?.StartActivity("AddUserCapabilities"))
                            {
                                foreach (var capability in userData.Capabilities)
                                {
                                    long count;
                                    await using (var cmd = new NpgsqlCommand(
                                        $@"SELECT count(*)
                                            FROM capabilities
                                            WHERE userid={uidnumber} and ""action""='{capability.Action}' and ""object""='{capability.Object}';", conn))
                                    {
                                        count = (long)await cmd.ExecuteScalarAsync();
                                    }

                                    if (count == 0)
                                    {
                                        await using (var cmd = new NpgsqlCommand(
                                        $@"INSERT INTO capabilities(userid, action, object)
                                            VALUES('{uidnumber}','{capability.Action}','{capability.Object}');", conn))
                                        {
                                            await cmd.ExecuteNonQueryAsync();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private async Task UpdateGlauthGroupsAsync(V1Secret resource)
        {
            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                foreach (var key in resource.Data.Keys)
                {
                    using (var groupActivity = TelemetryHub.ActivitySource?.StartActivity("AddGroup"))
                    {
                        var group = NeonHelper.YamlDeserialize<GlauthGroup>(Encoding.UTF8.GetString(resource.Data[key]));

                        await using (var cmd = new NpgsqlCommand(
                            $@"INSERT INTO groups(name, gidnumber)
                            VALUES('{group.Name}','{group.GidNumber}') 
                                ON CONFLICT (name) DO UPDATE
                                    SET gidnumber = '{group.GidNumber}';", conn))
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
        }
    }
}
