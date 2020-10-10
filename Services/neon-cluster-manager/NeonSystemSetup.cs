//-----------------------------------------------------------------------------
// FILE:	    NeonSystemSetup.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.

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

namespace NeonClusterManager
{
    public partial class NeonClusterManager : NeonService
    {
        /// <summary>
        /// Handles setup of neon-system database.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task NeonSystemSetup()
        {
            await SetupGrafanaAsync();
        }

        /// <summary>
        /// Method to wait for neon-system Citus database to be ready.
        /// </summary>
        /// <returns></returns>
        public async Task WaitForCitusAsync()
        {
            await NeonHelper.WaitForAsync(
                    async () => 
                    (
                        await k8s.ListNamespacedStatefulSetAsync("neon-system")).Items.Any(
                            s => s.Metadata.Name == "neon-system-db-citus-postgresql-worker"
                            && s.Status.ReadyReplicas == s.Status.Replicas
                    ), 
                    TimeSpan.FromSeconds(3600)
                );

            await NeonHelper.WaitForAsync(
                    async () => 
                    (
                        await k8s.ListNamespacedStatefulSetAsync("neon-system")).Items.Any(
                            s => s.Metadata.Name == "neon-system-db-citus-postgresql-master"
                            && s.Status.ReadyReplicas == s.Status.Replicas
                    ), 
                    TimeSpan.FromSeconds(3600)
                );
        }

        /// <summary>
        /// Configure Grafana database.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task SetupGrafanaAsync()
        {
            Log.LogInfo("[neon-system-db] Waiting for neon-system database to be ready.");
            await WaitForCitusAsync();

            Log.LogInfo("[neon-system-db] Configuring for Grafana.");
            var connString = "Host=neon-system-db-citus-postgresql.neon-system;Username=postgres;Password=0987654321;Database=postgres";

            var grafanaInitialized = true;

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            await using (var cmd = new NpgsqlCommand("SELECT DATNAME FROM pg_catalog.pg_database WHERE DATNAME = 'grafana'", conn))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                await reader.ReadAsync();
                if (!reader.HasRows)
                {
                    await conn.CloseAsync();
                    Log.LogInfo("[neon-system-db] Creating database 'grafana'.");
                    grafanaInitialized = false;
                    await using (var createCmd = new NpgsqlCommand("CREATE DATABASE grafana", conn))
                    {
                        await conn.OpenAsync();
                        await createCmd.ExecuteNonQueryAsync();
                        await conn.CloseAsync();
                    }
                }
            }

            await conn.OpenAsync();
            await using (var cmd = new NpgsqlCommand("SELECT 'exists' FROM pg_roles WHERE rolname='grafana'", conn))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                await reader.ReadAsync();
                if (!reader.HasRows)
                {
                    await conn.CloseAsync();
                    Log.LogInfo("[neon-system-db] Creating user 'grafana'.");
                    grafanaInitialized = false;
                    await using (var createCmd = new NpgsqlCommand("CREATE ROLE grafana WITH LOGIN", conn))
                    {
                        await conn.OpenAsync();
                        await createCmd.ExecuteNonQueryAsync();
                        await conn.CloseAsync();
                    }
                }
            }

            if (!grafanaInitialized)
            {
                await conn.OpenAsync();
                Log.LogInfo("[neon-system-db] Setting permissions for user 'grafana' on database 'grafana'.");
                await using (var createCmd = new NpgsqlCommand("GRANT ALL PRIVILEGES ON DATABASE grafana TO grafana", conn))
                {
                    await createCmd.ExecuteNonQueryAsync();
                    await conn.CloseAsync();
                }
            }
            Log.LogInfo("[neon-system-db] Finished setup for grafana.");
        }
    }
}
