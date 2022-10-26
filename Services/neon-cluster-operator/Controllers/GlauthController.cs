//-----------------------------------------------------------------------------
// FILE:	    GlauthController.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Operator;
using Neon.Kube.ResourceDefinitions;
using Neon.Retry;
using Neon.Tasks;
using Neon.Time;

using k8s;
using k8s.Autorest;
using k8s.Models;

using KubeOps.Operator.Controller;
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Rbac;

using Newtonsoft.Json;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;

using Quartz.Impl;
using Quartz;
using Npgsql;
using k8s.KubeConfigModels;
using Microsoft.AspNetCore.Mvc;
using Neon.Cryptography;
using Octokit;
using System.Text.RegularExpressions;

namespace NeonClusterOperator
{
    /// <summary>
    /// Manages Glauth LDAP database.
    /// </summary>
    [EntityRbac(typeof(V1Secret), Verbs = RbacVerb.Get | RbacVerb.List | RbacVerb.Patch | RbacVerb.Watch | RbacVerb.Update)]
    public class GlauthController : IOperatorController<V1Secret>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly ILogger log = TelemetryHub.CreateLogger<GlauthController>();

        private static ResourceManager<V1Secret, GlauthController> resourceManager;
        private static string connectionString;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static GlauthController()
        {
        }

        /// <summary>
        /// Starts the controller.
        /// </summary>
        /// <param name="k8s">The <see cref="IKubernetes"/> client to use.</param>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task StartAsync(
            IKubernetes k8s,
            IServiceProvider serviceProvider)
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            // Load the configuration settings.

            var leaderConfig =
                new LeaderElectionConfig(
                    k8s,
                    @namespace: KubeNamespace.NeonSystem,
                    leaseName: $"{Program.Service.Name}.glauth",
                    identity: Pod.Name,
                    promotionCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}glauth_promoted", "Leader promotions"),
                    demotionCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}glauth_demoted", "Leader demotions"),
                    newLeaderCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}glauth_newLeader", "Leadership changes"));

            var options = new ResourceManagerOptions()
            {
                ErrorMaxRetryCount = int.MaxValue,
                ErrorMaxRequeueInterval = TimeSpan.FromMinutes(10),
                ErrorMinRequeueInterval = TimeSpan.FromSeconds(60),
                IdleCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}glauth_idle", "IDLE events processed."),
                ReconcileCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}glauth_idle", "RECONCILE events processed."),
                DeleteCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}glauth_idle", "DELETED events processed."),
                StatusModifyCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}glauth_idle", "STATUS-MODIFY events processed."),
                IdleErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}glauth_idle_error", "Failed ClusterOperatorSettings IDLE event processing."),
                ReconcileErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}glauth_reconcile_error", "Failed ClusterOperatorSettings RECONCILE event processing."),
                DeleteErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}glauth_delete_error", "Failed ClusterOperatorSettings DELETE event processing."),
                StatusModifyErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}glauth_statusmodify_error", "Failed ClusterOperatorSettings STATUS-MODIFY events processing.")
            };

            resourceManager = new ResourceManager<V1Secret, GlauthController>(
                k8s,
                options: options,
                leaderConfig: leaderConfig,
                serviceProvider: serviceProvider,
                filter: (secret) =>
                {
                    try
                    {
                        if (secret.Metadata.Labels[NeonLabel.ManagedBy] == KubeService.NeonClusterOperator
                                && secret.Name() == "glauth-users" || secret.Name() == "glauth-groups")
                        {
                            return true;
                        }
                        return false;
                    }
                    catch
                    {
                        return false;
                    }
                });

            await resourceManager.StartAsync();

            var secret = await k8s.ReadNamespacedSecretAsync("neon-admin.neon-system-db.credentials.postgresql", KubeNamespace.NeonSystem);

            var password = Encoding.UTF8.GetString(secret.Data["password"]);

            connectionString = $"Host={KubeService.NeonSystemDb}.{KubeNamespace.NeonSystem};Username={KubeConst.NeonSystemDbAdminUser};Password={password};Database=glauth";

            log.LogInformationEx($"ConnectionString: [{connectionString}]");
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes k8s;

        /// <summary>
        /// Constructor.
        /// </summary>
        public GlauthController(IKubernetes k8s)
        {
            Covenant.Requires(k8s != null, nameof(k8s));

            this.k8s = k8s;
        }

        /// <summary>
        /// Called periodically to allow the operator to perform global events.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task IdleAsync()
        {
            await SyncContext.Clear;

            log.LogInformationEx("[IDLE]");
        }

        /// <inheritdoc/>
        public async Task<ResourceControllerResult> ReconcileAsync(V1Secret resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("reconcile", attributes => attributes.Add("resource", nameof(V1Secret)));

                // Ignore all events when the controller hasn't been started.

                if (resourceManager == null)
                {
                    return null;
                }

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

                log.LogInformationEx(() => $"RECONCILED: {resource.Name()}");

                return null;
            }
        }

        /// <inheritdoc/>
        public async Task DeletedAsync(V1Secret resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {

                // Ignore all events when the controller hasn't been started.

                if (resourceManager == null)
                {
                    return;
                }

                log.LogInformationEx(() => $"DELETED: {resource.Name()}");
            }
        }

        /// <inheritdoc/>
        public async Task OnPromotionAsync()
        {
            await SyncContext.Clear;

            log.LogInformationEx(() => $"PROMOTED");
        }

        /// <inheritdoc/>
        public async Task OnDemotionAsync()
        {
            await SyncContext.Clear;

            log.LogInformationEx(() => $"DEMOTED");
        }

        /// <inheritdoc/>
        public async Task OnNewLeaderAsync(string identity)
        {
            await SyncContext.Clear;

            log.LogInformationEx(() => $"NEW LEADER: {identity}");
        }

        private async Task UpdateGlauthUsersAsync(V1Secret resource)
        {
            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                foreach (var user in resource.Data.Keys)
                {
                    using (var userActivity = TelemetryHub.ActivitySource.StartActivity("AddUser"))
                    {
                        var userData = NeonHelper.YamlDeserialize<GlauthUser>(Encoding.UTF8.GetString(resource.Data[user]));
                        var name = userData.Name;
                        var givenname = userData.Name;
                        var mail = userData.Mail ?? $"{userData.Name}@{Program.Service.ClusterInfo.Domain}";
                        var uidnumber = userData.UidNumber;
                        var primarygroup = userData.PrimaryGroup;
                        var passsha256 = CryptoHelper.ComputeSHA256String(userData.Password);

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
                            using (var userCapabilityActivity = TelemetryHub.ActivitySource.StartActivity("AddUserCapabilities"))
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
            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                foreach (var key in resource.Data.Keys)
                {
                    using (var groupActivity = TelemetryHub.ActivitySource.StartActivity("AddGroup"))
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
