//-----------------------------------------------------------------------------
// FILE:	    DockerManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Docker;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;
namespace Neon.Hive
{
    /// <summary>
    /// Handles Docker related operations for a <see cref="HiveProxy"/>.
    /// </summary>
    public sealed class DockerManager
    {
        private HiveProxy hive;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="hive">The parent <see cref="HiveProxy"/>.</param>
        internal DockerManager(HiveProxy hive)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            this.hive    = hive;
            this.Config  = new DockerConfigManager(hive);
            this.Secret  = new DockerSecretManager(hive);
        }

        /// <summary>
        /// Returns Docker config manager.
        /// </summary>
        public DockerConfigManager Config { get; private set; }

        /// <summary>
        /// Returns the Docker secret manager.
        /// </summary>
        public DockerSecretManager Secret { get; private set; }

        /// <summary>
        /// Lists the Docker services running on the hive.
        /// </summary>
        /// <returns>The service names.</returns>
        /// <exception cref="HiveException">Thrown if the operation failed.</exception>
        public IEnumerable<string> ListServices()
        {
            var manager  = hive.GetReachableManager();
            var response = manager.SudoCommand("docker service ls --format {{.Name}}");

            if (response.ExitCode != 0)
            {
                throw new HiveException(response.ErrorSummary);
            }

            using (var reader = new StringReader(response.OutputText))
            {
                return reader.Lines().ToArray();
            }
        }

        /// <summary>
        /// Inspects a service, returning details about its current state.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="strict">Optionally specify strict JSON parsing.</param>
        /// <returns>The <see cref="ServiceDetails"/> or <c>null</c> if the service doesn't exist.</returns>
        public ServiceDetails InspectService(string name, bool strict = false)
        {
            var response = hive.GetReachableManager().DockerCommand(RunOptions.None, "docker", "service", "inspect", name);

            if (response.ExitCode != 0)
            {
                if (response.AllText.Contains("Status: Error: no such service:"))
                {
                    return null;
                }

                throw new Exception($"Cannot inspect service [{name}]: {response.AllText}");
            }

            // The inspection response is actually an array with a single
            // service details element, so we'll need to extract that element
            // and then parse it.

            var jArray      = JArray.Parse(response.OutputText);
            var jsonDetails = jArray[0].ToString(Formatting.Indented);
            var details     = NeonHelper.JsonDeserialize<ServiceDetails>(jsonDetails, strict);

            details.Normalize();

            return details;
        }

        /// <summary>
        /// Signals the Docker orchestrator to drain all service tasks from a node.
        /// </summary>
        /// <param name="nodeName">Identifies the target node.</param>
        /// <exception cref="KeyNotFoundException">Thrown if the named node does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the node is not part of the swarm.</exception>
        public void DrainNode(string nodeName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName));

            var node = hive.GetNode(nodeName);

            if (!node.Metadata.InSwarm)
            {
                throw new InvalidOperationException($"Node [{nodeName}] is not part of the swarm.");
            }

            // I've see transient errors, so we'll retry a few times.
            
            var manager = hive.GetReachableManager();
            var retry   = new LinearRetryPolicy(typeof(Exception), maxAttempts: 5, retryInterval: TimeSpan.FromSeconds(5));

            retry.InvokeAsync(
                async () =>
                {
                    var response = manager.SudoCommand($"docker node update --availability drain {nodeName}");

                    if (response.ExitCode != 0)
                    {
                        throw new Exception(response.ErrorSummary);
                    }

                    await Task.CompletedTask;

                }).Wait();

            // $todo(jeff.lill): 
            //
            // Ideally, we'd wait for all of the service tasks to stop but it 
            // appears that there's no easy way to check for this other than 
            // listing all of the hive services and then doing a 
            //
            //      docker service ps SERVICE]
            //
            // for each until none report running on this node.
            //
            // A hacky alternative would be to list local containers and try
            // to determine which ones look liks service tasks by examining
            // the container name.

            Thread.Sleep(TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Signals the Docker orchestrator to begin scheduling service tasks on a node.
        /// </summary>
        /// <param name="nodeName">Identifies the target node.</param>
        /// <exception cref="KeyNotFoundException">Thrown if the named node does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the node is not part of the swarm.</exception>
        public void ActivateNode(string nodeName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName));

            var node = hive.GetNode(nodeName);

            if (!node.Metadata.InSwarm)
            {
                throw new InvalidOperationException($"Node [{nodeName}] is not part of the swarm.");
            }

            // I've see transient errors, so we'll retry a few times.

            var manager = hive.GetReachableManager();
            var retry   = new LinearRetryPolicy(typeof(Exception), maxAttempts: 5, retryInterval: TimeSpan.FromSeconds(5));

            retry.InvokeAsync(
                async () =>
                {
                    var response = manager.SudoCommand($"docker node update --availability active {nodeName}");

                    if (response.ExitCode != 0)
                    {
                        throw new Exception(response.ErrorSummary);
                    }

                    await Task.CompletedTask;

                }).Wait();
        }
    }
}
