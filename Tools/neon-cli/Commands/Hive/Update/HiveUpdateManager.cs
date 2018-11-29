//-----------------------------------------------------------------------------
// FILE:	    HiveUpdateManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Manages the available hive updates.
    /// </summary>
    public static class HiveUpdateManager
    {
        /// <summary>
        /// Static constuctor.
        /// </summary>
        static HiveUpdateManager()
        {
            // Reflect the current assembly to locate any update classes.

            var updateList = new List<IHiveUpdate>();

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.GetCustomAttribute<HiveUpdateAttribute>() != null)
                {
                    if (!type.Implements<IHiveUpdate>())
                    {
                        throw new TypeLoadException($"Type [{type.FullName}] is not a valid update class because it doesn't implement [{nameof(IHiveUpdate)}].");
                    }

                    var constructor = type.GetConstructor(new Type[0]);

                    if (constructor == null)
                    {
                        throw new TypeLoadException($"Type [{type.FullName}] is not a valid update class because it doesn't have a default constructor.");
                    }

                    updateList.Add((IHiveUpdate)constructor.Invoke(new object[0]));
                }
            }

            Updates = updateList;
        }

        /// <summary>
        /// Returns the available hive updates (in no particular order).
        /// </summary>
        public static IEnumerable<IHiveUpdate> Updates { get; private set; }

        /// <summary>
        /// Scans the hive and adds the steps to a <see cref="SetupController{NodeMetadata}"/> required
        /// to update the hive to the most recent version.
        /// </summary>
        /// <param name="hive">The target hive proxy.</param>
        /// <param name="controller">The setup controller.</param>
        /// <param name="restartRequired">Returns as <c>true</c> if one or more cluster nodes will be restarted during the update.</param>
        /// <param name="servicesOnly">Optionally indicate that only hive service and container images should be updated.</param>
        /// <param name="serviceUpdateParallism">Optionally specifies the parallism to use when updating services.</param>
        /// <param name="imageTag">Optionally overrides the default image tag.</param>
        /// <returns>The number of pending updates.</returns>
        /// <exception cref="HiveException">Thrown if there was an error selecting the updates.</exception>
        public static int AddHiveUpdateSteps(HiveProxy hive, SetupController<NodeDefinition> controller, out bool restartRequired, bool servicesOnly = false, int serviceUpdateParallism = 1, string imageTag = null)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            restartRequired = false;

            var pendingUpdateCount = 0;

            // Obtain and parse the current hive version.

            if (!SemanticVersion.TryParse(hive.Globals.Version, out var hiveVersion))
            {
                throw new HiveException($"Unable to retrieve or parse the hive version global [{HiveGlobals.Version}].");
            }

            if (!servicesOnly)
            {
                // Scan for the first update that applies.

                var firstUpdate = Updates
                    .Where(u => u.FromVersion >= hiveVersion)
                    .OrderBy(u => u.FromVersion)
                    .FirstOrDefault();

                if (firstUpdate != null)
                {
                    // Determine which updates apply.  We're going to sort the available updates
                    // in ascending order by [FromVersion] and then in decending order by [ToVersion]
                    // to favor overlapping updates that advance the hive the most.

                    var nextVersion = firstUpdate.FromVersion;

                    foreach (var update in Updates
                        .OrderBy(u => u.FromVersion)
                        .ThenByDescending(u => u.ToVersion))
                    {
                        if (update.FromVersion >= nextVersion)
                        {
                            pendingUpdateCount++;

                            update.Hive = hive;
                            nextVersion = update.ToVersion;

                            if (!servicesOnly)
                            {
                                update.AddUpdateSteps(controller);

                                if (update.RestartRequired)
                                {
                                    restartRequired = true;
                                }
                            }
                        }
                    }
                }
            }

            var componentInfo    = hive.Headend.GetComponentInfo(hive.Globals.Version, ThisAssembly.Git.Branch);
            var systemContainers = HiveConst.DockerContainers;
            var systemServices   = HiveConst.DockerServices;
            var firstManager     = hive.FirstManager;

            if (hive.Definition.Docker.RegistryCache)
            {
                controller.AddGlobalStep("pull images to cache",
                    () =>
                    {
                        foreach (var container in systemContainers)
                        {
                            var image = GetUpdateImage(hive, componentInfo, container, imageTag);

                            if (image != null)
                            {
                                firstManager.Status = $"run: docker pull {image}";
                                firstManager.SudoCommand($"docker pull {image}");
                                firstManager.Status = string.Empty;
                            }
                        }

                        foreach (var service in systemServices)
                        {
                            var image = GetUpdateImage(hive, componentInfo, service, imageTag);

                            if (image != null)
                            {
                                firstManager.Status = $"run: docker pull {image}";
                                firstManager.SudoCommand($"docker pull {image}");
                                firstManager.Status = string.Empty;
                            }
                        }
                    });
            }

            controller.AddStep("update services",
                (node, stepDelay) =>
                {
                    // List the neonHIVE services actually running and only update those.

                    var runningServices = new HashSet<string>();
                    var response        = node.SudoCommand("docker service ls --format \"{{.Name}}\"");

                    using (var reader = new StringReader(response.OutputText))
                    {
                        foreach (var service in reader.Lines())
                        {
                            runningServices.Add(service);
                        }
                    }

                    foreach (var service in systemServices.Where(s => runningServices.Contains(s)))
                    {
                        var image = GetUpdateImage(hive, componentInfo, service, imageTag);

                        if (image != null)
                        {
                            // $todo(jeff.lill):
                            //
                            // We should check the service image to see if we actually need to perform an
                            // upgrade.  There's no point in restarting the service instances unnecessarily.
                            //
                            //      https://github.com/jefflill/NeonForge/issues/378

                            firstManager.Status = $"update: {image}";
                            node.SudoCommand($"docker service update --force --image {image} --update-parallelism {serviceUpdateParallism} {service}");
                            firstManager.Status = string.Empty;

                            // Update the service creation scripts on all manager nodes for all built-in 
                            // services.  Note that this depends on how [ServicesBase.CreateStartScript()]
                            // formatted the generated code at the top of the script.

                            foreach (var manager in hive.Managers)
                            {
                                UpdateStartScript(manager, service, $"{image}");
                            }
                        }
                    }
                },
                node => node == firstManager);

            controller.AddGlobalStep("update containers",
                () =>
                {
                    // $todo(jeff.lill):
                    //
                    // We should check the service image to see if we actually need to perform an
                    // upgrade.  There's no point in restarting the service instances unnecessarily.
                    //
                    //      https://github.com/jefflill/NeonForge/issues/378

                    // We're going to update containers on each node, one node at a time
                    // and then stablize for a period of time before moving on to the 
                    // next node.  This will help keep clustered applications like HiveMQ
                    // and databases like Couchbase that are deployed as containers happy
                    // by not blowing all of the application instances away at the same
                    // time while updating.
                    //
                    // Hopefully, there will be enough time after updating a clustered
                    // application container for the container to rejoin the cluster
                    // before we update the next node.

                    foreach (var node in hive.Nodes)
                    {
                        // List the neonHIVE containers actually running and only update those.
                        // Note that we're going to use the local script to start the container
                        // so we don't need to hardcode the Docker options here.  We won't restart
                        // the container if the script doesn't exist.
                        //
                        // Note that we'll update and restart the containers in parallel if the
                        // hive has a local registry, otherwise we'll just go with the user
                        // specified parallelism to avoid overwhelming the network with image
                        // downloads.

                        // $todo(jeff.lill):
                        //
                        // A case could be made for having a central place for generating container
                        // (and service) scripts for hive setup as well as situations like this.
                        // It could also be possible then to be able to scan for and repair missing
                        // or incorrect scripts.

                        var runningContainers = new HashSet<string>();
                        var response          = node.SudoCommand("docker ps --format \"{{.Names}}\"");

                        using (var reader = new StringReader(response.OutputText))
                        {
                            foreach (var container in reader.Lines())
                            {
                                runningContainers.Add(container);
                            }
                        }

                        foreach (var container in systemContainers.Where(s => runningContainers.Contains(s)))
                        {
                            var image = GetUpdateImage(hive, componentInfo, container, imageTag);

                            if (image != null)
                            {
                                var scriptPath = LinuxPath.Combine(HiveHostFolders.Scripts, $"{container}.sh");

                                if (node.FileExists(scriptPath))
                                {
                                    // The container has a creation script, so update the script, stop/remove the
                                    // container and then run the script to restart the container.

                                    UpdateStartScript(node, container, $"{image}");

                                    node.Status = $"stop: {container}";
                                    node.DockerCommand("docker", "rm", "--force", container);

                                    node.Status = $"restart: {container}";
                                    node.SudoCommand(scriptPath);
                                }
                                else
                                {
                                    var warning = $"WARNING: Container script [{scriptPath}] is not present on this node so we can't update the [{container}] container.";

                                    node.Status = warning;
                                    node.Log(warning);
                                    Thread.Sleep(TimeSpan.FromSeconds(5));
                                }
                            }
                        }

                        node.Status = $"stablizing ({Program.WaitSeconds}s)";
                        Thread.Sleep(TimeSpan.FromSeconds(Program.WaitSeconds));
                        node.Status = "READY";
                    }
                });

            return pendingUpdateCount;
        }

        /// <summary>
        /// Returns the fully qualified image name required to upgrade a container or service. 
        /// </summary>
        /// <param name="hive">The hive proxy.</param>
        /// <param name="componentInfo">The hive component version information.</param>
        /// <param name="componentName">The service or container name.</param>
        /// <param name="imageTag"></param>
        /// <returns>The fully qualified image or <c>null</c> if there is no known image for the service or container.</returns>
        private static string GetUpdateImage(HiveProxy hive, HiveComponentInfo componentInfo, string componentName, string imageTag = null)
        {
            if (!componentInfo.ComponentToImage.TryGetValue(componentName, out var imageName))
            {
                hive.FirstManager.LogLine($"WARNING: Cannot map service or container named [{componentName}] to an image.");
                return null;
            }

            if (!componentInfo.ImageToFullyQualified.TryGetValue(imageName, out var image))
            {
                hive.FirstManager.LogLine($"WARNING: Cannot map unqualified image name [{imageName}] to a fully qualified image.");
                return null;
            }

            if (!string.IsNullOrEmpty(imageTag))
            {
                // Replace the default image tag with the override.

                var posColon = image.LastIndexOf(':');

                if (posColon != -1)
                {
                    image = image.Substring(0, posColon);
                }

                return $"{image}:{imageTag}";
            }
            else
            {
                return image;
            }
        }

        /// <summary>
        /// Updates a service or container start script on a hive node with a new image.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        /// <param name="scriptName">The script name (without the <b>.sh</b>).</param>
        /// <param name="image">The fully qualified image name.</param>
        private static void UpdateStartScript(SshProxy<NodeDefinition> node, string scriptName, string image)
        {
            var scriptPath = LinuxPath.Combine(HiveHostFolders.Scripts, $"{scriptName}.sh");

            node.Status = $"edit: {scriptPath}";

            if (node.FileExists(scriptPath))
            {
                var curScript   = node.DownloadText(scriptPath);
                var sbNewScript = new StringBuilder();

                // Scan for the generated code section and then replace the first
                // line that looks like:
                //
                //      TARGET_IMAGE=OLD-IMAGE
                //
                // with the new image and then upload the change.

                using (var reader = new StringReader(curScript))
                {
                    var inGenerated = false;
                    var wasEdited   = false;

                    foreach (var line in reader.Lines())
                    {
                        if (wasEdited)
                        {
                            sbNewScript.AppendLine(line);
                            continue;
                        }

                        if (!inGenerated && line.StartsWith(ServiceHelper.ParamSectionMarker))
                        {
                            inGenerated = true;
                        }

                        if (line.StartsWith("TARGET_IMAGE="))
                        {
                            sbNewScript.AppendLine($"TARGET_IMAGE={image}");
                            wasEdited = true;
                        }
                        else
                        {
                            sbNewScript.AppendLine(line);
                        }
                    }
                }

                node.UploadText(scriptPath, sbNewScript.ToString(), permissions: "740");
            }

            node.Status = string.Empty;
        }

        /// <summary>
        /// Scans the hive and adds the steps to a <see cref="SetupController{NodeMetadata}"/> required
        /// to update the hive to the most recent version.
        /// </summary>
        /// <param name="hive">The target hive proxy.</param>
        /// <param name="controller">The setup controller.</param>
        /// <param name="dockerVersion">The version of Docker required.</param>
        /// <returns>The number of pending updates.</returns>
        /// <exception cref="HiveException">Thrown if there was an error selecting the updates.</exception>
        /// <remarks>
        /// <note>
        /// This method does not allow an older version of the component to be installed.
        /// In this case, the current version will remain.
        /// </note>
        /// </remarks>
        public static void AddDockerUpdateSteps(HiveProxy hive, SetupController<NodeDefinition> controller, string dockerVersion)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(dockerVersion));

            var newVersion       = (SemanticVersion)dockerVersion;
            var pendingNodes     = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var dockerPackageUri = hive.Headend.GetDockerPackageUri(dockerVersion, out var message);

            // Update the managers first.

            pendingNodes.Clear();
            foreach (var node in hive.Managers)
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
                        UpdateDocker(hive, node, dockerPackageUri);
                    },
                    n => pendingNodes.Contains(n.Name),
                    parallelLimit: 1);
            }

            // Update the workers.

            pendingNodes.Clear();
            foreach (var node in hive.Workers)
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
                        UpdateDocker(hive, node, dockerPackageUri);
                    },
                    n => pendingNodes.Contains(n.Name),
                    parallelLimit: 1);
            }

            // Update the pets.

            pendingNodes.Clear();
            foreach (var node in hive.Pets)
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
                        UpdateDocker(hive, node, dockerPackageUri);
                    },
                    n => pendingNodes.Contains(n.Name),
                    parallelLimit: 1);
            }
        }

        /// <summary>
        /// Updates docker on a hive node.
        /// </summary>
        /// <param name="hive">The target hive.</param>
        /// <param name="node">The target node.</param>
        /// <param name="dockerPackageUri">The Docker Debian package URI.</param>
        private static void UpdateDocker(HiveProxy hive, SshProxy<NodeDefinition> node, string dockerPackageUri)
        {
            try
            {
                if (node.Metadata.InSwarm)
                {
                    node.Status = "swarm: drain services";
                    hive.Docker.DrainNode(node.Name);
                }

                node.Status = "stop: docker";
                node.SudoCommand("systemctl stop docker").EnsureSuccess();

                node.Status = "download: docker package";
                node.SudoCommand($"curl {Program.CurlOptions} {dockerPackageUri} -o /tmp/docker.deb").EnsureSuccess();

                node.Status = "update: docker";
                node.SudoCommand("gdebi /tmp/docker.deb").EnsureSuccess();
                node.SudoCommand("rm /tmp/docker.deb");

                node.Status = "restart: docker";
                node.SudoCommand("systemctl start docker").EnsureSuccess();

                if (node.Metadata.InSwarm)
                {
                    node.Status = "swarm: activate";
                    hive.Docker.ActivateNode(node.Name);
                }
            }
            catch (Exception e)
            {
                node.Fault($"[docker] update failed: {NeonHelper.ExceptionError(e)}");
            }
        }

        /// <summary>
        /// Adds a global step that restarts the designated cluster nodes one-by-one.
        /// </summary>
        /// <param name="hive">The hive proxy.</param>
        /// <param name="controller">The setup controller.</param>
        /// <param name="predicate">
        /// Optionally specifies the predicate to be used to select the hive nodes
        /// to be rebooted.  This defaults to <c>null</c> indicating that all nodes
        /// will be rebooted.
        /// </param>
        /// <param name="stepLabel">
        /// Optionally specifies the step label.  This default to <b>restart nodes</b>.
        /// </param>
        /// <param name="stablizeTime">
        /// The time to wait after the node has been restarted for things
        /// to stablize.  This defaults to <see cref="Program.WaitSeconds"/>.
        /// </param>
        public static void AddRestartClusterStep(
            HiveProxy                           hive, 
            SetupController<NodeDefinition>     controller, 
            Func<NodeDefinition, bool>          predicate    = null, 
            string                              stepLabel    = null, 
            TimeSpan                            stablizeTime = default(TimeSpan))
        {
            Covenant.Requires<ArgumentNullException>(hive != null);
            Covenant.Requires<ArgumentNullException>(controller != null);

            predicate = predicate ?? (node => true);

            stepLabel = stepLabel ?? "restart nodes";

            if (stablizeTime <= TimeSpan.Zero)
            {
                stablizeTime = TimeSpan.FromSeconds(Program.WaitSeconds);
            }

            controller.AddGlobalStep(stepLabel,
                () =>
                {
                    foreach (var node in hive.Nodes.Where(n => predicate(n.Metadata)))
                    {
                        node.Status = "restart pending";
                    }

                    // We're going to restart selected nodes by type in this order:
                    //
                    //      Managers
                    //      Workers
                    //      Pets

                    var restartNode = new Action<SshProxy<NodeDefinition>>(
                        node =>
                        {
                            node.Status = "restart";
                            node.Reboot(wait: true);
                            node.Status = $"stabilize ({stablizeTime.TotalSeconds}s)";
                            Thread.Sleep(stablizeTime);
                            node.Status = "READY";
                        });

                    // Manager nodes.

                    foreach (var node in hive.Nodes.Where(n => n.Metadata.IsManager && predicate(n.Metadata)))
                    {
                        restartNode(node);
                    }

                    // Worker nodes.

                    foreach (var node in hive.Nodes.Where(n => n.Metadata.IsWorker && predicate(n.Metadata)))
                    {
                        restartNode(node);
                    }

                    // Pet nodes.

                    foreach (var node in hive.Nodes.Where(n => n.Metadata.IsPet && predicate(n.Metadata)))
                    {
                        restartNode(node);
                    }

                    // Clear the node status.

                    foreach (var node in hive.Nodes)
                    {
                        node.Status = string.Empty;
                    }
                });
        }
    }
}
