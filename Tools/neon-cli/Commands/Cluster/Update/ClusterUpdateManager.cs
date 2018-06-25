//-----------------------------------------------------------------------------
// FILE:	    ClusterUpdateManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Cluster;
using Neon.Common;
using Neon.IO;

namespace NeonCli
{
    /// <summary>
    /// Manages the available cluster updates.
    /// </summary>
    public static class ClusterUpdateManager
    {
        /// <summary>
        /// Static constuctor.
        /// </summary>
        static ClusterUpdateManager()
        {
            Updates = new List<IClusterUpdate>()
            {
                new Update_010297_010298()
            };
        }

        /// <summary>
        /// Returns the available cluster updates (in no particular order).
        /// </summary>
        public static IEnumerable<IClusterUpdate> Updates { get; private set; }

        /// <summary>
        /// Scans the cluster and adds the steps to a <see cref="SetupController"/> required
        /// to update the cluster to the most recent version.
        /// </summary>
        /// <param name="cluster">The target cluster proxy.</param>
        /// <param name="controller">The setup controller.</param>
        /// <param name="servicesOnly">Optionally indicate that only cluster service and container images should be updated.</param>
        /// <param name="serviceUpdateParallism">Optionally specifies the parallism to use when updating services.</param>
        /// <returns>The number of pending updates.</returns>
        /// <exception cref="ClusterException">Thrown if there was an error selecting the updates.</exception>
        public static int AddHiveUpdateSteps(ClusterProxy cluster, SetupController<NodeDefinition> controller, bool servicesOnly = false, int serviceUpdateParallism = 1)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            var pendingUpdateCount = 0;

            // Obtain and parse the current cluster version.

            if (!SemanticVersion.TryParse(cluster.Globals.Version, out var clusterVersion))
            {
                throw new ClusterException($"Unable to retrieve or parse the cluster version global [{NeonClusterGlobals.NeonCliVersion}].");
            }

            if (!servicesOnly)
            {
                // Scan for the first update that applies.

                var firstUpdate = Updates
                    .Where(u => u.FromVersion >= clusterVersion)
                    .OrderBy(u => u.FromVersion)
                    .FirstOrDefault();

                if (firstUpdate != null)
                {
                    // Determine which updates apply.  We're going to sort the available updates
                    // in ascending order by [FromVersion] and then in decending order by [ToVersion]
                    // to favor overlapping updates that advance the cluster the most.

                    var nextVersion = firstUpdate.FromVersion;

                    foreach (var update in Updates
                        .OrderBy(u => u.FromVersion)
                        .ThenByDescending(u => u.ToVersion))
                    {
                        if (update.FromVersion >= nextVersion)
                        {
                            pendingUpdateCount++;

                            update.Cluster = cluster;
                            nextVersion = update.ToVersion;

                            if (!servicesOnly)
                            {
                                update.AddUpdateSteps(controller);
                            }
                        }
                    }
                }
            }

            // $todo(jeff.lill):
            //
            // We're going to hack this for now by updating all known neonCLUSTER service
            // and container images to the [:latest] tag.  This is not really correct for
            // these reasons:
            //
            //      1. Clusters may have been deployed using images from another branch
            //         like [jeff-latest].  This implementation will replace these with
            //         [latest].  Perhaps we should add a cluster global that identifies
            //         the branch.  This may become more important if we make the concepts
            //         of DEV and EDGE (and perhaps ENTERPRISE) releases public.
            //
            //         This also doesn't support obtaining images from other registries
            //         or with image names that don't match the service/container name.
            //         This could be a problem for air-gapped or other cluster configs
            //         in the future.
            //
            //      2. It's possible that specific versions of the cluster might require
            //         images to be pinned to a specific version.
            //
            //      3. The service/container create scripts deployed the the nodes are
            //         not being updated resulting in potential inconsistencies.
            //
            //      4. We should really be querying a service that returns the images
            //         required for a specific cluster version and then pin the containers
            //         and services to specific image tags, perhaps by HASH.
            //
            //      5. Once we've done #4, we should scan cluster containers and services
            //         to determine whether they actually need to be updated.  The code
            //         below simply add steps to update everything.
            //
            //      6. We're going to update containers by stopping and removing them,
            //         then we'll pull the new image and then use the container setup 
            //         script as is to relaunch the container.  This assumes that the
            //         container script uses the [:latest] tag.
            //
            // We're currently using a stubbed implementation of neonHIVE headend
            // services to implement some of this.

