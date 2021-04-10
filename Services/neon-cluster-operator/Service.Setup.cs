//-----------------------------------------------------------------------------
// FILE:	    Service.Setup.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Net;
using Neon.Retry;
using Neon.Service;
using Neon.Tasks;

using Npgsql;

using k8s;

namespace NeonClusterOperator
{
    public partial class Service : NeonService
    {
        private static string connString = "Host=neon-system-db-citus-postgresql.neon-system;Username=postgres;Password=0987654321;Database=postgres";

        /// <summary>
        /// Handles setup of neon-system database.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task NeonSystemSetup()
        {
            await WaitForCitusAsync();

            var tasks = new List<Task>();

            tasks.Add(SetupGrafanaAsync());
            tasks.Add(SetupHarborAsync());

            await NeonHelper.WaitAllAsync(tasks);
        }

        /// <summary>
        /// Method to wait for neon-system Citus database to be ready.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task WaitForCitusAsync()
        {
            Log.LogInfo("[neon-system-db] Waiting for neon-system database to be ready.");

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    var statefulsets = await k8s.ListNamespacedStatefulSetAsync("neon-system", labelSelector: "release=neon-system-db");
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
                    var deployments = await k8s.ListNamespacedDeploymentAsync("neon-system", labelSelector: "release=neon-system-db");
                    if (deployments == null || deployments.Items.Count == 0)
                    {
                        return false;
                    }

                    return deployments.Items.All(deployment => deployment.Status.AvailableReplicas == deployment.Spec.Replicas);
                },
                timeout: TimeSpan.FromMinutes(30),
                pollInterval: TimeSpan.FromSeconds(10));

            Log.LogInfo("[neon-system-db] neon-system database is ready.");
        }

        private async Task SetupHarborAsync()
        {
            Log.LogInfo("[neon-system-db] Configuring for Harbor.");

            var databases = new string[] { "registry", "clair", "notary_server", "notary_signer" };

            foreach (var db in databases)
            {
                await CreateDatabaseAsync(db, "harbor", "harbor");
            }

            Log.LogInfo("[neon-system-db] Finished setup for Harbor.");
        }

        /// <summary>
        /// Configure Grafana database.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task SetupGrafanaAsync()
        {
            Log.LogInfo("[neon-system-db] Configuring for Grafana.");

            await CreateDatabaseAsync("grafana", "grafana");
            
            Log.LogInfo("[neon-system-db] Finished setup for Grafana.");
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
                        Log.LogInfo($"[neon-system-db] Creating database '{dbName}'.");
                        dbInitialized = false;
                        await using (var createCmd = new NpgsqlCommand($"CREATE DATABASE {dbName}", conn))
                        {
                            await conn.OpenAsync();
                            await createCmd.ExecuteNonQueryAsync();
                            await conn.CloseAsync();
                        }
                    }
                }

                if (conn.State != System.Data.ConnectionState.Open) {
                    await conn.OpenAsync(); 
                }

                await using (var cmd = new NpgsqlCommand($"SELECT 'exists' FROM pg_roles WHERE rolname='{dbUser}'", conn))
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    await reader.ReadAsync();
                    if (!reader.HasRows)
                    {
                        await conn.CloseAsync();
                        Log.LogInfo($"[neon-system-db] Creating user '{dbUser}'.");
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

                    Log.LogInfo($"[neon-system-db] Setting permissions for user '{dbUser}' on database '{dbName}'.");
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
