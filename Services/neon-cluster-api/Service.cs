//------------------------------------------------------------------------------
// FILE:         NeonClusterApi.cs
// CONTRIBUTOR:  Marcus Bowyer
// COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Net;
using Neon.Retry;
using Neon.Service;
using Neon.Postgres;

using k8s;
using k8s.Models;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Npgsql;


namespace NeonClusterApi
{
    public partial class Service : NeonService
    {
        public string StateTable = "state";

        public KubernetesWithRetry k8s;
        public string DbConnectionString;

        // class fields
        private IWebHost webHost;
        
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="serviceMap">Optionally specifies the service map.</param>
        public Service(string name, ServiceMap serviceMap = null)
            : base(name, version: KubeVersions.NeonKubeVersion, serviceMap: serviceMap)
        {
            k8s = new KubernetesWithRetry(KubernetesClientConfiguration.BuildDefaultConfig());

            k8s.RetryPolicy = new ExponentialRetryPolicy(
                e => true,
                maxAttempts: int.MaxValue,
                initialRetryInterval: TimeSpan.FromSeconds(0.25),
                maxRetryInterval: TimeSpan.FromSeconds(10),
                timeout: TimeSpan.FromMinutes(5));
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected async override Task<int> OnRunAsync()
        {
            // Wait for Citus and make sure it's initialized.

            await InitializeDatabaseAsync();

            var endpoint = Description.Endpoints.Default;

            webHost = new WebHostBuilder()
                .UseStartup<KubeKv>()
                .UseKestrel(options => options.Listen(IPAddress.Any, endpoint.Uri.Port))
                .ConfigureServices(services => services.AddSingleton(typeof(Service), this))
                .Build();

            webHost.Start();

            // Let KubeService know that we're running.

            await SetRunningAsync();

            await Terminator.StopEvent.WaitAsync();

            return await Task.FromResult(0);
        }

        /// <summary>
        /// Gets a connection string for connecting to Citus.
        /// </summary>
        /// <param name="database"></param>
        /// <returns></returns>
        public async Task<string> GetConnectionStringAsync(string database = "postgres")
        {
            var username = GetEnvironmentVariable("CITUS_USER");
            var password = GetEnvironmentVariable("CITUS_PASSWORD");

            var dbHost = ServiceMap[NeonServices.NeonSystemDb].Endpoints.Default.Uri.Host;
            var dbPort = ServiceMap[NeonServices.NeonSystemDb].Endpoints.Default.Uri.Port;

            var connectionString = $"Host={dbHost};Username={username};Password={password};Database={database};Port={dbPort}";

            Log.LogDebug($"Connection string: [{connectionString.Replace(password, "REDACTED")}]");

            return await Task.FromResult(connectionString);
        }

        /// <summary>
        /// Gets the connection string used to connect to the neon-system database.
        /// </summary>
        /// <returns></returns>
        private async Task InitializeDatabaseAsync()
        {
            Log.LogInfo($"Connecting to citus...");

            DbConnectionString    = await GetConnectionStringAsync();
            var schemaDirectory   = Assembly.GetExecutingAssembly().GetResourceFileSystem("NeonClusterApi.Schema");

            var variables = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
                { "database", KubeConst.NeonClusterOperatorDatabase },
                { "service_user",  GetEnvironmentVariable("CITUS_USER") },
                { "service_password", GetEnvironmentVariable("CITUS_PASSWORD") },
                { "state_table", StateTable }
            };

            await using (NpgsqlConnection conn = new NpgsqlConnection(DbConnectionString))
            {
                await conn.OpenAsync();

                using (var schemaManager = new SchemaManager(conn, KubeConst.NeonClusterOperatorDatabase, schemaDirectory, variables))
                {
                    var status = await schemaManager.GetStatusAsync();

                    switch (status.SchemaStatus)
                    {
                        case SchemaStatus.ExistsNoSchema:

                            Log.LogInfo($"[{KubeConst.NeonClusterOperatorDatabase}] database exists but is not initialized.");
                            break;

                        case SchemaStatus.ExistsWithSchema:

                            Log.LogInfo($"[{KubeConst.NeonClusterOperatorDatabase}] database exists with [version={status.Version}].");
                            break;

                        case SchemaStatus.NotFound:

                            Log.LogInfo($"[{KubeConst.NeonClusterOperatorDatabase}] database does not exist.");
                            await schemaManager.CreateDatabaseAsync();
                            break;

                        case SchemaStatus.Updating:

                            throw new SchemaManagerException($"[{KubeConst.NeonClusterOperatorDatabase}] database is currently being updated by [updater={status.Updater}].");

                        case SchemaStatus.UpgradeError:

                            throw new SchemaManagerException($"[{KubeConst.NeonClusterOperatorDatabase}] database is in an inconsistent state due to a previous update failure [updater={status.Updater}] [error={status.Error}].  This will require manual intervention.");

                        default:

                            throw new NotImplementedException();
                    }

                    var version = await schemaManager.UpgradeDatabaseAsync();

                    if (version == status.Version)
                    {
                        Log.LogInfo($"[{KubeConst.NeonClusterOperatorDatabase}] database is up to date at [version={version}]");
                    }
                    else
                    {
                        Log.LogInfo($"[{KubeConst.NeonClusterOperatorDatabase}] database is upgraded from [version={status.Version}] to [version={version}].");
                    }
                }
            }
            DbConnectionString = await GetConnectionStringAsync(KubeConst.NeonClusterOperatorDatabase);

        }

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
                timeout:      TimeSpan.FromMinutes(30),
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
                timeout:      TimeSpan.FromMinutes(30),
                pollInterval: TimeSpan.FromSeconds(10));

            Log.LogInfo($"[{KubeNamespaces.NeonSystem}-db] {KubeNamespaces.NeonSystem} database is ready.");
        }
    }
}