            var versions         = cluster.Headend.GetComponentVersions(cluster.Globals.Version);
            var systemContainers = NeonClusterConst.DockerContainers;
            var systemServices   = NeonClusterConst.DockerServices;
            var firstManager     = cluster.FirstManager;

            if (cluster.Definition.Docker.RegistryCache)
            {
                controller.AddGlobalStep("pull images to cache",
                    () =>
                    {
                        foreach (var container in systemContainers)
                        {
                            if (!versions.Images.TryGetValue(container, out var image))
                            {
                                firstManager.LogLine($"WARNING: Could not resolve [{container}] to a specific image.");
                                continue;
                            }

                            firstManager.Status = $"run: docker pull {image}";
                            firstManager.SudoCommand($"docker pull {image}");
                            firstManager.Status = string.Empty;
                        }

                        foreach (var service in systemServices)
                        {
                            if (!versions.Images.TryGetValue(service, out var image))
                            {
                                firstManager.LogLine($"WARNING: Could not resolve [{service}] to a specific image.");
                                continue;
                            }

                            firstManager.Status = $"run: docker pull {image}";
                            firstManager.SudoCommand($"docker pull {image}");
                            firstManager.Status = string.Empty;
                        }
                    });
            }

            controller.AddStep("update services",
                (node, stepDelay) =>
                {
                    // List the neonCLUSTER services actually running and only update those.

                    var services = new HashSet<string>();
                    var response = node.SudoCommand("docker service ls --format \"{{.Name}}\"");

                    using (var reader = new StringReader(response.OutputText))
                    {
                        foreach (var service in reader.Lines())
                        {
                            services.Add(service);
                        }
                    }

                    foreach (var service in systemServices.Where(s => services.Contains(s)))
                    {
                        if (!versions.Images.TryGetValue(service, out var image))
                        {
                            firstManager.LogLine($"WARNING: Could not resolve [{service}] to a specific image.");
                            continue;
                        }

                        firstManager.Status = $"update: {image}";
                        node.SudoCommand($"docker service update --image {image} --max-parallelism {serviceUpdateParallism} {service}");
                        firstManager.Status = string.Empty;
                    }
                },
                node => node == firstManager);

            controller.AddStep("update containers",
                (node, stepDelay) =>
                {
                    // We'll honor the step delay if the cluster doesn't have a registry cache
                    // so we don't overwhelm the network when pulling images to the nodes.

                    if (!cluster.Definition.Docker.RegistryCache)
                    {
                        Thread.Sleep(stepDelay);
                    }

                    // List the neonCLUSTER containers actually running and only update those.
                    // Note that we're going to use the local script to start the container
                    // so we don't need to hardcode the Docker options here.  We won't restart
                    // the container if the script doesn't exist.
                    //
                    // Note that we'll update and restart the containers in parallel if the
                    // cluster has a local registry, otherwise we'll just go with the user
                    // specified parallelism to avoid overwhelming the network with image
                    // downloads.

                    // $todo(jeff.lill):
                    //
                    // A case could be made for having a central place for generating container
                    // (and service) scripts for cluster setup as well as situations like this.
                    // It could also be possible then to be able to scan for and repair missing
                    // or incorrect scripts.

                    var containers = new HashSet<string>();
                    var response   = node.SudoCommand("docker ps --format \"{{.Names}}\"");

                    using (var reader = new StringReader(response.OutputText))
                    {
                        foreach (var container in reader.Lines())
                        {
                            containers.Add(container);
                        }
                    }

                    foreach (var container in systemContainers.Where(s => containers.Contains(s)))
                    {
                        if (!versions.Images.TryGetValue(container, out var image))
                        {
                            firstManager.LogLine($"WARNING: Could not resolve [{container}] to a specific image.");
                            continue;
                        }

                        var containerStartScriptPath = LinuxPath.Combine(NeonHostFolders.Scripts, $"{container}.sh");

                        if (node.FileExists(containerStartScriptPath))
                        {
                            // The container has a creation script, so pull the image and then
                            // restart the container.

                            // $hack(jeff.lill): I'm baking in the image tag here as ":latest"

                            node.Status = $"pull: {image}";
                            node.DockerCommand("docker", "pull", image);

                            node.Status = $"stop: {container}";
                            node.DockerCommand("docker", "rm", "--force", container);

                            node.Status = $"restart: {container}";
                            node.SudoCommand("bash", containerStartScriptPath);
                        }
                        else
                        {
                            node.Status = $"WARNING: Container script [{containerStartScriptPath}] is not present so we can't update the [{container}] container.";
                            node.Log($"WARNING: Container script [{containerStartScriptPath}] is not present on this node so we can't update the [{container}] container.");
                            Thread.Sleep(TimeSpan.FromSeconds(5));
                        }
                    }
                },
                noParallelLimit: cluster.Definition.Docker.RegistryCache);

