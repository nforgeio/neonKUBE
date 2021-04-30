//------------------------------------------------------------------------------
// FILE:         NeonClusterOperator.cs
// CONTRIBUTOR:  Marcus Bowyer
// COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading;
using System.Net.Sockets;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Net;
using Neon.Service;

using Helm.Helm;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

using Npgsql;

using YamlDotNet.RepresentationModel;

namespace NeonSetupHarbor
{
    public partial class Service : NeonService
    {
        private static Kubernetes k8s;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="serviceMap">Optionally specifies the service map.</param>
        public Service(string name, ServiceMap serviceMap = null)
            : base(name, serviceMap: serviceMap)
        {
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected async override Task<int> OnRunAsync()
        {
            // Let KubeService know that we're running.



            await SetRunningAsync();
            await WaitForCitusAsync();
            await SetupHarborAsync();

            return 0;
        }

        private static string connString = $"Host=db-citus-postgresql.{KubeNamespaces.NeonSystem};Username=postgres;Password=0987654321;Database=postgres";

        /// <summary>
        /// Method to wait for neon-system Citus database to be ready.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task WaitForCitusAsync()
        {
            Log.LogInfo($"[{KubeNamespaces.NeonSystem}-db] Waiting for {KubeNamespaces.NeonSystem} database to be ready.");

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    var statefulsets = await k8s.ListNamespacedStatefulSetAsync(KubeNamespaces.NeonSystem, labelSelector: "release=db");
                    if (statefulsets == null || statefulsets.Items.Count < 2)
                    {
                        return false;
                    }

                    return statefulsets.Items.All(@set => @set.Status.ReadyReplicas == @set.Spec.Replicas);
                },
                timeout: TimeSpan.FromMinutes(30),
                pollInterval: TimeSpan.FromSeconds(10));

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    var deployments = await k8s.ListNamespacedDeploymentAsync(KubeNamespaces.NeonSystem, labelSelector: "release=db");
                    if (deployments == null || deployments.Items.Count == 0)
                    {
                        return false;
                    }

                    return deployments.Items.All(deployment => deployment.Status.AvailableReplicas == deployment.Spec.Replicas);
                },
                timeout: TimeSpan.FromMinutes(30),
                pollInterval: TimeSpan.FromSeconds(10));

            Log.LogInfo($"[{KubeNamespaces.NeonSystem}-db] {KubeNamespaces.NeonSystem} database is ready.");
        }

        private async Task SetupHarborAsync()
        {
            Log.LogInfo($"[{KubeNamespaces.NeonSystem}-db] Configuring for Harbor.");

            var databases = new string[] { "registry", "clair", "notary_server", "notary_signer" };

            foreach (var db in databases)
            {
                await CreateDatabaseAsync(db, "harbor", "harbor");
            }

            Log.LogInfo($"[{KubeNamespaces.NeonSystem}-db] Finished setup for Harbor.");
        }

        /// <summary>
        /// Helper method to create a database with default user.
        /// </summary>
        /// <param name="dbName">Specifies the database name.</param>
        /// <param name="dbUser">Specifies the database user name.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task CreateDatabaseAsync(string dbName, string dbUser, string dbPass = null)
        {
            try
            {
                await using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                var dbInitialized = true;

                await using (var cmd = new NpgsqlCommand($"SELECT DATNAME FROM pg_catalog.pg_database WHERE DATNAME = '{dbName}'", conn))
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    await reader.ReadAsync();
                    if (!reader.HasRows)
                    {
                        await conn.CloseAsync();
                        Log.LogInfo($"[{KubeNamespaces.NeonSystem}-db] Creating database '{dbName}'.");
                        dbInitialized = false;
                        await using (var createCmd = new NpgsqlCommand($"CREATE DATABASE {dbName}", conn))
                        {
                            await conn.OpenAsync();
                            await createCmd.ExecuteNonQueryAsync();
                            await conn.CloseAsync();
                        }
                    }
                }

                if (conn.State != System.Data.ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }

                await using (var cmd = new NpgsqlCommand($"SELECT 'exists' FROM pg_roles WHERE rolname='{dbUser}'", conn))
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    await reader.ReadAsync();
                    if (!reader.HasRows)
                    {
                        await conn.CloseAsync();
                        Log.LogInfo($"[{KubeNamespaces.NeonSystem}-db] Creating user '{dbUser}'.");
                        dbInitialized = false;

                        string createCmdString;
                        if (!string.IsNullOrEmpty(dbPass))
                        {
                            createCmdString = $"CREATE USER {dbUser} WITH PASSWORD '{dbPass}'";
                        }
                        else
                        {
                            createCmdString = $"CREATE ROLE {dbUser} WITH LOGIN";
                        }

                        await using (var createCmd = new NpgsqlCommand(createCmdString, conn))
                        {
                            await conn.OpenAsync();
                            await createCmd.ExecuteNonQueryAsync();
                            await conn.CloseAsync();
                        }
                    }
                }

                if (!dbInitialized)
                {
                    if (conn.State != System.Data.ConnectionState.Open)
                    {
                        await conn.OpenAsync();
                    }

                    Log.LogInfo($"[{KubeNamespaces.NeonSystem}-db] Setting permissions for user '{dbUser}' on database '{dbName}'.");
                    await using (var createCmd = new NpgsqlCommand($"GRANT ALL PRIVILEGES ON DATABASE {dbName} TO {dbUser}", conn))
                    {
                        await createCmd.ExecuteNonQueryAsync();
                        await conn.CloseAsync();
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogError(e);
            }
        }
    }
}
