//-----------------------------------------------------------------------------
// FILE:	    AnsibleCommand.Module.DockerService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using Neon.IO;
using Neon.Net;

namespace NeonCli
{
    //---------------------------------------------------------------------
    // neon_docker_service:
    //
    // Synopsis:
    // ---------
    //
    // Manages Docker services.
    //
    // Requirements:
    // -------------
    //
    // This module runs only within the [neon-cli] container when invoked
    // by [neon ansible exec ...] or [neon ansible play ...].
    //
    // Options:
    // --------
    //
    // parameter    required    default     choices     comments
    // --------------------------------------------------------------------
    //
    // name         yes                                 docker service name
    //
    // state        no          present     absent      indicates whether the service should
    //                                      present     be created or removed
    //
    // force        no          false                   forces service update when [state=present]

    public partial class AnsibleCommand : CommandBase
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Specifies a Docker service.
        /// </summary>
        private class DockerService
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            public DockerService()
            {
            }

            /// <summary>
            /// Identifies the configurations to be made available to the service.
            /// These appear to look like file names without a directory.
            /// </summary>
            public List<string> Config { get; private set; } = new List<string>();

            /// <summary>
            /// Specifies service container placement constraints.  These will look
            /// like <b>LABEL=VALUE</b> or <b>LABEL!=VALUE</b>.
            /// </summary>
            public List<string> Constraint { get; private set; } = new List<string>();

            /// <summary>
            /// Specifies the service container labels.  These will look like
            /// <b>LABEL=VALUE</b>.
            /// </summary>
            public List<string> ContainerLabel { get; private set; } = new List<string>();

            /// <summary>
            /// Indicates that the module should detach immediately from the service
            /// after signalling its creation or update rather than waiting for it
            /// to converge.
            /// </summary>
            public bool Detach { get; set; }

            /// <summary>
            /// Specifies the DNS nameserver IP addresses for the container.
            /// </summary>
            public List<IPAddress> Dns { get; private set; } = new List<IPAddress>();

            /// <summary>
            /// DNS options.  I believe these will be formatted like <b>OPTION=VALUE</b>
            /// but I'm not going to enforce this because I'm not sure.  The options
            /// are described here: http://manpages.ubuntu.com/manpages/precise/man5/resolvconf.conf.5.html
            /// </summary>
            public List<string> DnsOption { get; private set; } = new List<string>();

            /// <summary>
            /// Specifies the DNS domains to be searched for non-fully qualified hostnames.
            /// </summary>
            public List<string> DnsSearch { get; private set; } = new List<string>();

            /// <summary>
            /// Specifies the endpoint mode.
            /// </summary>
            public EndpointMode EndpointMode { get; private set; } = EndpointMode.Vip;

            /// <summary>
            /// Optionally overrides the image entrypoint.
            /// </summary>
            public string Entrypoint { get; set; }

            /// <summary>
            /// Specifies environment variables to be passed to the service containers.  These
            /// will be formatted as <b>NAME=VALUE</b> to set explicit values or just <b>NAME</b>
            /// to pass the current value of a host variable.
            /// </summary>
            public List<string> Env { get; private set; } = new List<string>();

            /// <summary>
            /// Specifies the host files with environment variable definitions to be
            /// passed to the service containers.
            /// </summary>
            public List<string> EnvFile { get; private set; } = new List<string>();

            /// <summary>
            /// Specifies additional service container placement constraints.  I'm not
            /// entirely sure of the format, so we're not going to parse these.
            /// </summary>
            public List<string> GenericResource { get; private set; } = new List<string>();

            /// <summary>
            /// Specifies supplementary user groups for the service containers.
            /// </summary>
            public List<string> Group { get; private set; } = new List<string>();

            /// <summary>
            /// Optionally specifies the command to be executed within the service containers
            /// to determine the container health status.
            /// </summary>
            public string HealthCmd { get; set; }

            /// <summary>
            /// Optionally specifies the interval between health checks.  This is an integer
            /// with an optional unit: <b>ms|s|m|h</b>.
            /// </summary>
            public string HeathInterval { get; set; }

            /// <summary>
            /// Optionally specifies the number of times the <see cref="HealthCmd"/> can
            /// fail before a service container will be considered unhealthy.
            /// </summary>
            public int HealthRetries { get; set; } = -1;

            /// <summary>
            /// Optionally specifies the period after the service container starts when
            /// health check failures will be ignored. This is an integer with an 
            /// optional unit: <b>ms|s|m|h</b>.
            /// </summary>
            public string HealthStartPeriod { get; set; }

            /// <summary>
            /// Optionally specifies the maximum time to wait for a health check to
            /// be completed.   This is an integer with an optional unit: <b>ms|s|m|h</b>.
            /// </summary>
            public string HealthTimeout { get; set; }