            return pendingUpdateCount;
        }

        /// <summary>
        /// Scans the cluster and adds the steps to a <see cref="SetupController"/> required
        /// to update the cluster to the most recent version.
        /// </summary>
        /// <param name="cluster">The target cluster proxy.</param>
        /// <param name="controller">The setup controller.</param>
        /// <param name="dockerVersion">The version of Docker required.</param>
        /// <returns>The number of pending updates.</returns>
        /// <exception cref="ClusterException">Thrown if there was an error selecting the updates.</exception>
        /// <remarks>
        /// <note>
        /// This method does not allow an older version of the component to be installed.
        /// In this case, the current version will remain.
        /// </note>
        /// </remarks>
        public static void AddDockerUpdateSteps(ClusterProxy cluster, SetupController<NodeDefinition> controller, string dockerVersion)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(dockerVersion));

            var newVersion   = (SemanticVersion)dockerVersion;
            var pendingNodes = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var package      = cluster.Headend.GetDockerPackage(dockerVersion, out var message);

            // Update the managers first.

            pendingNodes.Clear();
            foreach (var node in cluster.Managers)
            {
                if ((SemanticVersion)node.GetDockerVersion() < newVersion)
                {
                    pendingNodes.Add(node.Name);
                }
            }

            if (pendingNodes.Count > 0)
            {
                controller.AddStep("managers: update docker",
                    (node, stepDelay) =>
                    {
                        Thread.Sleep(stepDelay);
                        UpdateDocker(cluster, node, package);
                    },
                    n => pendingNodes.Contains(n.Name),
                    parallelLimit: 1);
            }

            // Update the workers.

            pendingNodes.Clear();
            foreach (var node in cluster.Workers)
            {
                if ((SemanticVersion)node.GetDockerVersion() < newVersion)
                {
                    pendingNodes.Add(node.Name);
                }
            }

            if (pendingNodes.Count > 0)
            {
                controller.AddStep("workers: update docker",
                    (node, stepDelay) =>
                    {
                        Thread.Sleep(stepDelay);
                        UpdateDocker(cluster, node, package);
                    },
                    n => pendingNodes.Contains(n.Name),
                    parallelLimit: 1);
            }

            // Update the pets.

            pendingNodes.Clear();
            foreach (var node in cluster.Pets)
            {
                if ((SemanticVersion)node.GetDockerVersion() < newVersion)
                {
                    pendingNodes.Add(node.Name);
                }
            }

            if (pendingNodes.Count > 0)
            {
                controller.AddStep("workers: update docker",
                    (node, stepDelay) =>
                    {
                        Thread.Sleep(stepDelay);
                        UpdateDocker(cluster, node, package);
                    },
                    n => pendingNodes.Contains(n.Name),
                    parallelLimit: 1);
            }
        }

        /// <summary>
        /// Updates docker on a cluster node.
        /// </summary>
        /// <param name="cluster">The target cluster.</param>
        /// <param name="node">The target node.</param>
        /// <param name="package">The fully qualified Docker Debian poackage name.</param>
        private static void UpdateDocker(ClusterProxy cluster, SshProxy<NodeDefinition> node, string package)
        {
            if (node.Metadata.InSwarm)
            {
                node.Status = "swarm: drain services";
                cluster.Docker.DrainNode(node.Name);
            }

            node.Status = "stop: docker";
            node.SudoCommand("systemctl stop docker");

            node.Status = "update: docker";
            node.SudoCommand("safe-apt-get update");

            var failed = node.SudoCommand($"safe-apt-get install -yq {package}", RunOptions.LogOutput).ExitCode != 0;

            node.Status = "restart: docker";
            node.SudoCommand("systemctl start docker");

            if (node.Metadata.InSwarm)
            {
                node.Status = "swarm: activate";
                cluster.Docker.ActivateNode(node.Name);
            }

            if (failed)
            {
                node.Fault("[docker] update failed.");
            }
        }
    }
}
