//-----------------------------------------------------------------------------
// FILE:	    DockerServiceSpec.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using ICSharpCode.SharpZipLib.Zip;

using Neon.Cluster;
using Neon.Cryptography;
using Neon.Common;
using Neon.Docker;
using Neon.IO;
using Neon.Net;

namespace NeonCli.Ansible.Docker
{
    // NOTE: The types below are accurate as of Docker API version 1.35.

    /// <summary>
    /// Service port publication specification.
    /// </summary>
    public class PublishPort
    {
        public string Name { get; set; }
        public int? Published { get; set; }
        public int? Target { get; set; }
        public ServicePortMode? Mode { get; set; }
        public ServicePortProtocol? Protocol { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendWithSeparator($"published={Published}", ",");
            sb.AppendWithSeparator($"target={Target}", ",");

            if (Mode.HasValue)
            {
                sb.AppendWithSeparator($"mode={Mode.Value}", ",");
            }
            else
            {
                sb.AppendWithSeparator($"mode=ingress", ",");
            }

            if (Protocol.HasValue)
            {
                sb.AppendWithSeparator($"protocol={Protocol.Value}", ",");
            }
            else
            {
                sb.AppendWithSeparator($"protocol=tcp", ",");
            }

            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            var other = obj as PublishPort;

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }
            else if (this == null && other == null)
            {
                return true;
            }
            else if (this == null && other != null)
            {
                return false;
            }
            else if (this != null && other == null)
            {
                return false;
            }
            else
            {
                return this.ToString() == other.ToString();
            }
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    /// <summary>
    /// Service mount specification.
    /// </summary>
    public class Mount
    {
        public ServiceMountType? Type { get; set; }
        public string Source { get; set; }
        public string Target { get; set; }
        public bool? ReadOnly { get; set; }
        public ServiceMountConsistency? Consistency { get; set; }
        public ServiceMountBindPropagation? BindPropagation { get; set; }
        public string VolumeDriver { get; set; }
        public List<string> VolumeLabel { get; private set; } = new List<string>();
        public bool? VolumeNoCopy { get; set; }
        public List<string> VolumeOpt { get; private set; } = new List<string>();
        public long? TmpfsSize { get; set; }
        public string TmpfsMode { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();

            if (Type.HasValue)
            {
                sb.AppendWithSeparator($"type={Type.Value}", ",");
            }
            else
            {
                sb.AppendWithSeparator($"type=volume", ",");
            }

            if (Source != null)
            {
                sb.AppendWithSeparator($"source={Source}", ",");
            }

            if (Target != null)
            {
                sb.AppendWithSeparator($"target={Target}", ",");
            }

            if (ReadOnly.HasValue)
            {
                sb.AppendWithSeparator($"readonly={ReadOnly.Value.ToString().ToLowerInvariant()}", ",");
            }
            else
            {
                sb.AppendWithSeparator($"readonly=false", ",");
            }

            if (Consistency.HasValue)
            {
                sb.AppendWithSeparator($"consistency={Consistency.Value}", ",");
            }
            else
            {
                sb.AppendWithSeparator($"consistency=default", ",");
            }

            if (BindPropagation.HasValue)
            {
                sb.AppendWithSeparator($"bind-propagation={BindPropagation.Value}", ",");
            }
            else
            {
                sb.AppendWithSeparator($"bind-propagation=rprivate", ",");
            }

            if (!Type.HasValue || Type.Value == ServiceMountType.Volume)
            {
                if (VolumeDriver != null)
                {
                    sb.AppendWithSeparator($"volume-driver={VolumeDriver}", ",");
                }

                if (VolumeNoCopy.HasValue)
                {
                    sb.AppendWithSeparator($"volume-nocopy={VolumeNoCopy.ToString().ToLowerInvariant()}", ",");
                }
                else
                {
                    sb.AppendWithSeparator($"volume-nocopy=true", ",");
                }

                // I believe this needs to be specified last in the command line option.

                if (VolumeLabel.Count > 0)
                {
                    sb.AppendWithSeparator("volume-label=", ",");

                    foreach (var label in VolumeLabel)
                    {
                        sb.AppendWithSeparator(label, ",");
                    }
                }
            }

            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            var other = obj as Mount;

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }
            else if (this == null && other == null)
            {
                return true;
            }
            else if (this == null && other != null)
            {
                return false;
            }
            else if (this != null && other == null)
            {
                return false;
            }
            else
            {
                return this.ToString() == other.ToString();
            }
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    /// <summary>
    /// Service secret.
    /// </summary>
    public class Secret
    {
        public string Source { get; set; }
        public string Target { get; set; }
        public string UID { get; set; }
        public string GID { get; set; }
        public string Mode { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();

            if (Source != null)
            {
                sb.AppendWithSeparator($"source={Source}", ",");
            }

            if (Target != null)
            {
                sb.AppendWithSeparator($"target={Target}", ",");
            }

            if (UID != null)
            {
                sb.AppendWithSeparator($"uid={UID}", ",");
            }

            if (GID != null)
            {
                sb.AppendWithSeparator($"gid={GID}", ",");
            }

            if (Mode != null)
            {
                sb.AppendWithSeparator($"mode={Mode}", ",");
            }

            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            var other = obj as Secret;

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }
            else if (this == null && other == null)
            {
                return true;
            }
            else if (this == null && other != null)
            {
                return false;
            }
            else if (this != null && other == null)
            {
                return false;
            }
            else
            {
                return this.ToString() == other.ToString();
            }
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    /// <summary>
    /// Service config.
    /// </summary>
    public class Config
    {
        public string Source { get; set; }
        public string Target { get; set; }
        public string UID { get; set; }
        public string GID { get; set; }
        public string Mode { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();

            if (Source != null)
            {
                sb.AppendWithSeparator($"source={Source}", ",");
            }

            if (Target != null)
            {
                sb.AppendWithSeparator($"target={Target}", ",");
            }

            if (UID != null)
            {
                sb.AppendWithSeparator($"uid={UID}", ",");
            }

            if (GID != null)
            {
                sb.AppendWithSeparator($"gid={GID}", ",");
            }

            if (Mode != null)
            {
                sb.AppendWithSeparator($"mode={Mode}", ",");
            }

            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            var other = obj as Config;

            if (object.ReferenceEquals(this, other))
            {
                return true;
            }
            else if (this == null && other == null)
            {
                return true;
            }
            else if (this == null && other != null)
            {
                return false;
            }
            else if (this != null && other == null)
            {
                return false;
            }
            else
            {
                return this.ToString() == other.ToString();
            }
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    /// <summary>
    /// Specifies a Docker service.
    /// </summary>
    public class DockerServiceSpec
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Creates a <see cref="DockerServiceSpec"/> by parsing the response from a
        /// <b>docker service inspect SERVICE</b> for a service as well as the table output
        /// from a <b>docker network ls --no-trunc</b> command listing the current networks.
        /// </summary>
        /// <param name="context">The Annsible module context.</param>
        /// <param name="serviceDetails"><b>docker service inspect SERVICE</b> command output for the service.</param>
        /// <param name="networksText"><b>docker network ls --no-trunc</b> command output.</param>
        /// <returns>The parsed <see cref="DockerServiceSpec"/>.</returns>
        public static DockerServiceSpec FromDockerInspect(ModuleContext context, ServiceDetails serviceDetails, string networksText)
        {
            var service = new DockerServiceSpec();

            service.Parse(context, serviceDetails, networksText);

            return service;
        }

        /// <summary>
        /// Generates the <b>docker service update</b> command arguments that updates 
        /// the service whose current state is passed so that it matches the updated
        /// state.
        /// </summary>
        /// <param name="context">The module context.</param>
        /// <param name="current">The current service state as loaded by <see cref="FromDockerInspect(ModuleContext, string, string)"/>.</param>
        /// <param name="update">The required new service state.</param>
        /// <returns>
        /// The <b>docker service update</b> command arguments as a list.  
        /// This does not include the <b>docker service update</b> prefix.
        /// </returns>
        public static List<string> DockerUpdateCommandArgs(ModuleContext context, DockerServiceSpec current, DockerServiceSpec update)
        {
            var outputArgs = new List<string>();

            if (!AreIdentical(current.Args, update.Args))
            {
                // The service arguments are specified together in a single
                // quoted and space separated string.  Note that we don't need
                // to quote the arguments here because that will happen when
                // when we actually execute the update command.
                //
                // NOTE: This is a bit broken from a Docker design perspective
                //       because it implicitly assumes that service arguments
                //       never include spaces.  This is probably a reasonable
                //       assumption for Linux but may be an issue for Windows.

                outputArgs.Add("--args");

                var sb = new StringBuilder();

                foreach (var item in update.Args)
                {
                    sb.AppendWithSeparator(item.ToString());
                }

                outputArgs.Add(sb.ToString());
            }

            AppendUpdateListArgs(context, outputArgs, "--config", current.Config, update.Config, state => state.Source);
            AppendUpdateListArgs(context, outputArgs, "--constraint", current.Constraint, update.Constraint, SimpleNameExtractor);
            AppendUpdateListArgs(context, outputArgs, "--container-label", current.ContainerLabel, update.ContainerLabel, SimpleNameExtractor);
            AppendUpdateListArgs(context, outputArgs, "--credential-spec", current.CredentialSpec, update.CredentialSpec);
            AppendUpdateListArgs(context, outputArgs, "--dns", current.Dns, update.Dns);
            AppendUpdateListArgs(context, outputArgs, "--dns-option", current.DnsOption, update.DnsOption, SimpleNameExtractor);
            AppendUpdateListArgs(context, outputArgs, "--dns-search", current.DnsSearch, update.DnsSearch);
            AppendUpdateListArgs(context, outputArgs, "--constraint", current.Constraint, update.Constraint, SimpleNameExtractor);
            AppendUpdateEnumArgs<ServiceEndpointMode>(context, outputArgs, "--endpoint-mode", current.EndpointMode, update.EndpointMode);

            if (!AreIdentical(current.Command, update.Command))
            {
                // NOTE: I think the Docker design here may be broken because it
                //       doesn't look like it's possible to remove an entrypoint
                //       override because that would require passing an empty 
                //       string argument which will end up being ignored.
                //
                //       We're going to detect and fail when we see this.

                // $todo(jeff.lill):
                //
                // We could potentially address this using the REST API but I
                // don't want to go there right now.

                if (update.Command.Count == 0)
                {
                    context.WriteErrorLine("It is not possible to remove an existing [--entrypoint] override.");
                }
                else
                {
                    var sb = new StringBuilder();

                    foreach (var item in update.Command)
                    {
                        sb.AppendWithSeparator(item);
                    }

                    outputArgs.Add("--entrypoint");
                    outputArgs.Add(sb.ToString());
                }
            }

            AppendUpdateListArgs(context, outputArgs, "--env", current.Env, update.Env, SimpleNameExtractor);
            AppendUpdateListArgs(context, outputArgs, "--group", current.Groups, update.Groups, SimpleNameExtractor);

            if (!AreIdentical(current.HealthCmd, update.HealthCmd))
            {
                // NOTE: I think the Docker design here may be broken because it
                //       doesn't look like it's possible to remove an health command
                //       override because that would require passing an empty string
                //       argument which will end up being ignored.
                //
                //       We're going to detect and fail when we see this.

                // $todo(jeff.lill):
                //
                // We could potentially address this using the REST API but I
                // don't want to go there right now.

                if (update.HealthCmd.Count == 0)
                {
                    context.WriteErrorLine("It is not possible to remove an existing [--health-cmd] override.");
                }
                else
                {
                    var sb = new StringBuilder();

                    foreach (var item in update.HealthCmd)
                    {
                        sb.AppendWithSeparator(item);
                    }

                    outputArgs.Add("--health-cmd");
                    outputArgs.Add(sb.ToString());
                }
            }

            AppendUpdateDurationArgs(context, outputArgs, "--health-interval", current.HealthInterval, update.HealthInterval);
            AppendUpdateLongArgs(context, outputArgs, "--health-retries", current.HealthRetries, update.HealthRetries);
            AppendUpdateDurationArgs(context, outputArgs, "--health-startperiod", current.HealthStartPeriod, update.HealthStartPeriod);
            AppendUpdateDurationArgs(context, outputArgs, "--health-timeout", current.HealthTimeout, update.HealthTimeout);
            AppendUpdateListArgs(context, outputArgs, "--host", current.Host, update.Host);
            AppendUpdateStringArgs(context, outputArgs, "--hostname", current.Hostname, update.Hostname);
            AppendUpdateStringArgs(context, outputArgs, "--image", current.Image, update.Image);
            AppendUpdateEnumArgs(context, outputArgs, "--isolation", current.Isolation, update.Isolation);
            AppendUpdateListArgs(context, outputArgs, "--label", current.Label, update.Label);
            AppendUpdateDoubleArgs(context, outputArgs, "--limit-cpu", current.LimitCpu, update.LimitCpu);
            AppendUpdateLongArgs(context, outputArgs, "--limit-memory", current.LimitMemory, update.LimitMemory);
            AppendUpdateStringArgs(context, outputArgs, "--log-driver", current.LogDriver, update.LogDriver);

            if (!AreIdentical(current.LogOpt, update.LogOpt))
            {
                // The [docker service update] command does not include [--log-opt-add]
                // and [--log-opt-rm] options to manage log options individually, so we're
                // just going to build up a comma separator string of [OPTION=VALUE].

                var sb = new StringBuilder();

                foreach (var option in update.LogOpt)
                {
                    sb.AppendWithSeparator(option, ",");
                }

                outputArgs.Add("--log-opt");
                outputArgs.Add(sb.ToString());
            }

            AppendUpdateListArgs(context, outputArgs, "--mount", current.Mount, update.Mount, mount => mount.Target);
            AppendUpdateListArgs(context, outputArgs, "--network", current.Network, update.Network);
            AppendUpdateBoolArgs(context, outputArgs, "--no-health-check", current.NoHealthCheck, update.NoHealthCheck);
            AppendUpdateBoolArgs(context, outputArgs, "--no-resolve-image", current.NoResolveImage, update.NoResolveImage);

            // $todo(jeff.lill): Ignoring [--placement-pref].

            AppendUpdateListArgs(context, outputArgs, "--publish", current.Publish, update.Publish);
            AppendUpdateBoolArgs(context, outputArgs, "--read-only", current.ReadOnly, update.ReadOnly);
            AppendUpdateLongArgs(context, outputArgs, "--replicas", current.Replicas, update.Replicas);
            AppendUpdateDoubleArgs(context, outputArgs, "--reserve-cpu", current.ReserveCpu, update.ReserveCpu);
            AppendUpdateLongArgs(context, outputArgs, "--reserve-memory", current.ReserveMemory, update.ReserveMemory);
            AppendUpdateEnumArgs(context, outputArgs, "--restart-condition", current.RestartCondition, update.RestartCondition);
            AppendUpdateDurationArgs(context, outputArgs, "--restart-delay", current.RestartDelay, update.RestartDelay);
            AppendUpdateLongArgs(context, outputArgs, "--restart-max-attempts", current.RestartMaxAttempts, update.RestartMaxAttempts);
            AppendUpdateDurationArgs(context, outputArgs, "--restart-window", current.RestartWindow, update.RestartWindow);
            AppendUpdateDurationArgs(context, outputArgs, "--rollback-delay", current.RollbackDelay, update.RollbackDelay);
            AppendUpdateEnumArgs(context, outputArgs, "--rollback-failure-action", current.RollbackFailureAction, update.RollbackFailureAction);
            AppendUpdateDoubleArgs(context, outputArgs, "--rollback-max-failure-ratio", current.RollbackMaxFailureRatio, update.RollbackMaxFailureRatio);
            AppendUpdateDurationArgs(context, outputArgs, "--rollback-monitor", current.RollbackMonitor, update.RollbackMonitor);
            AppendUpdateEnumArgs(context, outputArgs, "--rollback-order", current.RollbackOrder, update.RollbackOrder);
            AppendUpdateLongArgs(context, outputArgs, "--rollback-parallelism", current.RollbackParallism, update.RollbackParallism);
            AppendUpdateListArgs(context, outputArgs, "--secret", current.Secret, update.Secret);
            AppendUpdateDurationArgs(context, outputArgs, "--stop-grace-period", current.StopGracePeriod, update.StopGracePeriod);
            AppendUpdateStringArgs(context, outputArgs, "--stop-signal", current.StopSignal, update.StopSignal);
            AppendUpdateBoolArgs(context, outputArgs, "--tty", current.TTY, update.TTY);
            AppendUpdateDurationArgs(context, outputArgs, "--update-delay", current.UpdateDelay, update.UpdateDelay);
            AppendUpdateEnumArgs(context, outputArgs, "--update-failure-action", current.UpdateFailureAction, update.UpdateFailureAction);
            AppendUpdateDoubleArgs(context, outputArgs, "--update-max-failure-ratio", current.UpdateMaxFailureRatio, update.UpdateMaxFailureRatio);
            AppendUpdateDurationArgs(context, outputArgs, "--update-monitor", current.UpdateMonitor, update.UpdateMonitor);
            AppendUpdateEnumArgs(context, outputArgs, "--update-order", current.UpdateOrder, update.UpdateOrder);
            AppendUpdateLongArgs(context, outputArgs, "--update-parallelism", current.UpdateParallism, update.UpdateParallism);
            AppendUpdateStringArgs(context, outputArgs, "--user", current.User, update.User);
            AppendUpdateBoolArgs(context, outputArgs, "--with-registry-auth", current.WithRegistryAuth, update.WithRegistryAuth);
            AppendUpdateStringArgs(context, outputArgs, "--workdir", current.Dir, update.Dir);
#if TODO
            // We're not currently handling these service properties.

            AppendUpdateArgs(outputArgs, "--generic-resource", current.GenericResource, update.GenericResource, SimpleNameExtractor);
#endif
            outputArgs.Add(current.Name);

            return outputArgs;
        }

        /// <summary>
        /// Helper state name extractor function for string state that is specified
        /// like: <b>NAME</b> or <b>NAME=VALUE</b>.
        /// </summary>
        /// <param name="state">The state string.</param>
        /// <returns>The extracted name.</returns>
        private static string SimpleNameExtractor(string state)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(state));