            /// <summary>
            /// Optionally specifies custom host/IP address mappings to be added to the service
            /// container's <b>/etc/hosts</b> file.  These are formatted like <b>HOST:IP</b>.
            /// </summary>
            public List<string> Host { get; private set; } = new List<string>();

            /// <summary>
            /// Optionally overrides <see cref="Name"/> as the service's DNS hostname.
            /// </summary>
            public string Hostname { get; set; }

            /// <summary>
            /// Service container isolation mode (Windows only).
            /// </summary>
            public IsolationMode IsolationMode { get; set; }

            /// <summary>
            /// Optionally specifies service labels.  These are formatted like <b>NAME=VALUE</b>.
            /// </summary>
            public List<string> Label { get; set; } = new List<string>();

            /// <summary>
            /// Limits the number of CPUs to be assigned to the service containers.
            /// </summary>
            public int LimitCpu { get; set; } = -1;

            /// <summary>
            /// Optionally specifies the maximum RAM to be assigned to the container.
            /// This is an integer followed by a unit: <b>b|k|m|g</b>.
            /// </summary>
            public string LimitMemory { get; set; }

            /// <summary>
            /// Optionally specifies the service logging driver.
            /// </summary>
            public string LogDriver { get; set; }

            /// <summary>
            /// Optionally specifies the log driver options.
            /// </summary>
            public string LogOpt { get; set; }

            /// <summary>
            /// Specifies the service mode.
            /// </summary>
            public ServiceMode Mode { get; set; } = ServiceMode.Replicated;

            /// <summary>
            /// Optionally specifies any service filesystem mounts.  We're not going to
            /// try to parse this and will just pass anything through.
            /// </summary>
            public List<string> Mount { get; private set; } = new List<string>();

            /// <summary>
            /// The service name.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Optionally specifies any network attachments.
            /// </summary>
            public List<string> Network { get; private set; } = new List<string>();

            /// <summary>
            /// Optionally disable container health checks.
            /// </summary>
            public bool NoHealthCheck { get; set; }

            /// <summary>
            /// Optionally prevent querying the registry to resolve image digests
            /// and supported platforms.
            /// </summary>
            public bool NoResolveImage { get; set; }

            /// <summary>
            /// Specifies service container placement preferences.  I'm not
            /// entirely sure of the format, so we're not going to parse these.
            /// </summary>
            public List<string> PlacementPref { get; private set; } = new List<string>();

            /// <summary>
            /// Optionally publish a service port to the ingress network.  I'm
            /// not going to try to parse these.
            /// </summary>
            public List<string> Publish { get; private set; } = new List<string>();

            /// <summary>
            /// Optionally mount the container's root filesystem as read-only.
            /// </summary>
            public bool ReadOnly { get; set; }

            /// <summary>
            /// Specifies the number of service instances deploy.
            /// </summary>
            public int Replicas { get; set; } = 1;

            /// <summary>
            /// Optionally specifies the number of CPUs to reserve for each service
            /// instance.  This is a double so you can specify things like 1.5 CPUs.
            /// </summary>
            public double ReserveCpu { get; set; } = -1.0;

            /// <summary>
            /// Optionally specifies the RAM to reserver for each service instance.
            /// This is an integer followed by a unit: <b>b|k|m|g</b>.
            /// </summary>
            public string ReserveMemory { get; set; }

            /// <summary>
            /// Optionally specifies the condition when service containers will
            /// be restarted.
            /// </summary>
            public RestartCondition RestartCondition { get; set; } = RestartCondition.Any;

            /// <summary>
            /// Optionally specifies the delay between restart attempts.  This is
            /// an integer with one of the following units: <b>ns|us|ms|s|m|h</b>.
            /// </summary>
            public string RestartDelay { get; set; }

            /// <summary>
            /// Optionally specifies the maximum number of service container restart attempts.
            /// </summary>
            public int RestartMaxAttempts { get; set; } = -1;

            /// <summary>
            /// Optionally specifies the Window used to evaluate the restart policy.
            /// This is an integer with one of the following units: <b>ns|us|ms|s|m|h</b>.
            /// </summary>
            public string RestartWindow { get; set; }

            /// <summary>
            /// Optionally specifies the delay between service task rollbacks.
            /// This is an integer with one of the following units: <b>ns|us|ms|s|m|h</b>.
            /// </summary>
            public string RollbackDelay { get; set; }

            /// <summary>
            /// Optionally specifies the failure rate to tolerate during a rollback.
            /// </summary>
            public double RollbackMaxFailureRatio { get; set; } = -1.0;

            /// <summary>
            /// Optionally specifies the time to wait after each task rollback to 
            /// monitor for failure.  This is an integer with one of the following
            /// units: <b>ns|us|ms|s|m|h</b>.
            /// </summary>
            public string RollbackMonitor { get; set; }

