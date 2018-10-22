//-----------------------------------------------------------------------------
// FILE:	    ServiceHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.IO;
using Neon.Hive;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace NeonCli
{
    /// <summary>
    /// Service/container deployment class utilities.
    /// </summary>
    public static class ServiceHelper
    {
        /// <summary>
        /// Use this argument to specify the target image in a <b>docker service create ...</b>
        /// or <b>docker run ...</b> command that will eventually be passed to one of 
        /// <see cref="StartService(HiveProxy, string, string, IBashCommandFormatter, RunOptions)"/> or 
        /// <see cref="StartContainer(SshProxy{NodeDefinition}, string, string, RunOptions, IBashCommandFormatter[])"/> so
        /// those methods can generate a more useful script that includes a parameter
        /// so that images and services can be easily upgraded.
        /// </summary>
        public const string ImagePlaceholderArg = "__TARGET_IMAGE__";

        /// <summary>
        /// Comments used to mark the generated arguments section of the script.
        /// </summary>
        public const string ParamSectionMarker = "# === GENERATED SCRIPT SECTION: DO NOT MODIFY ===";

        /// <summary>
        /// Generates the script used to start a neonHIVE related service or container.
        /// </summary>
        /// <param name="serviceName">Identifies the service.</param>
        /// <param name="pullImage">Indicates whether the image should be pulled before executing the commands.</param>
        /// <param name="image">The Docker image to be used by the service.</param>
        /// <param name="commands">The service creation commands.</param>
        /// <returns>The script text.</returns>
        private static string CreateStartScript(string serviceName, string image, bool pullImage, params IBashCommandFormatter[] commands)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(serviceName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(image));
            Covenant.Requires<ArgumentNullException>(commands != null);
            Covenant.Requires<ArgumentNullException>(commands.Length > 0);

            var sbScript = new StringBuilder();

            // Resolve the image tag.

            image = Program.ResolveDockerImage(image);

            // Generate the shebang.

            sbScript.AppendLine("#!/bin/bash");
            sbScript.AppendLine();

            // Generate the script's parameter section.
            //
            // WARNING: 
            //
            // Do not modify this code without being sure that
            // [HiveUpdateManager] will still be able to edit
            // the service/container creation scripts.

            sbScript.AppendLine(ParamSectionMarker);
            sbScript.AppendLine();

            sbScript.AppendLine($"TARGET_IMAGE={image}");
            sbScript.AppendLine();
            sbScript.AppendLine(@"if [ ""${1}"" != """" ] ; then");
            sbScript.AppendLine(@"    TARGET_IMAGE=${1}");
            sbScript.AppendLine(@"fi");

            if (pullImage)
            {
                sbScript.AppendLine();
                sbScript.AppendLine($"docker pull ${{TARGET_IMAGE}}");
            }

            sbScript.AppendLine();
            sbScript.AppendLine(ParamSectionMarker);

            // Render the service creation commands and append them to the script.

            foreach (var command in commands)
            {
                sbScript.AppendLine();
                sbScript.AppendLine(command.ToBash());
            }

            // Get the script text and then replace any [ImageArg] substrings with
            // a [${TARGET_IMAGE}] macro reference.

            var script = sbScript.ToString();

            return script.Replace(ImagePlaceholderArg, "${TARGET_IMAGE}");
        }

        /// <summary>
        /// Starts a neonHIVE related Docker service and also uploads a script to the 
        /// hive managers to make it easy to restart the service manually or for hive
        /// updates.
        /// </summary>
        /// <param name="hive">The target hive.</param>
        /// <param name="serviceName">Identifies the service.</param>
        /// <param name="image">The Docker image to be used by the service.</param>
        /// <param name="command">The <c>docker service create ...</c> command.</param>
        /// <param name="runOptions">Optional run options (defaults to <see cref="RunOptions.FaultOnError"/>).</param>
        /// <remarks>
        /// <para>
        /// This method performs the following steps:
        /// </para>
        /// <list type="number">
        ///     <item>
        ///     Passes <paramref name="image"/> to <see cref="Program.ResolveDockerImage(string)"/> to
        ///     obtain the actual image to be started.
        ///     </item>
        ///     <item>
        ///     Generates the first few lines of the script file that sets the
        ///     default image as the <c>TARGET_IMAGE</c> macro and then overrides 
        ///     this with the script parameter (if there is one).
        ///     </item>
        ///     <item>
        ///     Appends the commands to the script, replacing any text that matches
        ///     <see cref="ImagePlaceholderArg"/> with <c>${TARGET_IMAGE}</c> to make it easy
        ///     for services to be upgraded later.
        ///     </item>
        ///     <item>
        ///     Starts the service.
        ///     </item>
        ///     <item>
        ///     Uploads the generated script to each hive manager to [<see cref="HiveHostFolders.Scripts"/>/<paramref name="serviceName"/>.sh].
        ///     </item>
        /// </list>
        /// </remarks>
        public static void StartService(HiveProxy hive, string serviceName, string image, IBashCommandFormatter command, RunOptions runOptions = RunOptions.FaultOnError)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(serviceName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(image));
            Covenant.Requires<ArgumentNullException>(command != null);

            var firstManager = hive.FirstManager;

            firstManager.Status = $"start: {serviceName}";

            // Generate the service start script.

            var script = CreateStartScript(serviceName, image, false, command);

            // Upload the script to each of the manager nodes and set permissions.

            var scriptPath = LinuxPath.Combine(HiveHostFolders.Scripts, $"{serviceName}.sh");

            foreach (var manager in hive.Managers)
            {
                manager.UploadText(scriptPath, script);
                manager.SudoCommand($"chmod 740 {scriptPath}");
            }

            // Run the script without a parameter on the first manager to start the service.

            firstManager.IdempotentDockerCommand($"setup/{serviceName}", 
                response => 
                {
                    if (response.ExitCode != 0)
                    {
                        firstManager.Fault(response.ErrorSummary);
                    }
                },
                runOptions,
                scriptPath);

            firstManager.Status = string.Empty;
        }

        /// <summary>
        /// Appends the steps required to start a neonHIVE related Docker service and upload
        /// a script to the hive managers to make it easy to restart the service manually or 
        /// for hive updates.
        /// </summary>
        /// <param name="hive">The target hive.</param>
        /// <param name="steps">The target step list.</param>
        /// <param name="serviceName">Identifies the service.</param>
        /// <param name="image">The Docker image to be used by the service.</param>
        /// <param name="command">The <c>docker service create ...</c> command.</param>
        /// <param name="runOptions">Optional run options (defaults to <see cref="RunOptions.FaultOnError"/>).</param>
        /// <remarks>
        /// <para>
        /// This method performs the following steps:
        /// </para>
        /// <list type="number">
        ///     <item>
        ///     Passes <paramref name="image"/> to <see cref="Program.ResolveDockerImage(string)"/> to
        ///     obtain the actual image to be started.
        ///     </item>
        ///     <item>
        ///     Generates the first few lines of the script file that sets the
        ///     default image as the <c>TARGET_IMAGE</c> macro and then overrides 
        ///     this with the script parameter (if there is one).
        ///     </item>
        ///     <item>
        ///     Appends the commands to the script, replacing any text that matches
        ///     <see cref="ImagePlaceholderArg"/> with <c>${TARGET_IMAGE}</c> to make it easy
        ///     for services to be upgraded later.
        ///     </item>
        ///     <item>
        ///     Starts the service.
        ///     </item>
        ///     <item>
        ///     Uploads the generated script to each hive manager to [<see cref="HiveHostFolders.Scripts"/>/<paramref name="serviceName"/>.sh].
        ///     </item>
        /// </list>
        /// </remarks>
        public static void AddServiceStartSteps(HiveProxy hive, ConfigStepList steps, string serviceName, string image, IBashCommandFormatter command, RunOptions runOptions = RunOptions.FaultOnError)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);
            Covenant.Requires<ArgumentNullException>(steps != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(serviceName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(image));
            Covenant.Requires<ArgumentNullException>(command != null);

            // Generate the service start script.

            var script = CreateStartScript(serviceName, image, false, command);

            // Add steps to upload the script to the managers and then call the script 
            // to create the service on the first manager.

            var scriptPath = LinuxPath.Combine(HiveHostFolders.Scripts, $"{serviceName}.sh");

            steps.Add(hive.GetFileUploadSteps(hive.Managers, scriptPath, script, permissions: "740"));
            steps.Add(CommandStep.CreateIdempotentDocker(hive.FirstManager.Name, $"setup/{serviceName}", scriptPath));
        }

        /// <summary>
        /// Starts a neonHIVE related Docker container on a node and also uploads a script 
        /// to make it easy to restart the container manually or for hive updates.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        /// <param name="containerName">Identifies the container.</param>
        /// <param name="image">The Docker image to be used by the container.</param>
        /// <param name="runOptions">Optional run options (defaults to <see cref="RunOptions.FaultOnError"/>).</param>
        /// <param name="commands">The commands required to start the container.</param>
        /// <remarks>
        /// <para>
        /// This method performs the following steps:
        /// </para>
        /// <list type="number">
        ///     <item>
        ///     Passes <paramref name="image"/> to <see cref="Program.ResolveDockerImage(string)"/> to
        ///     obtain the actual image to be started.
        ///     </item>
        ///     <item>
        ///     Generates the first few lines of the script file that sets the
        ///     default image as the <c>TARGET_IMAGE</c> macro and then overrides 
        ///     this with the script parameter (if there is one).
        ///     </item>
        ///     <item>
        ///     Appends the commands to the script, replacing any text that matches
        ///     <see cref="ImagePlaceholderArg"/> with <c>${TARGET_IMAGE}</c> to make it easy
        ///     for services to be upgraded later.
        ///     </item>
        ///     <item>
        ///     Starts the container.
        ///     </item>
        ///     <item>
        ///     Uploads the generated script to the node to [<see cref="HiveHostFolders.Scripts"/>/<paramref name="containerName"/>.sh].
        ///     </item>
        /// </list>
        /// </remarks>
        public static void StartContainer(SshProxy<NodeDefinition> node, string containerName, string image, RunOptions runOptions = RunOptions.FaultOnError, params IBashCommandFormatter[] commands)
        {
            Covenant.Requires<ArgumentNullException>(node != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(containerName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(image));
            Covenant.Requires<ArgumentNullException>(commands != null);
            Covenant.Requires<ArgumentNullException>(commands.Length > 0);

            node.Status = $"start: {containerName}";

            // Generate the container start script.

            var script = CreateStartScript(containerName, image, true, commands);

            // Upload the script to the target node and set permissions.

            var scriptPath = LinuxPath.Combine(HiveHostFolders.Scripts, $"{containerName}.sh");

            node.UploadText(scriptPath, script);
            node.SudoCommand($"chmod 740 {scriptPath}");

            // Run the script without a parameter to start the container.

            node.IdempotentDockerCommand($"setup/{containerName}", null, runOptions, scriptPath);

            node.Status = string.Empty;
        }

        /// <summary>
        /// Appends the steps required to start a neonHIVE related Docker container and upload
        /// a script to the hive managers to make it easy to restart the service manually or 
        /// for hive updates.
        /// </summary>
        /// <param name="hive">The target hive.</param>
        /// <param name="steps">The target step list.</param>
        /// <param name="node">The target hive node.</param>
        /// <param name="containerName">Identifies the service.</param>
        /// <param name="image">The Docker image to be used by the container.</param>
        /// <param name="command">The <c>docker service create ...</c> command.</param>
        /// <param name="runOptions">Optional run options (defaults to <see cref="RunOptions.FaultOnError"/>).</param>
        /// <remarks>
        /// <para>
        /// This method performs the following steps:
        /// </para>
        /// <list type="number">
        ///     <item>
        ///     Passes <paramref name="image"/> to <see cref="Program.ResolveDockerImage(string)"/> to
        ///     obtain the actual image to be started.
        ///     </item>
        ///     <item>
        ///     Generates the first few lines of the script file that sets the
        ///     default image as the <c>TARGET_IMAGE</c> macro and then overrides 
        ///     this with the script parameter (if there is one).  We also add
        ///     a Docker command that pulls the image.
        ///     </item>
        ///     <item>
        ///     Appends the commands to the script, replacing any text that matches
        ///     <see cref="ImagePlaceholderArg"/> with <c>${TARGET_IMAGE}</c> to make it easy
        ///     for services to be upgraded later.
        ///     </item>
        ///     <item>
        ///     Starts the service.
        ///     </item>
        ///     <item>
        ///     Uploads the generated script to each hive manager to [<see cref="HiveHostFolders.Scripts"/>/<paramref name="containerName"/>.sh].
        ///     </item>
        /// </list>
        /// </remarks>
        public static void AddContainerStartSteps(HiveProxy hive, ConfigStepList steps, SshProxy<NodeDefinition> node, string containerName, string image, IBashCommandFormatter command, RunOptions runOptions = RunOptions.FaultOnError)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);
            Covenant.Requires<ArgumentNullException>(steps != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(containerName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(image));
            Covenant.Requires<ArgumentNullException>(command != null);

            // Generate the container start script.

            var script = CreateStartScript(containerName, image, true, command);

            // Add steps to upload the script to the managers and then call the script 
            // to create the container on the target node.

            var scriptPath = LinuxPath.Combine(HiveHostFolders.Scripts, $"{containerName}.sh");

            steps.Add(hive.GetFileUploadSteps(node, scriptPath, script, permissions: "740"));
            steps.Add(CommandStep.CreateIdempotentDocker(node.Name, $"setup/{containerName}", scriptPath));
        }
    }
}
