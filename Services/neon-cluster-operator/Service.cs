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
using Neon.Postgres;

using Helm.Helm;
using Newtonsoft.Json;
using YamlDotNet.RepresentationModel;

using k8s;
using k8s.Models;
using Npgsql;
using System.Reflection;

namespace NeonClusterOperator
{
    public partial class Service : NeonService
    {
        private const string StateTable = "state";
        
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
            // Let KubeService know that we're running.

            await SetRunningAsync();

            // Wait for Citus and make sure it's initialized.
            await WaitForCitusAsync();
            await WaitForMinioAsync();
            await InitializeDatabaseAsync();

            // Initialize Grafana and Harbor.
            await SetupGrafanaAsync();
            await SetupHarborAsync();

            // Launch the sub-tasks.  These will run until the service is terminated.

            while (true)
            {
                await Task.Delay(5000);
                
            }

            //return 0;
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

        /// <summary>
        /// Gets the connection string used to connect to the neon-system database.
        /// </summary>
        /// <returns></returns>
        private async Task InitializeDatabaseAsync()
        {
            Log.LogInfo($"Connecting to citus...");

            var connString = await GetConnectionStringAsync();
            //connString = $"Host=10.100.32.254;Port=30123;Username={username};Password={password};Database={KubeConst.NeonClusterOperatorDatabase}";

            var schemaDirectory = Assembly.GetExecutingAssembly().GetResourceFileSystem("NeonClusterOperator.Schema");

            var serviceUserSecret = await k8s.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbServiceSecret, KubeNamespaces.NeonSystem);

            var variables = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
                { "database", KubeConst.NeonClusterOperatorDatabase },
                { "service_user", Encoding.UTF8.GetString(serviceUserSecret.Data["username"]) },
                { "service_password", Encoding.UTF8.GetString(serviceUserSecret.Data["password"]) },
                { "state_table", StateTable }
            };

            await using (NpgsqlConnection conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();

                using (var schemaManager = new SchemaManager(conn, KubeConst.NeonClusterOperatorDatabase, schemaDirectory, variables))
                {
                    var status = await schemaManager.GetStatusAsync();
                    var message = (string)null;

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

                            message = $"[{KubeConst.NeonClusterOperatorDatabase}] database is currently being updated by [updater={status.Updater}].";

                            Log.LogWarn(message);
                            throw new SchemaManagerException(message);

                        case SchemaStatus.UpgradeError:

                            message = $"[{KubeConst.NeonClusterOperatorDatabase}] database is in an inconsistent state due to a previous update failure [updater={status.Updater}] [error={status.Error}].  This will require manual intervention.";

                            Log.LogError(message);
                            throw new SchemaManagerException(message);

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

        /// <summary>
        /// Method to wait for neon-system minio to be ready.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task WaitForMinioAsync()
        {
            Log.LogInfo($"[{KubeNamespaces.NeonSystem}-minio] Waiting for Minio to be ready.");

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    var statefulsets = await k8s.ListNamespacedStatefulSetAsync(KubeNamespaces.NeonSystem, labelSelector: "app=minio");

                    return statefulsets.Items.All(@set => @set.Status.ReadyReplicas == @set.Spec.Replicas);
                },
                timeout: TimeSpan.FromMinutes(30),
                pollInterval: TimeSpan.FromSeconds(10));

            Log.LogInfo($"[{KubeNamespaces.NeonSystem}-minio] Minio is ready.");
        }