            /// <summary>
            /// Optionally specifies the service task rollback order.
            /// </summary>
            public OperationOrder RollbackOrder { get; set; } = OperationOrder.StopFirst;

            /// <summary>
            /// Optionally specifies the maximum number of service tasks to be
            /// rolled back at once.
            /// </summary>
            public int RollbackParallism { get; set; } = -1;

            /// <summary>
            /// Optionally specifies the secrets to be exposed to the service.
            /// </summary>
            public List<string> Secret { get; private set; } = new List<string>();

            /// <summary>
            /// Optionally specifies the time to wait for a service container to
            /// stop gracefully after being signalled to stop before Docker will
            /// kill it forcefully.  This is an integer with one of the following
            /// units: <b>ns|us|ms|s|m|h</b>.
            /// </summary>
            public string StopGracePeriod { get; set; }

            /// <summary>
            /// Optionally specifies the signal to be used to stop service containers.
            /// I believe this can be an integer or a signal name.  I'm not going to
            /// parse it.
            /// </summary>
            public string StopSignal { get; set; }

            /// <summary>
            /// Optionally allocate a TTY for the service containers.
            /// </summary>
            public bool Tty { get; set; }

            /// <summary>
            /// Optionally specifies the delay between service container updates.
            /// This is an integer with one of the following units: <b>ns|us|ms|s|m|h</b>.
            /// </summary>
            public string UpdateDelay { get; set; }

            /// <summary>
            /// Optionally specifies the action to take when a service container update fails.
            /// </summary>
            public UpdateFailureAction UpdateFailureAction { get; set; } = UpdateFailureAction.Pause;

            /// <summary>
            /// Optionally specifies the time to wait after each service task update to 
            /// monitor for failure.  This is an integer with one of the following
            /// units: <b>ns|us|ms|s|m|h</b>.
            /// </summary>
            public string UpdateMonitor { get; set; }

            /// <summary>
            /// Optionally specifies the service task update order.
            /// </summary>
            public OperationOrder UpdateOrder { get; set; } = OperationOrder.StopFirst;

            /// <summary>
            /// Optionally specifies the maximum number of service tasks to be
            /// updated at once.
            /// </summary>
            public int UpdatekParallism { get; set; } = -1;

            /// <summary>
            /// Optionally specifies the service container username/group.
            /// </summary>
            public string User { get; set; }

            /// <summary>
            /// Optionally sends registry authentication details to swarm agents
            /// hosting the service containers.
            /// </summary>
            public bool WithRegistryAuth { get; set; }

            /// <summary>
            /// Optionally specifies the working directory within the service container.
            /// This will be set as the current directory before Docker executes a command
            /// within the container.
            /// </summary>
            public string WorkDir { get; set; }
        }

        private enum EndpointMode
        {
            [EnumMember(Value = "vip")]
            Vip = 0,

            [EnumMember(Value = "dnsrr")]
            DnsRR
        }

        private enum IsolationMode
        {
            [EnumMember(Value = "default")]
            Default = 0,

            [EnumMember(Value = "process")]
            Process,

            [EnumMember(Value = "hyperv")]
            HyperV
        }

        private enum ServiceMode
        {
            [EnumMember(Value = "replicated")]
            Replicated = 0,

            [EnumMember(Value = "global")]
            Global
        }

        private enum RestartCondition
        {
            [EnumMember(Value = "any")]
            Any = 0,

            [EnumMember(Value = "none")]
            None,

            [EnumMember(Value = "on-failure")]
            OnFailure
        }

        private enum OperationOrder
        {
            [EnumMember(Value = "stop-first")]
            StopFirst = 0,

            [EnumMember(Value = "start-first")]
            StartFirst
        }

        private enum UpdateFailureAction
        {
            [EnumMember(Value = "pause")]
            Pause = 0,

            [EnumMember(Value = "continue")]
            Continue,

            [EnumMember(Value = "rollback")]
            Rollback
        }

        //---------------------------------------------------------------------
        // Implementation

            /// <summary>
            /// Implements the built-in <b>neon_certificate</b> module.
            /// </summary>
            /// <param name="context">The module execution context.</param>
        private void RunDockerServiceModule(ModuleContext context)
        {
            // Obtain common arguments.

            if (!context.Arguments.TryGetValue<string>("name", out var name))
            {
                throw new ArgumentException($"[name] module argument is required.");
            }

            if (!ClusterDefinition.IsValidName(name))
            {
                throw new ArgumentException($"[name={name}] is not a valid service name.");
            }

            if (!context.Arguments.TryGetValue<string>("state", out var state))
            {
                state = "present";
            }

            state = state.ToLowerInvariant();

            if (!context.Arguments.TryGetValue<bool>("force", out var force))
            {
                force = false;
            }

            // We have the required arguments, so perform the operation.

            switch (state)
            {
                case "absent":

                    break;

                case "present":

                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [absent] or [present].");
            }
        }
    }
}
