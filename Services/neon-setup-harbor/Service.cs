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

using Minio;

using Newtonsoft.Json;

using Npgsql;

using YamlDotNet.RepresentationModel;

namespace NeonSetupHarbor
{
    public partial class Service : NeonService
    {
        public const string StateTable = "state";
     
        private static Kubernetes k8s;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="serviceMap">Optionally specifies the service map.</param>
        public Service(string name, ServiceMap serviceMap = null)
            : base(name, serviceMap: serviceMap)
        {
            k8s = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());
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
            await SetupHarborAsync();

            return 0;
        }

        /// <summary>
        /// Gets a connection string for connecting to Citus.
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public async Task<string> GetConnectionStringAsync(string database = "postgres")
        {
            var secret = await k8s.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbAdminSecret, KubeNamespaces.NeonSystem);

            var username = Encoding.UTF8.GetString(secret.Data["username"]);
            var password = Encoding.UTF8.GetString(secret.Data["password"]);

            var dbHost = $"db-citus-postgresql.{KubeNamespaces.NeonSystem}";

            return $"Host={dbHost};Username={username};Password={password};Database={database}";
        }

        private async Task SetupHarborAsync()
        {
            Log.LogInfo($"[{KubeNamespaces.NeonSystem}-db] Configuring for Harbor.");

            await UpdateStatusAsync("in-progress");

            var secret = await k8s.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbServiceSecret, KubeNamespaces.NeonSystem);

            var harborSecret = new V1Secret()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name = KubeConst.RegistrySecretKey,
                    NamespaceProperty = KubeNamespaces.NeonSystem
                },
                Data = new Dictionary<string, byte[]>(),
                StringData = new Dictionary<string, string>()
            };

            if ((await k8s.ListNamespacedSecretAsync(KubeNamespaces.NeonSystem)).Items.Any(s => s.Metadata.Name == KubeConst.RegistrySecretKey))
            {
                harborSecret = await k8s.ReadNamespacedSecretAsync(KubeConst.RegistrySecretKey, KubeNamespaces.NeonSystem);

                if (harborSecret.Data == null)
                {
                    harborSecret.Data = new Dictionary<string, byte[]>();
                }
                harborSecret.StringData = new Dictionary<string, string>();
            }

            if (!harborSecret.Data.ContainsKey("postgresql-password"))
            {
                harborSecret.Data["postgresql-password"] = secret.Data["password"];
                await UpsertSecretAsync(harborSecret, KubeNamespaces.NeonSystem);
            }

            if (!harborSecret.Data.ContainsKey("secret"))
            {
                harborSecret.StringData["secret"] = NeonHelper.GetCryptoRandomPassword(20);
                await UpsertSecretAsync(harborSecret, KubeNamespaces.NeonSystem);
            }

            var databases = new string[] { "core", "clair", "notaryserver", "notarysigner" };

            foreach (var db in databases)
            {
                await CreateDatabaseAsync($"{KubeConst.NeonSystemDbHarborPrefix}_{db}", KubeConst.NeonSystemDbServiceUser, Encoding.UTF8.GetString(secret.Data["password"]));
            }

            var minioSecret = await k8s.ReadNamespacedSecretAsync("minio", KubeNamespaces.NeonSystem);

            var endpoint = "minio.neon-system";
            var accessKey = Encoding.UTF8.GetString(minioSecret.Data["accesskey"]);
            var secretKey = Encoding.UTF8.GetString(minioSecret.Data["secretkey"]);

            var minio = new MinioClient(endpoint, accessKey, secretKey);

            var buckets = await minio.ListBucketsAsync();
            if (!await minio.BucketExistsAsync("harbor"))
            {
                await minio.MakeBucketAsync("harbor");
            }

            await UpdateStatusAsync("complete");

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
                await using var conn = new NpgsqlConnection(await GetConnectionStringAsync());
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
            await using var conn = new NpgsqlConnection(await GetConnectionStringAsync(KubeConst.NeonClusterOperatorDatabase));
            {
                await conn.OpenAsync();
                await using (var cmd = new NpgsqlCommand($@"
INSERT
    INTO
    {StateTable} (KEY, value)
VALUES (@k, @v) ON
CONFLICT (KEY) DO
UPDATE
SET
    value = @v", conn))
                {
                    cmd.Parameters.AddWithValue("k", KubeConst.NeonJobSetupHarbor);
                    cmd.Parameters.AddWithValue("v", status);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<V1Secret> UpsertSecretAsync(V1Secret secret, string @namespace = null)
        {
            if ((await k8s.ListNamespacedSecretAsync(@namespace)).Items.Any(s => s.Metadata.Name == secret.Name()))
            {
                return await k8s.ReplaceNamespacedSecretAsync(secret, secret.Name(), @namespace);
            }
            else
            {
                return await k8s.CreateNamespacedSecretAsync(secret, @namespace);
            }
        }
    }
}