        /// <summary>
        /// Deploys a Kubernetes job that runs Grafana setup.
        /// </summary>
        /// <returns></returns>
        public async Task SetupGrafanaAsync()
        {
            var connString = await GetConnectionStringAsync(KubeConst.NeonClusterOperatorDatabase);

            await using (NpgsqlConnection conn = new NpgsqlConnection(connString))
            {
                conn.Open();
                await using (NpgsqlCommand cmd = new NpgsqlCommand($"SELECT value FROM {StateTable} WHERE key='{KubeConst.NeonJobSetupHarbor}'", conn))
                {
                    var result = await cmd.ExecuteScalarAsync();
                    if ((string)result != "complete")
                    {
                        Log.LogInfo($"Grafana setup incomplete [{(string)result}].");

                        var jobs = await k8s.ListNamespacedJobAsync(KubeNamespaces.NeonSystem);

                        if (!jobs.Items.Any(j => j.Metadata.Name == KubeConst.NeonJobSetupGrafana))
                        {
                            Log.LogInfo($"Creating Grafana setup job.");

                            await k8s.CreateNamespacedJobAsync(
                                new V1Job()
                                {
                                    Metadata = new V1ObjectMeta()
                                    {
                                        Name = KubeConst.NeonJobSetupGrafana,
                                        NamespaceProperty = KubeNamespaces.NeonSystem
                                    },
                                    Spec = new V1JobSpec()
                                    {
                                        TtlSecondsAfterFinished = 100,
                                        Template = new V1PodTemplateSpec()
                                        {
                                            Spec = new V1PodSpec()
                                            {
                                                Containers = new List<V1Container>()
                                                {
                                                    new V1Container()
                                                    {
                                                        Name  = KubeConst.NeonJobSetupGrafana,
                                                        Image = $"{KubeConst.LocalClusterRegistry}/neon-setup-grafana:neonkube-{KubeConst.NeonKubeVersion}"
                                                    },
                                                },
                                                RestartPolicy = "OnFailure",
                                                ServiceAccount = NeonServices.ClusterOperator,
                                                ServiceAccountName = NeonServices.ClusterOperator
                                            },
                                        },
                                        BackoffLimit = 5,
                                    },
                                },
                                KubeNamespaces.NeonSystem);

                            Log.LogInfo($"Created Grafana setup job.");
                        }
                        else
                        {
                            Log.LogInfo($"Grafana setup job is running.");
                        }

                        var job = await k8s.ReadNamespacedJobAsync(KubeConst.NeonJobSetupGrafana, KubeNamespaces.NeonSystem);

                        while (job.Status.Succeeded < 1)
                        {
                            await Task.Delay(1000);
                            job = await k8s.ReadNamespacedJobAsync(KubeConst.NeonJobSetupGrafana, KubeNamespaces.NeonSystem);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Deploys a Kubernetes job that runs Harbor setup.
        /// </summary>
        /// <returns></returns>
        public async Task SetupHarborAsync()
        {
            var connString = await GetConnectionStringAsync(KubeConst.NeonClusterOperatorDatabase);

            await using (NpgsqlConnection conn = new NpgsqlConnection(connString))
            {
                conn.Open();
                await using (NpgsqlCommand cmd = new NpgsqlCommand($"SELECT value FROM {StateTable} WHERE key='{KubeConst.NeonJobSetupHarbor}'", conn))
                {
                    var result = await cmd.ExecuteScalarAsync();
                    if ((string)result != "complete")
                    {
                        Log.LogInfo($"Harbor setup incomplete [{(string)result}].");

                        var jobs = await k8s.ListNamespacedJobAsync(KubeNamespaces.NeonSystem);

                        if (!jobs.Items.Any(j => j.Metadata.Name == KubeConst.NeonJobSetupHarbor))
                        {
                            Log.LogInfo($"Creating Harbor setup job.");

                            await k8s.CreateNamespacedJobAsync(
                                new V1Job()
                                {
                                    Metadata = new V1ObjectMeta()
                                    {
                                        Name = KubeConst.NeonJobSetupHarbor,
                                        NamespaceProperty = KubeNamespaces.NeonSystem
                                    },
                                    Spec = new V1JobSpec()
                                    {
                                        TtlSecondsAfterFinished = 100,
                                        Template = new V1PodTemplateSpec()
                                        {
                                            Spec = new V1PodSpec()
                                            {
                                                Containers = new List<V1Container>()
                                                {
                                                    new V1Container()
                                                    {
                                                        Name  = KubeConst.NeonJobSetupHarbor,
                                                        Image = $"{KubeConst.LocalClusterRegistry}/neon-setup-harbor:neonkube-{KubeConst.NeonKubeVersion}"
                                                    },
                                                },
                                                RestartPolicy = "OnFailure",
                                                ServiceAccount = NeonServices.ClusterOperator,
                                                ServiceAccountName = NeonServices.ClusterOperator
                                            },
                                        },
                                        BackoffLimit = 5,
                                    },
                                },
                                KubeNamespaces.NeonSystem);
                            Log.LogInfo($"Created Harbor setup job.");
                        }
                        else
                        {
                            Log.LogInfo($"Harbor setup job is running.");
                        }

                        var job = await k8s.ReadNamespacedJobAsync(KubeConst.NeonJobSetupHarbor, KubeNamespaces.NeonSystem);

                        while (job.Status.Succeeded < 1)
                        {
                            await Task.Delay(1000);
                            job = await k8s.ReadNamespacedJobAsync(KubeConst.NeonJobSetupHarbor, KubeNamespaces.NeonSystem);
                        }
                    }
                }
            }
        }
    }
}