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
        /// <param name="imagesOnly">Optionally indicate that only cluster service and container images should be updated.</param>
        /// <param name="serviceUpdateParallism">Optionally specifies the parallism to use when updating services.</param>
        /// <exception cref="ClusterException">Thrown if there was an error selecting the updates.</exception>
        public static void AddUpdateSteps(ClusterProxy cluster, SetupController<NodeDefinition> controller, bool imagesOnly = false, int serviceUpdateParallism = 1)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            // Obtain and parse the current cluster version.

            if (!cluster.Globals.TryGetString(NeonClusterGlobals.NeonCliVersion, out var versionString) ||
                !SemanticVersion.TryParse(versionString, out var clusterVersion))
            {
                throw new ClusterException($"Unable to retrieve or parse the cluster version global [{NeonClusterGlobals.NeonCliVersion}].");
            }

            if (!imagesOnly)
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
                            update.Cluster = cluster;
                            nextVersion    = update.ToVersion;

                            update.AddUpdateSteps(controller);
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

            var knownContainers = new List<string>()
            {
                "neon-log-host",
                "neon-log-metricbeat",
                "neon-registry-cache"
            };

            var knownServices = new List<string>()
            {
                "neon-cluster-manager",
                "neon-dns",
                "neon-dns-mon",
                "neon-log-collector",
                "neon-log-kibana",
                "neon-proxy-manager",
                "neon-proxy-private",
                "neon-proxy-public",
                "neon-proxy-vault"
            };

            var firstManager = cluster.FirstManager;

            if (cluster.Definition.Docker.RegistryCache)
            {
                controller.AddGlobalStep("pull images to cache",
                    () =>
                    {
                        foreach (var container in knownContainers)
                        {
                            firstManager.SudoCommand($"docker pull {container}:latest");
                        }

                        foreach (var service in knownServices)
                        {
                            firstManager.SudoCommand($"docker pull {service}:latest");
                        }
                    });

                controller.AddStep("pull images to node", 
                    (node, stepDelay) =>
                    {
                        foreach (var container in knownContainers)
                        {
                            node.SudoCommand($"docker pull {container}:latest");
                        }

                        foreach (var service in knownServices)
                        {
                            node.SudoCommand($"docker pull {service}:latest");
                        }
                    },
                    node => node != firstManager);
            }
            else
            {
                controller.AddStep("pull images to node", 
                    (node, stepDelay) =>
                    {
                        foreach (var container in knownContainers)
                        {
                            node.SudoCommand($"docker pull {container}:latest");
                        }

                        foreach (var service in knownServices)
                        {
                            node.SudoCommand($"docker pull {service}:latest");
                        }
                    });
            }

            controller.AddStep("update services",
                (node, stepDelay) =>
                {
                    // List the neonCLUSTER services actually running and only update those.

                    foreach (var service in knownServices)
                    {
                        node.SudoCommand($"docker service update --image {service}:latest --max-parallelism {serviceUpdateParallism} {service}");
                    }
                },
                node => node == firstManager);

            controller.AddStep("update containers",
                (node, stepDelay) =>
                {
                    // List the neonCLUSTER containers actually running and only update those.
                });
        }
    }
}
