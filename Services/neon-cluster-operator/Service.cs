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
using Newtonsoft.Json;
using YamlDotNet.RepresentationModel;

using k8s;
using k8s.Models;
using Npgsql;

namespace NeonClusterOperator
{
    public partial class Service : NeonService
    {
        private const string StateTable = "state";
        
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
            await ConnectDatabaseAsync();

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
        /// Gets the connection string used to connect to the neon-system database.
        /// </summary>
        /// <returns></returns>
        private async Task ConnectDatabaseAsync()
        {
            Log.LogInfo($"Connecting to citus...");

            var secret = await k8s.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbAdminSecret, KubeNamespaces.NeonSystem);

            var username = Encoding.UTF8.GetString(secret.Data["username"]);
            var password = Encoding.UTF8.GetString(secret.Data["password"]);

            var dbHost = $"db-citus-postgresql.{KubeNamespaces.NeonSystem}";

            connString = $"Host={dbHost};Username={username};Password={password};Database={KubeConst.NeonClusterOperatorDatabase}";

            await using (NpgsqlConnection conn = new NpgsqlConnection(connString))
            {
                conn.Open();

                Log.LogInfo($"Connected.");

                await using (var createTableCmd = new NpgsqlCommand($@"
SELECT
1
FROM
information_schema.tables
WHERE
table_schema = 'public'
AND table_name = '{StateTable}'
", conn))
                {
                    if (createTableCmd.ExecuteScalar() == null)
                    {
                        Log.LogInfo($"State table doesn't exist, creating...");

                        await using (var createDbCmd = new NpgsqlCommand($@"
CREATE TABLE {StateTable}( KEY TEXT, value TEXT, PRIMARY KEY(KEY) )
", conn))
                        {
                            await createDbCmd.ExecuteNonQueryAsync();
                            Log.LogInfo($"State table created.");
                        }
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

        public async Task SetupGrafanaAsync()
        {
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
                                        Template = new V1PodTemplateSpec()
                                        {
                                            Spec = new V1PodSpec()
                                            {
                                                Containers = new List<V1Container>()
                                                {
                                                    new V1Container()
                                                    {
                                                        Name  = KubeConst.NeonJobSetupGrafana,
                                                        Image = $"ghcr.io/neonkube-dev/neon-setup-grafana:latest"
                                                    },
                                                },
                                                RestartPolicy = "OnFailure",
                                                ServiceAccount = NeonServices.ClusterOperator,
                                                ServiceAccountName = NeonServices.ClusterOperator
                                            },
                                        },
                                    },
                                },
                                KubeNamespaces.NeonSystem);

                            Log.LogInfo($"Created Grafana setup job.");
                        }
                        else
                        {
                            Log.LogInfo($"Grafana setup job is running.");
                        }
                    }
                    }
                }
        }

        public async Task SetupHarborAsync()
        {
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
                                        Template = new V1PodTemplateSpec()
                                        {
                                            Spec = new V1PodSpec()
                                            {
                                                Containers = new List<V1Container>()
                                                {
                                                    new V1Container()
                                                    {
                                                        Name  = KubeConst.NeonJobSetupHarbor,
                                                        Image = $"ghcr.io/neonkube-dev/neon-setup-harbor:latest"
                                                    },
                                                },
                                                RestartPolicy = "OnFailure",
                                                ServiceAccount = NeonServices.ClusterOperator,
                                                ServiceAccountName = NeonServices.ClusterOperator
                                            },
                                        },
                                    },
                                },
                                KubeNamespaces.NeonSystem);
                            Log.LogInfo($"Created Harbor setup job.");
                        }
                        else
                        {
                            Log.LogInfo($"Harbor setup job is running.");
                        }
                    }
                }
            }
        }
    }
}
