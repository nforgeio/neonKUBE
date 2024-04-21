//-----------------------------------------------------------------------------
// FILE:        GlauthController.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Glauth;
using Neon.Net;
using Neon.Operator.Attributes;
using Neon.Operator.Controllers;
using Neon.Operator.Rbac;
using Neon.Tasks;

using Npgsql;

using OpenTelemetry.Trace;

namespace NeonClusterOperator
{
    /// <summary>
    /// Manages Glauth LDAP database.
    /// </summary>
    [ResourceController(
        ManageCustomResourceDefinitions = false,
        LabelSelector                   = "neonkube.io/managed-by=neon-cluster-operator,neonkube.io/controlled-by=glauth-controller",
        MaxConcurrentReconciles         = 1)]
    [RbacRule<V1Secret>(
        Verbs     = RbacVerb.All, 
        Scope     = EntityScope.Cluster,
        Namespace = KubeNamespace.NeonSystem)]
    [RbacRule<V1Pod>(Verbs = RbacVerb.List)]
    public class GlauthController : ResourceControllerBase<V1Secret>
    {
        //---------------------------------------------------------------------
        // Static members

        private static string   connectionString;

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes                k8s;
        private readonly ILogger<GlauthController>  logger;
        private readonly Service                    service;

        /// <summary>
        /// Constructor.
        /// </summary>
        public GlauthController(
            IKubernetes               k8s, 
            ILogger<GlauthController> logger,
            Service                   service)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(logger != null, nameof(logger));

            this.k8s     = k8s;
            this.logger  = logger;
            this.service = service;
        }

        /// <summary>
        /// Starts the controller.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public override async Task StartAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("start", attributes => attributes.Add("customresource", nameof(V1Secret)));

                var logger   = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<GlauthController>();
                var k8s      = serviceProvider.GetRequiredService<IKubernetes>();
                var secret   = await k8s.CoreV1.ReadNamespacedSecretAsync("neon-admin.neon-system-db.credentials.postgresql", KubeNamespace.NeonSystem);
                var password = Encoding.UTF8.GetString(secret.Data["password"]);

                if (!NeonHelper.IsDevWorkstation)
                {
                    connectionString = $"Host={KubeService.NeonSystemDb}.{KubeNamespace.NeonSystem};Username={KubeConst.NeonSystemDbAdminUser};Password={password};Database=glauth";
                }
                else
                {
                    var localPort = NetHelper.GetUnusedTcpPort(IPAddress.Loopback);
                    var pod       = (await k8s.CoreV1.ListNamespacedPodAsync(KubeNamespace.NeonSystem, labelSelector: "app=neon-system-db")).Items.First();

                    connectionString = $"Host=localhost;Port={localPort};Username={KubeConst.NeonSystemDbAdminUser};Password={password};Database=glauth";

                    service.PortForwardManager.StartPodPortForward(
                        name:       pod.Name(),
                        @namespace: KubeNamespace.NeonSystem,
                        localPort:  localPort,
                        remotePort: 5432);
                }

                logger?.LogDebugEx(() => $"ConnectionString: [{connectionString}]");
            }
        }

        /// <inheritdoc/>
        public override async Task<ResourceControllerResult> ReconcileAsync(V1Secret resource, CancellationToken cancellationToken = default)
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
        public override async Task DeletedAsync(V1Secret resource, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                logger?.LogInformationEx(() => $"DELETED: {resource.Name()}");
            }
        }

        /// <summary>
        /// Updates an SSO user secret.
        /// </summary>
        /// <param name="secret">Specifies the secret resource including the user identity and passwword.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task UpdateGlauthUsersAsync(V1Secret secret)
        {
            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                foreach (var user in secret.Data.Keys)
                {
                    using (var userActivity = TelemetryHub.ActivitySource?.StartActivity("AddUser"))
                    {
                        var userData     = NeonHelper.YamlDeserialize<GlauthUser>(Encoding.UTF8.GetString(secret.Data[user]));
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

        /// <summary>
        /// Updates an SSO group secret.
        /// </summary>
        /// <param name="secret">Specifies the secret resource including the user identity and passwword.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task UpdateGlauthGroupsAsync(V1Secret secret)
        {
            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                foreach (var key in secret.Data.Keys)
                {
                    using (var groupActivity = TelemetryHub.ActivitySource?.StartActivity("AddGroup"))
                    {
                        var group = NeonHelper.YamlDeserialize<GlauthGroup>(Encoding.UTF8.GetString(secret.Data[key]));

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
