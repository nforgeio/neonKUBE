//------------------------------------------------------------------------------
// FILE:         Service.cs
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
using System.Text;
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

namespace NeonSetupGrafana
{
    public partial class Service : NeonService
    {
        public const string StateTable = "state";

        private static Kubernetes k8s;
        private static string connString;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="serviceMap">Optionally specifies the service map.</param>
        public Service(string name, ServiceMap serviceMap = null)
            : base(name, serviceMap: serviceMap)
        {
            k8s = new Kubernetes(KubernetesClientConfiguration.InClusterConfig());
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected async override Task<int> OnRunAsync()
        {
            // Let NeonService know that we're running.
            
            await SetRunningAsync();
            await GetConnectionStringAsync();
            await SetupGrafanaAsync();
            
            return 0;
        }

        /// <summary>
        /// Gets the connection string used to connect to the neon-system database.
        /// </summary>
        /// <returns></returns>
        private async Task GetConnectionStringAsync()
        {
            var secret = await k8s.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbAdminSecret, KubeNamespaces.NeonSystem);

            var username = Encoding.UTF8.GetString(secret.Data["username"]);
            var password = Encoding.UTF8.GetString(secret.Data["password"]);

            connString = $"Host=db-citus-postgresql.{KubeNamespaces.NeonSystem};Username={username};Password={password};Database={KubeConst.NeonClusterOperatorDatabase}";
        }

        /// <summary>
        /// Configure Grafana database.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task SetupGrafanaAsync()
        {
            Log.LogInfo($"[{KubeNamespaces.NeonSystem}-db] Configuring for Grafana.");

            await UpdateStatusAsync("in-progress");

            var secret = await k8s.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbServiceSecret, KubeNamespaces.NeonSystem);

            await CreateDatabaseAsync("grafana", KubeConst.NeonSystemDbServiceUser, Encoding.UTF8.GetString(secret.Data["password"]));

            await UpdateStatusAsync("complete");

            Log.LogInfo($"[{KubeNamespaces.NeonSystem}-db] Finished setup for Grafana.");
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

        private async Task UpdateStatusAsync(string status)
        {
            await using (NpgsqlConnection conn = new NpgsqlConnection(connString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand($@"
INSERT
    INTO
    {StateTable} (KEY, value)
VALUES (@k, @v) ON
CONFLICT (KEY) DO
UPDATE
SET
    value = @v"))
                {
                    cmd.Parameters.AddWithValue("k", KubeConst.NeonJobSetupHarbor);
                    cmd.Parameters.AddWithValue("v", status);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