            var equalsPos = state.IndexOf('=');

            if (equalsPos == -1)
            {
                return state;
            }
            else
            {
                return state.Substring(0, equalsPos);
            }
        }

        /// <summary>
        /// Compares two lists to determine whether the have the same elements
        /// in the same order.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="current">The current list.</param>
        /// <param name="update">The updated list.</param>
        /// <returns><c>true</c> if the lists are identical.</returns>
        private static bool AreIdentical<T>(List<T> current, List<T> update)
        {
            Covenant.Requires<ArgumentNullException>(current != null);
            Covenant.Requires<ArgumentNullException>(update != null);

            if (current.Count != update.Count)
            {
                return false;
            }

            for (int i = 0; i < current.Count; i++)
            {
                if (current[i].ToString() != update[i].ToString())
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Compares two lists to determine whether the have the same elements
        /// but potentially in a different order.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="current">The current list.</param>
        /// <param name="update">The updated list.</param>
        /// <returns><c>true</c> if the lists are equlivalent.</returns>
        private static bool AreEquivalent<T>(List<T> current, List<T> update)
        {
            Covenant.Requires<ArgumentNullException>(current != null);
            Covenant.Requires<ArgumentNullException>(update != null);

            if (current.Count != update.Count)
            {
                return false;
            }

            var currentSet = new Dictionary<string, bool>();
            var updateSet  = new Dictionary<string, bool>();

            for (int i = 0; i < current.Count; i++)
            {
                currentSet[current[i].ToString()] = true;
                updateSet[update[i].ToString()] = true;
            }

            foreach (var item in currentSet.Keys)
            {
                if (!updateSet.ContainsKey(item))
                {
                    return false;
                }
            }

            foreach (var item in updateSet.Keys)
            {
                if (!currentSet.ContainsKey(item))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns the name to use for removing specific state from a service.
        /// </summary>
        /// <typeparam name="T">The option type.</typeparam>
        /// <param name="state">The current state.</param>
        /// <param name="nameExtractor">
        /// Optional function that extracts the name to be used for removing the item from
        /// the service from the item value.  This deefaults to the entire item value if
        /// no extractor is specified.
        /// </param>
        /// <returns>
        /// The extracted name if <paramref name="nameExtractor"/> is not <c>null</c> 
        /// or else the option value rendered as a string.
        /// </returns>
        private static string GetStateName<T>(T state, Func<T, string> nameExtractor = null)
        {
            if (nameExtractor != null)
            {
                return nameExtractor(state);
            }
            else
            {
                return state.ToString();
            }
        }

        /// <summary>
        /// Compares the <paramref name="current"/> and <paramref name="update"/> lists describing a
        /// service parameter for the current service state and the required updated state and when
        /// these differ, generates the command line <paramref name="option"/> and values required
        /// to transition the parameter from the current to the updated state within a <b>docker service update</b>
        /// command.  Any generated options will be appended to the <paramref name="outputArgs"/> array.
        /// </summary>
        /// <typeparam name="T">The option type.</typeparam>
        /// <param name="context">The module context.</param>
        /// <param name="outputArgs">The output command line arguments list.</param>
        /// <param name="option">The base command line option (e.g.) <b>--env</b>.</param>
        /// <param name="current">The current service state.</param>
        /// <param name="update">The required updated service state.</param>
        /// <param name="nameExtractor">
        /// Optional function that extracts the name to be used for removing the item from
        /// the service from the item value.  This deefaults to the entire item value if
        /// no extractor is specified.
        /// </param>
        /// <remarks>
        /// <para>
        /// The <c>docker service update</c> commands allows for individual items like
        /// environment variables, secrets, bind mounts,... to be added or removed from
        /// a running service via options like <b>--env-add</b> and <b>--env-rm</b>.
        /// </para>
        /// <para>
        /// This method compares the two lists passed and generates the necessary <b>add/rm</b>
        /// arguments to update the service state using <paramref name="option"/> as the base
        /// option name to which <b>-add</b> or <b>--rm</b> will be appended as necessary.
        /// </para>
        /// <para>
        /// The <paramref name="nameExtractor"/> function is required for sertvice state where
        /// the <b>rm</b> option identifies the option by a subfield of the option value
        /// (e.g. the target port for a <b>--publish-rm port</b> option).
        /// </para>
        /// </remarks>
        private static void AppendUpdateListArgs<T>(ModuleContext context, List<string> outputArgs, string option, List<T> current, List<T> update, Func<T, string> nameExtractor = null)
        {
            if (AreEquivalent(current, update))
            {
                return; // No changes detected.
            }

            // Append options to remove current service state that's not desired for the update.

            var currentSet = new Dictionary<string, T>();
            var updateSet  = new Dictionary<string, T>();

            for (int i = 0; i < current.Count; i++)
            {
                currentSet[GetStateName(current[i], nameExtractor)] = current[i];
                updateSet[GetStateName(update[i], nameExtractor)]   = update[i];
            }

            // Generate a [*-rm] option to remove state that exists in the current service
            // state but is not desired or will change for the updated service.

            foreach (var currentItem in currentSet.Values)
            {
                var stateName = GetStateName(currentItem, nameExtractor);
                var remove    = false;

                if (updateSet.TryGetValue(stateName, out var updateItem))
                {
                    remove = current.ToString() != updateItem.ToString();
                }
                else
                {
                    remove = true;
                }

                if (remove)
                {
                    outputArgs.Add($"{option}-rm");
                    outputArgs.Add(stateName);
                }
            }

            // Generate a [*-add] option to add state that exists in the updated service
            // or will change for the updated service.

            foreach (var updateItem in updateSet.Values)
            {
                var stateName = GetStateName(updateItem, nameExtractor);
                var add       = false;

                if (currentSet.TryGetValue(stateName, out var currentItem))
                {
                    add = current.ToString() != updateItem.ToString();
                }
                else
                {
                    add = true;
                }

                if (add)
                {
                    outputArgs.Add($"{option}-add");
                    outputArgs.Add(updateItem.ToString());
                }
            }
        }

        /// <summary>
        /// Compares the <paramref name="current"/> and <paramref name="update"/> string values 
        /// describing a service parameter for the current service state and the required updated state and 
        /// when these differ, generates the command line <paramref name="option"/> and values required
        /// to transition the parameter from the current to the updated state within a <b>docker service update</b>
        /// command.  Any generated options will be appended to the <paramref name="outputArgs"/> array.
        /// </summary>
        /// <typeparam name="T">The option type.</typeparam>
        /// <param name="context">The module context.</param>
        /// <param name="outputArgs">The output command line arguments list.</param>
        /// <param name="option">The base command line option (e.g.) <b>--env</b>.</param>
        /// <param name="current">The current service state.</param>
        /// <param name="update">The required updated service state.</param>
        private static void AppendUpdateStringArgs(ModuleContext context, List<string> outputArgs, string option, string current, string update)
        {
            if (string.IsNullOrEmpty(update))
            {
                context.WriteErrorLine($"It is not possible to set an empty [{option}] option.");
            }

            // Return if no change is detected.

            if (current == update)
            {
                return;
            }

            outputArgs.Add($"{option}");
            outputArgs.Add(update.ToString());
        }

        /// <summary>
        /// Compares the <paramref name="current"/> and <paramref name="update"/> <c>double</c> values 
        /// describing a service parameter for the current service state and the required updated state and 
        /// when these differ, generates the command line <paramref name="option"/> and values required
        /// to transition the parameter from the current to the updated state within a <b>docker service update</b>
        /// command.  Any generated options will be appended to the <paramref name="outputArgs"/> array.
        /// </summary>
        /// <typeparam name="T">The option type.</typeparam>
        /// <param name="context">The module context.</param>
        /// <param name="outputArgs">The output command line arguments list.</param>
        /// <param name="option">The base command line option (e.g.) <b>--env</b>.</param>
        /// <param name="current">The current service state.</param>
        /// <param name="update">The required updated service state.</param>
        private static void AppendUpdateDoubleArgs(ModuleContext context, List<string> outputArgs, string option, double? current, double? update)
        {
            // Return if no change is detected.

            if (NeonHelper.NullableEquals(current, update))
            {
                return;
            }

            // Default [update] to 0.0

            if (!update.HasValue)
            {
                update = 0;
            }

            outputArgs.Add($"{option}");
            outputArgs.Add(update.Value.ToString("#.#"));
        }

        /// <summary>
        /// Compares the <paramref name="current"/> and <paramref name="update"/> scaler <c>bool</c> values 
        /// describing a service parameter for the current service state and the required updated state and 
        /// when these differ, generates the command line <paramref name="option"/> and values required
        /// to transition the parameter from the current to the updated state within a <b>docker service update</b>
        /// command.  Any generated options will be appended to the <paramref name="outputArgs"/> array.
        /// </summary>
        /// <typeparam name="T">The option type.</typeparam>
        /// <param name="context">The module context.</param>
        /// <param name="outputArgs">The output command line arguments list.</param>
        /// <param name="option">The base command line option (e.g.) <b>--env</b>.</param>
        /// <param name="current">The current service state.</param>
        /// <param name="update">The required updated service state.</param>
        private static void AppendUpdateBoolArgs(ModuleContext context, List<string> outputArgs, string option, bool? current, bool? update)
        {
            // Return if no change is detected.

            if (NeonHelper.NullableEquals(current, update))
            {
                return;
            }

            // Default [update] to [false].

            if (!update.HasValue)
            {
                update = false;
            }

            outputArgs.Add($"{option}");
            outputArgs.Add(update.Value ? "1" : "0");
        }

        /// <summary>
        /// Compares the <paramref name="current"/> and <paramref name="update"/> scaler enumeration values 
        /// describing a service parameter for the current service state and the required updated state and 
        /// when these differ, generates the command line <paramref name="option"/> and values required
        /// to transition the parameter from the current to the updated state within a <b>docker service update</b>
        /// command.  Any generated options will be appended to the <paramref name="outputArgs"/> array.
        /// </summary>
        /// <typeparam name="T">The option type.</typeparam>
        /// <param name="context">The module context.</param>
        /// <param name="outputArgs">The output command line arguments list.</param>
        /// <param name="option">The base command line option (e.g.) <b>--env</b>.</param>
        /// <param name="current">The current service state.</param>
        /// <param name="update">The required updated service state.</param>
        private static void AppendUpdateEnumArgs<T>(ModuleContext context, List<string> outputArgs, string option, T? current, T? update)
            where T : struct
        {
            // Return if no change is detected.

            if (NeonHelper.NullableEquals(current, update))
            {
                return;
            }

            // Default [update] to the default enum value (the one set to zero).

            if (!update.HasValue)
            {
                update = default(T);
            }

            outputArgs.Add($"{option}");
            outputArgs.Add(NeonHelper.EnumToStringUsingAttributes(update.Value));
        }

        /// <summary>
        /// Compares the <paramref name="current"/> and <paramref name="update"/> duration values describing a
        /// service parameter for the current service state and the required updated state and when
        /// these differ, generates the command line <paramref name="option"/> and values required
        /// to transition the parameter from the current to the updated state within a <b>docker service update</b>
        /// command.  Any generated options will be appended to the <paramref name="outputArgs"/> array.
        /// </summary>
        /// <typeparam name="T">The option type.</typeparam>
        /// <param name="context">The module context.</param>
        /// <param name="outputArgs">The output command line arguments list.</param>
        /// <param name="option">The base command line option (e.g.) <b>--env</b>.</param>
        /// <param name="current">The current service state (nanoseconds).</param>
        /// <param name="update">The required updated service state (nanoseconds).</param>
        private static void AppendUpdateDurationArgs(ModuleContext context, List<string> outputArgs, string option, long? current, long? update)
        {
            // Return if no change is detected.

            if (NeonHelper.NullableEquals(current, update))
            {
                return;
            }

            // $hack(jeff.lill):
            //
            // I'm assuming that I can specify [0ns] to reset to the default value.

            if (!update.HasValue)
            {
                update = 0L;
            }

            outputArgs.Add($"{option}");
            outputArgs.Add($"{update}ns");
        }

        /// <summary>
        /// Compares the <paramref name="current"/> and <paramref name="update"/> <c>int</c> values describing a
        /// service parameter for the current service state and the required updated state and when
        /// these differ, generates the command line <paramref name="option"/> and values required
        /// to transition the parameter from the current to the updated state within a <b>docker service update</b>
        /// command.  Any generated options will be appended to the <paramref name="outputArgs"/> array.
        /// </summary>
        /// <typeparam name="T">The option type.</typeparam>
        /// <param name="context">The module context.</param>
        /// <param name="outputArgs">The output command line arguments list.</param>
        /// <param name="option">The base command line option (e.g.) <b>--env</b>.</param>
        /// <param name="current">The current service state (nanoseconds).</param>
        /// <param name="update">The required updated service state (nanoseconds).</param>
        private static void AppendIntUpdateArgs(ModuleContext context, List<string> outputArgs, string option, int? current, int? update)
        {
            // Return if no change is detected.

            if (NeonHelper.NullableEquals(current, update))
            {
                return;
            }

            // $hack(jeff.lill):
            //
            // I'm assuming that I can specify [0] to reset to the default value.

            if (!update.HasValue)
            {
                update = 0;
            }

            outputArgs.Add($"{option}");
            outputArgs.Add($"{update}");
        }

        /// <summary>
        /// Compares the <paramref name="current"/> and <paramref name="update"/> <c>long</c> values describing a
        /// service parameter for the current service state and the required updated state and when
        /// these differ, generates the command line <paramref name="option"/> and values required
        /// to transition the parameter from the current to the updated state within a <b>docker service update</b>
        /// command.  Any generated options will be appended to the <paramref name="outputArgs"/> array.
        /// </summary>
        /// <typeparam name="T">The option type.</typeparam>
        /// <param name="context">The module context.</param>
        /// <param name="outputArgs">The output command line arguments list.</param>
        /// <param name="option">The base command line option (e.g.) <b>--env</b>.</param>
        /// <param name="current">The current service state (nanoseconds).</param>
        /// <param name="update">The required updated service state (nanoseconds).</param>
        private static void AppendUpdateLongArgs(ModuleContext context, List<string> outputArgs, string option, long? current, long? update)
        {
            // Return if no change is detected.

            if (NeonHelper.NullableEquals(current, update))
            {
                return;
            }

            // $hack(jeff.lill):
            //
            // I'm assuming that I can specify [0] to reset to the default value.

            if (!update.HasValue)
            {
                update = 0L;
            }

            outputArgs.Add($"{option}");
            outputArgs.Add($"{update}");
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public DockerServiceSpec()
        {
        }

        /// <summary>
        /// Optionally specifies service arguments.
        /// </summary>
        public List<string> Args { get; set; } = new List<string>();

        /// <summary>
        /// Optionally specifies credential specifications for Windows managed services.
        /// These are formatted like <b>file://NAME</b> or <b>registry://KEY</b>.
        /// </summary>
        public List<string> CredentialSpec { get; set; } = new List<string>();

        /// <summary>
        /// Identifies the configurations to be made available to the service.
        /// </summary>
        public List<Config> Config { get; set; } = new List<Config>();

        /// <summary>
        /// Specifies service container placement constraints.  These will look
        /// like <b>LABEL=VALUE</b> or <b>LABEL!=VALUE</b>.
        /// </summary>
        public List<string> Constraint { get; set; } = new List<string>();

        /// <summary>
        /// Specifies the service container labels.  These will look like
        /// <b>LABEL=VALUE</b>.
        /// </summary>
        public List<string> ContainerLabel { get; set; } = new List<string>();

        /// <summary>
        /// Indicates that the module should detach immediately from the service
        /// after signalling its creation or update rather than waiting for it
        /// to converge.
        /// </summary>
        public bool? Detach { get; set; }

        /// <summary>
        /// Specifies the DNS nameserver IP addresses for the container.
        /// </summary>
        public List<IPAddress> Dns { get; set; } = new List<IPAddress>();

        /// <summary>
        /// DNS options.  I believe these will be formatted like <b>OPTION=VALUE</b>
        /// but I'm not going to enforce this because I'm not sure.  The options
        /// are described here: http://manpages.ubuntu.com/manpages/precise/man5/resolvconf.conf.5.html
        /// </summary>
        public List<string> DnsOption { get; set; } = new List<string>();

        /// <summary>
        /// Specifies the DNS domains to be searched for non-fully qualified hostnames.
        /// </summary>
        public List<string> DnsSearch { get; set; } = new List<string>();

        /// <summary>
        /// Specifies the endpoint mode.
        /// </summary>
        public ServiceEndpointMode? EndpointMode { get; set; }

        /// <summary>
        /// Optionally overrides the image entrypoint command and arguments.
        /// </summary>
        public List<string> Command { get; set; } = new List<string>();

        /// <summary>
        /// Specifies environment variables to be passed to the service containers.  These
        /// will be formatted as <b>NAME=VALUE</b> to set explicit values or just <b>NAME</b>
        /// to pass the current value of a host variable.
        /// </summary>
        public List<string> Env { get; set; } = new List<string>();

        /// <summary>
        /// Specifies additional service container placement constraints.
        /// </summary>
        public List<string> GenericResource { get; set; } = new List<string>();

        /// <summary>
        /// Specifies supplementary user groups for the service containers.
        /// </summary>
        public List<string> Groups { get; set; } = new List<string>();

        /// <summary>
        /// Optionally specifies the command to be executed within the service containers
        /// to determine the container health status.
        /// </summary>
        /// <remarks>
        /// This list is empty to if the default image health command it to be
        /// used, or else it will start with <b>NONE</b> to disable health checks,
        /// <b>CMD</b> for a regular command followed by optional arguments or 
        /// <b>CMD-SHELL</b> followed by a single argument that will be executed 
        /// in the image's default shell.
        /// </remarks>
        public List<string> HealthCmd { get; set; } = new List<string>();

        /// <summary>
        /// Optionally specifies the interval between health checks (nanoseconds).
        /// </summary>
        public long? HealthInterval { get; set; }

        /// <summary>
        /// Optionally specifies the number of times the <see cref="HealthCmd"/> can
        /// fail before a service container will be considered unhealthy.
        /// </summary>
        public long? HealthRetries { get; set; }

        /// <summary>
        /// Optionally specifies the period after the service container starts when
        /// health check failures will be ignored (nanoseconds).
        /// </summary>
        public long? HealthStartPeriod { get; set; }

        /// <summary>
        /// Optionally specifies the maximum time to wait for a health check to
        /// be completed (nanoseconds).
        /// </summary>
        public long? HealthTimeout { get; set; }

        /// <summary>
        /// Optionally specifies custom host/IP address mappings to be added to the service
        /// container's <b>/etc/hosts</b> file.  These are formatted like <b>HOST:IP</b>.
        /// </summary>
        public List<string> Host { get; set; } = new List<string>();

        /// <summary>
        /// Optionally overrides <see cref="Name"/> as the service's DNS hostname.
        /// </summary>
        public string Hostname { get; set; }

        /// <summary>
        /// Specifies the Docker image.
        /// </summary>
        public string Image { get; set; }

        /// <summary>
        /// Optionally indicates that the service <see cref="Image"/> should not be repulled
        /// and updated if the image and tag are unchanged, ignoring the image SHA-256.
        /// See the remarks for more information.  This defaults to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property has no effect when creating a service for the first time or
        /// if <see cref="Image"/> identifies a specific image via a SHA-256.  In these
        /// cases, the latest identified image will be downloaded and used to launch
        /// the service.
        /// </para>
        /// <para>
        /// When updating a service with <see cref="ImageUpdate"/>=<c>false</c>, the
        /// current service image will be compared to <see cref="Image"/>.  If the 
        /// image and tags match, then no attempt will be made to update the image.
        /// </para>
        /// <para>
        /// When updating a service with <see cref="ImageUpdate"/>=<c>true</c>, the
        /// registry will be checked for a new image and the service will be restarted
        /// to use the new image, if any.
        /// </para>
        /// </remarks>
        public bool? ImageUpdate { get; set; }

        /// <summary>
        /// Service container isolation mode (Windows only).
        /// </summary>
        public ServiceIsolationMode? Isolation { get; set; }

        /// <summary>
        /// Optionally specifies service labels.  These are formatted like <b>NAME=VALUE</b>.
        /// </summary>
        public List<string> Label { get; set; } = new List<string>();

        /// <summary>
        /// Limits the number of CPUs to be assigned to the service containers (double).
        /// </summary>
        public double? LimitCpu { get; set; }

        /// <summary>
        /// Optionally specifies the maximum RAM to be assigned to the container (bytes).
        /// </summary>
        public long? LimitMemory { get; set; }

        /// <summary>
        /// Optionally specifies the service logging driver.
        /// </summary>
        public string LogDriver { get; set; }

        /// <summary>
        /// Optionally specifies the log driver options.
        /// </summary>
        public List<string> LogOpt { get; set; } = new List<string>();

        /// <summary>
        /// Specifies the service mode.
        /// </summary>
        public ServiceMode? Mode { get; set; }

        /// <summary>
        /// Optionally specifies any service filesystem mounts.
        /// </summary>
        public List<Mount> Mount { get; set; } = new List<Mount>();

        /// <summary>
        /// The service name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Optionally specifies any network attachments.
        /// </summary>
        public List<string> Network { get; set; } = new List<string>();

        /// <summary>
        /// Optionally disable container health checks.
        /// </summary>
        public bool? NoHealthCheck { get; set; }

        /// <summary>
        /// Optionally prevent querying the registry to resolve image digests
        /// and supported platforms.
        /// </summary>
        public bool? NoResolveImage { get; set; }

        /// <summary>
        /// Specifies service container placement preferences.  I'm not
        /// entirely sure of the format, so we're not going to parse these.
        /// </summary>
        public List<string> PlacementPref { get; set; } = new List<string>();

        /// <summary>
        /// Optionally publish a service port to the ingress network.
        /// </summary>
        public List<PublishPort> Publish { get; set; } = new List<PublishPort>();

        /// <summary>
        /// Optionally mount the container's root filesystem as read-only.
        /// </summary>
        public bool? ReadOnly { get; set; }

        /// <summary>
        /// Specifies the number of service instances deploy.
        /// </summary>
        public long? Replicas { get; set; }

        /// <summary>
        /// Optionally specifies the number of CPUs to reserve for each service
        /// instance.  This is a double so you can specify things like 1.5 CPUs.
        /// </summary>
        public double? ReserveCpu { get; set; }

        /// <summary>
        /// Optionally specifies the RAM to reserver for each service instance (bytes).
        /// </summary>
        public long? ReserveMemory { get; set; }

        /// <summary>
        /// Optionally specifies the condition when service containers will
        /// be restarted.
        /// </summary>
        public ServiceRestartCondition? RestartCondition { get; set; }

        /// <summary>
        /// Optionally specifies the delay between restart attempts (nanoseconds).
        /// </summary>
        public long? RestartDelay { get; set; }

        /// <summary>
        /// Optionally specifies the maximum number of service container restart attempts.
        /// </summary>
        public long? RestartMaxAttempts { get; set; } = -1;

        /// <summary>
        /// Optionally specifies the Window used to evaluate the restart policy (nanoseconds).
        /// </summary>
        public long? RestartWindow { get; set; }

        /// <summary>
        /// Optionally specifies the delay between service task rollbacks (nanoseconds).
        /// </summary>
        public long? RollbackDelay { get; set; }

        /// <summary>
        /// The action to take when service rollback fails.
        /// </summary>
        public ServiceRollbackFailureAction? RollbackFailureAction { get; set; }

        /// <summary>
        /// Optionally specifies the failure rate to tolerate during a rollback.
        /// </summary>
        public double? RollbackMaxFailureRatio { get; set; }

        /// <summary>
        /// Optionally specifies the time to wait after each task rollback to 
        /// monitor for failure (nanoseconds).
        /// </summary>
        public long? RollbackMonitor { get; set; }

        /// <summary>
        /// Optionally specifies the service task rollback order.
        /// </summary>
        public ServiceRollbackOrder? RollbackOrder { get; set; }

        /// <summary>
        /// Optionally specifies the maximum number of service tasks to be
        /// rolled back at once.
        /// </summary>
        public long? RollbackParallism { get; set; }

        /// <summary>
        /// Optionally specifies the secrets to be exposed to the service.
        /// </summary>
        public List<Secret> Secret { get; set; } = new List<Secret>();

        /// <summary>
        /// Optionally specifies the time to wait for a service container to
        /// stop gracefully after being signalled to stop before Docker will
        /// kill it forcefully (nanoseconds).
        /// </summary>
        public long? StopGracePeriod { get; set; }

        /// <summary>
        /// Optionally specifies the signal to be used to stop service containers.
        /// I believe this can be an integer or a signal name.
        /// </summary>
        public string StopSignal { get; set; }

        /// <summary>
        /// Optionally allocate a TTY for the service containers.
        /// </summary>
        public bool? TTY { get; set; }

        /// <summary>
        /// Optionally specifies the delay between service container updates (nanoseconds).
        /// </summary>
        public long? UpdateDelay { get; set; }

        /// <summary>
        /// Optionally specifies the action to take when a service container update fails.
        /// </summary>
        public ServiceUpdateFailureAction? UpdateFailureAction { get; set; }

        /// <summary>
        /// Optionally specifies the failure rate to tolerate during an update.
        /// </summary>
        public double? UpdateMaxFailureRatio { get; set; }

        /// <summary>
        /// Optionally specifies the time to wait after each service task update to 
        /// monitor for failure (nanoseconds).
        /// </summary>
        public long? UpdateMonitor { get; set; }

        /// <summary>
        /// Optionally specifies the service task update order.
        /// </summary>
        public ServiceUpdateOrder? UpdateOrder { get; set; }

        /// <summary>
        /// Optionally specifies the maximum number of service tasks to be
        /// updated at once.
        /// </summary>
        public long? UpdateParallism { get; set; }

        /// <summary>
        /// Optionally specifies the service container username/group.
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// Optionally sends registry authentication details to swarm agents
        /// hosting the service containers.
        /// </summary>
        public bool? WithRegistryAuth { get; set; }

        /// <summary>
        /// Optionally specifies the working directory within the service container.
        /// This will be set as the current directory before Docker executes a command
        /// within the container.
        /// </summary>
        public string Dir { get; set; }

        /// <summary>
        /// Parsing the JSON responses from a <b>docker service inspect SERVICE</b> for a 
        /// service as well as the table output from a <b>docker network ls --no-trunc</b> 
        /// command listing the current networks.
        /// </summary>
        /// <param name="context">The Annsible module context.</param>
        /// <param name="serviceDetails"><b>docker service inspect SERVICE</b> command output for the service.</param>
        /// <param name="networksText"><b>docker network ls --no-trunc</b> command output.</param>
        /// <returns>The parsed <see cref="DockerServiceSpec"/>.</returns>
        /// <exception cref="Exception">Various exceptions are thrown for errors.</exception>
        private void Parse(ModuleContext context, ServiceDetails serviceDetails, string networksText)
        {
            Covenant.Requires<ArgumentNullException>(serviceDetails != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(networksText));

            //-----------------------------------------------------------------
            // Parse the network definitions so we can map network UUIDs to network names.
            // We're expecting the [docker network ls --no-trunc] output to look like:
            // 
            //  NETWORK ID                                                         NAME                DRIVER              SCOPE
            //  f2c93c25908a391398ef5416940c06322f3ac5f72ea915dd6a09a2efa49677b5   bridge              bridge              local
            //  d28f66e2c56338cdf3b870294ba7a2378d482f0890b89923cd359bf27305c180   docker_gwbridge     bridge              local
            //  b8840a636be38a454bcb0a87e660a37c21f8145112619e00ef632f51a2ca60a5   host                host                local
            //  c3oo8mz0vugdsqjytykcef664                                          ingress             overlay             swarm
            //  tzer69rfle2h6rbcjo9s4b3xo                                          neon-private        overlay             swarm
            //  x9o5teq0z1spgb6md9qmno1rz                                          neon-public         overlay             swarm
            //  f147d7e9bacb06d963bb09a2860953a78ab9c971852c310050a0341aab607e15   none                null                local

            var networkIdToName = new Dictionary<string, string>();

            using (var reader = new StringReader(networksText))
            {
                foreach (var line in reader.Lines().Skip(1))
                {
                    var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (fields.Length >= 2)
                    {
                        networkIdToName.Add(fields[0], fields[1]);
                    }
                }
            }

            // See the link below for more information on the REST API JSON format:
            //
            //      https://docs.docker.com/engine/api/v1.35/#operation/ServiceInspect
            //
            // The parsing code below basically follows the order of the properties
            // as defined by the REST specification.

            //-----------------------------------------------------------------
            // Extract the current service state from the service JSON.

            var spec         = serviceDetails.Spec;
            var taskTemplate = spec.TaskTemplate;

            //-----------------------------------------------------------------
            // Spec.Name

            this.Name = spec.Name;

            //-----------------------------------------------------------------
            // Spec.Labels

            foreach (var item in spec.Labels)
            {
                this.Label.Add($"{item.Key}={item.Value}");
            }

            //-----------------------------------------------------------------
            // Ignoring [Spec.TaskTemplate.PluginSpec] currently experimental
            // and I'm not sure that modifying managed plugins with this module
            // is a great idea anyway.

            //-----------------------------------------------------------------
            // Spec.TaskTemplate.ContainerSpec

            var containerSpec = taskTemplate.ContainerSpec;

            this.Image = containerSpec.Image;

            foreach (var item in containerSpec.Labels)
            {
                this.ContainerLabel.Add($"{item.Key}={item.Value}");
            }

            foreach (var item in containerSpec.Command)
            {
                this.Command.Add(item);
            }

            foreach (string item in containerSpec.Args)
            {
                this.Args.Add(item);
            }

            this.Hostname = containerSpec.Hostname;

            foreach (var item in containerSpec.Env)
            {
                this.Env.Add(item);
            }

            this.Dir  = containerSpec.Dir;
            this.User = containerSpec.User;

            foreach (var item in containerSpec.Groups)
            {
                this.Groups.Add(item);
            }

            // $todo(jeff.lill): Ignoring [Spec.TaskTemplate.Privileges] for now.

            this.TTY = containerSpec.TTY;

            // $todo(jeff.lill): Ignoring [Spec.TaskTemplate.OpenStdin] for now.
            //
            // I think this corresponds to the [docker run -i] flag for containers 
            // but this doesn't make sense for services, right?

            this.ReadOnly = containerSpec.ReadOnly;

            foreach (var item in containerSpec.Mounts)
            {
                var mount = new Mount();

                mount.Target      = item.Target;
                mount.Source      = item.Source;
                mount.Type        = item.Type;
                mount.ReadOnly    = item.ReadOnly;
                mount.Consistency = item.Consistency;

                switch (mount.Type)
                {
                    case ServiceMountType.Bind:

                        mount.BindPropagation = item.BindOptions.Propagation;
                        break;

                    case ServiceMountType.Volume:

                        var volumeOptions = item.VolumeOptions;

                        mount.VolumeNoCopy = volumeOptions.NoCopy;

                        foreach (var label in volumeOptions.Labels)
                        {
                            mount.VolumeLabel.Add($"{label.Key}={label.Value}");
                        }

                        var driverConfig = volumeOptions.DriverConfig;

                        if (driverConfig != null)
                        {
                            mount.VolumeDriver = driverConfig.Name;

                            foreach (var option in driverConfig.Options)
                            {
                                mount.VolumeOpt.Add($"{option.Key}={option.Value}");
                            }
                        }
                        break;

                    case ServiceMountType.Tmpfs:

                        var tmpfsOptions = item.TmpfsOptions;

                        if (tmpfsOptions != null)
                        {
                            mount.TmpfsSize = tmpfsOptions.SizeBytes;
                            mount.TmpfsMode = Convert.ToString(tmpfsOptions.Mode, 8);
                        }
                        break;
                }

                this.Mount.Add(mount);
            }

            this.StopSignal      = containerSpec.StopSignal;
            this.StopGracePeriod = containerSpec.StopGracePeriod;

            // NOTE:
            //
            // [HealthCheck.Test] is either an empty array, indicating that the HEALTHCHECK
            // specified in the image (if any will be used).  Otherwise the first
            // element of the the array describes the health check type with the
            // command itself to follow.  Here's how this looks:
            //
            //      []                      - Use image health check
            //      ["NONE"]                - Disable health checking
            //      ["CMD", args...]        - Execute a command
            //      ["CMD-SHELL", command]  - Run a command in the container's shell

            var healthCheck = containerSpec.HealthCheck;

            foreach (var item in healthCheck.Test)
            {
                this.HealthCmd.Add(item);
            }

            this.HealthInterval    = healthCheck.Interval;
            this.HealthTimeout     = healthCheck.Timeout;
            this.HealthRetries     = healthCheck.Retries;
            this.HealthStartPeriod = healthCheck.StartPeriod;

            foreach (var item in containerSpec.Hosts)
            {
                // NOTE: 
                //
                // The REST API allows additional aliases to be specified after
                // the first host name.  We're going to ignore these because 
                // there's no way to specify these on the command line which
                // specifies these as:
                //
                //      HOST:IP

                var fields = item.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                this.Host.Add($"{fields[0]}:{fields[1]}");
            }

            var dnsConfig = containerSpec.DNSConfig;

            foreach (var item in dnsConfig.Nameservers)
            {
                this.Dns.Add(IPAddress.Parse(item));
            }

            foreach (var item in dnsConfig.Search)
            {
                this.DnsSearch.Add(item);
            }

            foreach (var item in dnsConfig.Options)
            {
                // $todo(jeff.lill):
                //
                // I'm guessing here that the service inspect JSON uses ':'
                // instead of '=' like the command line.  I'm going to 
                // convert any colons to equal signs.

                this.DnsOption.Add(item.Replace(':', '='));
            }

            foreach (var item in containerSpec.Secrets)
            {
                var secret = new Secret();

                secret.Source = item.SecretName;
                secret.Target = item.File.Name;
                secret.UID    = item.File.UID;
                secret.GID    = item.File.GID;
                secret.Mode   = Convert.ToString(item.File.Mode, 8);

                this.Secret.Add(secret);
            }

            foreach (var item in containerSpec.Configs)
            {
                var config = new Config();

                config.Source = item.ConfigName;
                config.Target = item.File.Name;
                config.UID    = item.File.UID;
                config.GID    = item.File.GID;
                config.Mode    = Convert.ToString(item.File.Mode, 8);

                this.Config.Add(config);
            }

            this.Isolation = containerSpec.Isolation;

            //-----------------------------------------------------------------
            // Spec.TaskTemplate.Resources

            // $todo(jeff.lill):
            //
            // I'm ignoring the [Limits.GenericResources] and [Reservation.GenericResources]
            // properties right now.

            const long oneBillion = 1000000000L;

            var resources = taskTemplate.Resources;
            var limits    = resources.Limits;

            this.LimitCpu    = limits.NanoCPUs / oneBillion;
            this.LimitMemory = limits.MemoryBytes;

            var reservation = resources.Reservations;

            this.ReserveCpu    = reservation.NanoCPUs / oneBillion;
            this.ReserveMemory = reservation.MemoryBytes;

            //-----------------------------------------------------------------
            // Spec.TaskTemplate.RestartPolicy

            var restartPolicy = taskTemplate.RestartPolicy;

            this.RestartCondition   = restartPolicy.Condition;
            this.RestartDelay       = restartPolicy.Delay;
            this.RestartMaxAttempts = restartPolicy.MaxAttempts;
            this.RestartWindow      = restartPolicy.Window;

            //-----------------------------------------------------------------
            // Spec.TaskTemplatePlacement

            // $todo(jeff.lill):
            //
            // We're going to ignore the [Preferences] and [Platforms] fields for now.

            foreach (var item in taskTemplate.Placement.Constraints)
            {
                this.Constraint.Add(item);
            }

            // $todo(jeff.lill): Ignoring the [Runtime] property.

            //-----------------------------------------------------------------
            // Spec.TaskTemplate.Network

            // $todo(jeff.lill):
            //
            // Inspect reports referenced networks by UUID, not name.  We'll 
            // use the network map passed to the method to try to associate the
            // network names.
            //
            // Note that it's possible (but unlikely) for the set of cluster networks
            // to have changed between listing them and inspecting the service, so
            // we might not be able to map a network ID to a name.
            //
            // In this case, we won't add the referenced network to this service
            // specification.  The ultimate impact will be to potentially trigger 
            // an unnecessary service update, but since this will be super rare
            // and shouldn't have any adverse impact, so I'm not going to worry
            // about it.

            foreach (var network in taskTemplate.Networks)
            {
                if (networkIdToName.TryGetValue(network.Target, out var networkName))
                {
                    this.Network.Add(networkName);
                }
            }

            //-----------------------------------------------------------------
            // Spec.TaskTemplate.LogDriver

            var logDriver = taskTemplate.LogDriver;

            if (LogDriver != null)
            {
                this.LogDriver = logDriver.Name;

                foreach (var item in logDriver.Options)
                {
                    this.LogOpt.Add($"{item.Key}={item.Value}");
                }
            }

            //-----------------------------------------------------------------
            // Spec.Mode

            if (spec.Mode.Global != null)
            {
                this.Mode = ServiceMode.Global;
            }
            else if (spec.Mode.Replicated != null)
            {
                this.Mode     = ServiceMode.Replicated;
                this.Replicas = spec.Mode.Replicated.Replicas;
            }
            else
            {
                throw new NotSupportedException("Unexpected service [Spec.Mode].");
            }

            //-----------------------------------------------------------------
            // Spec.UpdateConfig

            var updateConfig = spec.UpdateConfig;

            this.UpdateParallism       = updateConfig.Parallelism;
            this.UpdateDelay           = updateConfig.Delay;
            this.UpdateFailureAction   = updateConfig.FailureAction;
            this.UpdateMonitor         = updateConfig.Monitor;
            this.UpdateMaxFailureRatio = updateConfig.MaxFailureRatio;
            this.UpdateOrder           = updateConfig.Order;

            //-----------------------------------------------------------------
            // Spec.RollbackConfig

            var rollbackConfig = spec.RollbackConfig;

            this.RollbackParallism       = rollbackConfig.Parallelism;
            this.RollbackDelay           = rollbackConfig.Delay;
            this.RollbackFailureAction   = rollbackConfig.FailureAction;
            this.RollbackMonitor         = rollbackConfig.Monitor;
            this.RollbackMaxFailureRatio = rollbackConfig.MaxFailureRatio;
            this.RollbackOrder           = rollbackConfig.Order;

            //-----------------------------------------------------------------
            // Spec.EndpointSpec

            var endpointSpec = spec.EndpointSpec;

            this.EndpointMode = endpointSpec.Mode;

            foreach (var item in endpointSpec.Ports)
            {
                var port = new PublishPort();

                port.Name      = item.Name;
                port.Protocol  = item.Protocol;
                port.Target    = item.TargetPort;
                port.Published = item.PublishedPort;
                port.Mode      = item.PublishMode;
            }
        }



        //---------------------------------------------------------------------
        // JSON helpers:

        /// <summary>
        /// Looks up a <see cref="JToken"/> property, returning <c>null</c> if the
        /// property doesn't exist.
        /// </summary>
        /// <param name="jObject">The parent object or <c>null</c>.</param>
        /// <param name="name">The property name.</param>
        /// <returns>
        /// The property token or <c>null</c> if the property doesn't exist or
        /// if <see cref="jObject"/> is <c>null</c>.
        /// </returns>
        private static JToken GetJTokenProperty(JObject jObject, string name)
        {
            if (jObject == null)
            {
                return null;
            }

            if (jObject.TryGetValue(name, out var value))
            {
                return value;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Looks up a <c>string</c> property, returning <c>null</c> if the
        /// property doesn't exist.
        /// </summary>
        /// <param name="jObject">The parent object or <c>null</c>.</param>
        /// <param name="name">The property name.</param>
        /// <returns>
        /// The property string or <c>null</c> if the property doesn't exist or
        /// if <see cref="jObject"/> is <c>null</c>.
        /// </returns>
        private static string GetStringProperty(JObject jObject, string name)
        {
            var jToken = GetJTokenProperty(jObject, name);

            if (jToken != null)
            {
                return (string)jToken;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Looks up a <c>bool</c> property, returning <c>null</c> if the
        /// property doesn't exist.
        /// </summary>
        /// <param name="jObject">The parent object or <c>null</c>.</param>
        /// <param name="name">The property name.</param>
        /// <returns>
        /// The property boolean or <c>null</c> if the property doesn't exist or
        /// if <see cref="jObject"/> is <c>null</c>.
        /// </returns>
        private static bool? GetBoolProperty(JObject jObject, string name)
        {
            var jToken = GetJTokenProperty(jObject, name);

            if (jToken != null)
            {
                switch (jToken.Type)
                {
                    case JTokenType.Boolean:

                        return (bool)jToken;

                    case JTokenType.Integer:

                        return (long)jToken != 0;

                    case JTokenType.Float:

                        return (double)jToken != 0.0;

                    case JTokenType.String:

                        switch (((string)jToken).ToLowerInvariant())
                        {
                            case "0":
                            case "false":
                            case "off":
                            case "no":

                                return false;

                            case "1":
                            case "true":
                            case "on":
                            case "yes":

                                return true;

                            default:

                                return false;
                        }

                    case JTokenType.None:
                    case JTokenType.Null:

                        return false;

                    default:

                        return false;
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Looks up an <c>int</c> property, returning <c>null</c> if the
        /// property doesn't exist.
        /// </summary>
        /// <param name="jObject">The parent object or <c>null</c>.</param>
        /// <param name="name">The property name.</param>
        /// <returns>
        /// The property int or <c>null</c> if the property doesn't exist or
        /// if <see cref="jObject"/> is <c>null</c>.
        /// </returns>
        private static int? GetIntProperty(JObject jObject, string name)
        {
            var jToken = GetJTokenProperty(jObject, name);

            if (jToken == null)
            {
                return null;
            }

            switch (jToken.Type)
            {
                case JTokenType.Integer:

                    return (int)jToken;

                case JTokenType.String:

                    if (int.TryParse((string)jToken, out var value))
                    {
                        return value;
                    }
                    else
                    {
                        return null;
                    }

                default:

                    return null;
            }
        }

        /// <summary>
        /// Looks up a <c>long</c> property, returning <c>null</c> if the
        /// property doesn't exist.
        /// </summary>
        /// <param name="jObject">The parent object or <c>null</c>.</param>
        /// <param name="name">The property name.</param>
        /// <returns>
        /// The property int or <c>null</c> if the property doesn't exist or
        /// if <see cref="jObject"/> is <c>null</c>.
        /// </returns>
        private static long? GetLongProperty(JObject jObject, string name)
        {
            var jToken = GetJTokenProperty(jObject, name);

            if (jToken == null)
            {
                return null;
            }

            switch (jToken.Type)
            {
                case JTokenType.Integer:

                    return (long)jToken;

                case JTokenType.String:

                    if (long.TryParse((string)jToken, out var value))
                    {
                        return value;
                    }
                    else
                    {
                        return null;
                    }

                default:

                    return null;
            }
        }

        /// <summary>
        /// Looks up a <c>double</c> property, returning <c>null</c> if the
        /// property doesn't exist.
        /// </summary>
        /// <param name="jObject">The parent object or <c>null</c>.</param>
        /// <param name="name">The property name.</param>
        /// <returns>
        /// The property int or <c>null</c> if the property doesn't exist or
        /// if <see cref="jObject"/> is <c>null</c>.
        /// </returns>
        private static double? GetDoubleProperty(JObject jObject, string name)
        {
            var jToken = GetJTokenProperty(jObject, name);

            if (jToken == null)
            {
                return null;
            }

            switch (jToken.Type)
            {
                case JTokenType.Float:
                case JTokenType.Integer:

                    return (double)jToken;

                case JTokenType.String:

                    if (double.TryParse((string)jToken, out var value))
                    {
                        return value;
                    }
                    else
                    {
                        return null;
                    }

                default:

                    return null;
            }
        }

        /// <summary>
        /// Looks up an <c>int</c> property converting it to an octal string
        /// or returning <c>null</c> if the property doesn't exist.  This is
        /// handy for parsing Linux style file modes from the decimal integers
        /// Docker reports.
        /// </summary>
        /// <param name="jObject">The parent object or <c>null</c>.</param>
        /// <param name="name">The property name.</param>
        /// <returns>
        /// The property value or <c>null</c> if the property doesn't exist or
        /// if <see cref="jObject"/> is <c>null</c>.
        /// </returns>
        private static string GetFileModeProperty(JObject jObject, string name)
        {
            var decimalMode = GetIntProperty(jObject, name);

            if (decimalMode.HasValue)
            {
                return Convert.ToString(decimalMode.Value, 8);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Looks up an <c>enum</c> property, returning <c>null</c> if the
        /// property doesn't exist.
        /// </summary>
        /// <param name="jObject">The parent object or <c>null</c>.</param>
        /// <param name="name">The property name.</param>
        /// <returns>
        /// The property int or <c>null</c> if the property doesn't exist or
        /// if <see cref="jObject"/> is <c>null</c>.
        /// </returns>
        private static TEnum? GetEnumProperty<TEnum>(JObject jObject, string name)
            where TEnum : struct
        {
            var value = GetStringProperty(jObject, name);

            if (value != null)
            {
                return Enum.Parse<TEnum>(value);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Looks up a <see cref="JObject"/> property value returning an
        /// empty object if the property doesn't exist.
        /// </summary>
        /// <param name="jObject">The parent object or <c>null</c>.</param>
        /// <param name="name">The property name.</param>
        /// <returns>
        /// The property object or <c>null</c> if the property doesn't exist or
        /// if <see cref="jObject"/> is <c>null</c>.
        /// </returns>
        private static JObject GetObjectProperty(JObject jObject, string name)
        {
            var jToken = GetJTokenProperty(jObject, name);
            var value  = jToken as JObject;

            if (value != null)
            {
                return value;
            }
            else
            {
                return new JObject();
            }
        }

        /// <summary>
        /// Looks up a <see cref="JArray"/> property value returning an
        /// empty array if the property doesn't exist.
        /// </summary>
        /// <param name="jObject">The parent object or <c>null</c>.</param>
        /// <param name="name">The property name.</param>
        /// <returns>
        /// The property array or <c>null</c> if the property doesn't exist or
        /// if <see cref="jObject"/> is <c>null</c>.
        /// </returns>
        private static JArray GetArrayProperty(JObject jObject, string name)
        {
            var jToken = GetJTokenProperty(jObject, name);
            var value  = jToken as JArray;

            if (value != null)
            {
                return value;
            }
            else
            {
                return new JArray();
            }
        }
    }
}
