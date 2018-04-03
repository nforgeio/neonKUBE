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
    // NOTE: Durations without units will be assumed to be seconds.
    //
    //                                      create
    // parameter                required    default     choices     comments
    // --------------------------------------------------------------------
    //
    // name                     yes                                 docker service name
    //
    // state                    no          present     absent      indicates whether the service should
    //                                                  present     be created or removed
    //
    // force                    no          false                   forces service update when [state=present]
    //
    // args                     no                                  array of service arguments
    //
    // config                   no                                  array of configuration names
    //
    // constraint               no                                  array of placement constraints like
    //                                                              LABEL==VALUE or LABEL!=VALUE
    //
    // container-label          no                                  array of container labels like
    //                                                              LABEL=VALUE
    //
    // credential-spec          no                                  array of Windows credential specifications
    //
    // detach                   no          false                   specifies whether the service command should
    //                                                              exit immediately or wait for the service changes
    //                                                              to converge
    //
    // dns                      no                                  array of DNS nameserver IP addresses
    //
    // dns-option               no                                  array of DNS options like OPTION=VALUE
    //
    // dns-search               no                                  array of  DNS domains to be searched for 
    //                                                              non-fully qualified hostnames
    //
    // endpoint-mode            no          vip                     service endpoint mode (vip|dnsrr)
    //
    // entrypoint               no                                  array of strings overriding the image entrypoint
    //                                                              command and arguments
    //
    // env                      no                                  array specifying environment variables to be
    //                                                              passed to the container like VARIABLE=VALUE
    //                                                              or just VARIABLE
    //
    // env-file                 no                                  array of host environment variable specification
    //                                                              file paths to be passed to the service containers
    //
    // generic-resource         no                                  array of generic resource requirements for
    //                                                              service placement.
    //
    // group                    no                                  array of service container supplementary user groups 
    //
    // health-cmd               no                                  service container health check command
    //
    // health-interval          no                                  interval between service container health checks
    //
    // health-retries           no                                  number of consecutive health check failures before
    //                                                              a service container is consider unhealthy
    //
    // health-start-period      no                                  delay after service container start before health
    //                                                              checks are enforced
    //
    // health-timeout           no                                  maximum time to wait for a healh check command
    //                                                          
    // host                     no                                  array of hostname to IP address mappings to be
    //                                                              resolved automatically within service containers,
    //                                                              essentially like adding theses to the local 
    //                                                              [/etc/hosts] file.  These are formatted like
    //                                                              HOST:IP.
    //
    // hostname                                                     overrides [Name] as the DNS name for the service.
    //
    // isolation                no          default                 Windows isolation mode (default|process|hyperv)
    //
    // label                    no                                  array of service labels like LABEL=VALUE
    //
    // limit-cpu                no                                  specifies how many CPUs the service containers requires.
    //                                                              This can be a floating point number (e.g. 1.5)
    //
    // limit-memory             no                                  specifies the maximum service container RAM as size 
    //                                                              and units (b|k|m|g)
    //
    // log-driver               no                                  specifies the logging driver
    //
    // log-opt                  no                                  specifies the logging options
    //
    // mode                     no          replicated              specifies the service mode (replicated|global)
    //
    // mount                    no                                  array of structures specifying container bind mounts
    //
    // network                  no                                  array of networks to be attached
    //
    // no-health-check          no          false                   disable service container health checks
    //
    // no-resolve-image         no          false                   disable registry query to resolve image digest 
    //                                                              and supported platforms
    //
    // placement-pref           no                                  array of placement preferences
    //
    // publish                  no                                  array of network port publication specifications like:
    //                                      
    //                                                                  published: 8080
    //                                                                  target: 80
    //                                                                  mode: ingress       (ingress|host}
    //                                                                  protcol: tcp        (tcp|udp|sctp)
    //
    // read-only                no          false                   mount container root filesystem as read-only
    //
    // replicas                 no          1                       number of service tasks
    //
    // reserve-cpu              no                                  CPUs to be reserved for each service container.
    //                                                              This is a floating point number.
    //
    // reserve-memory           no                                  RAM to be reserved for each service container as size 
    //                                                              and units (b|k|m|g)
    //
    // restart-condition        no          any                     specifies restart condition (none|on-failure|any)
    //
    // restart-delay            no          5s                      Delay between service container restart attempts
    //                                                              (ns|us|ms|s|m|h)
    //
    // restart-max-attempts     no          unlimited               maximum number of container restarts to be attempted
    //
    // restart-window           no                                  time window used to evaluate restart policy (ns|us|ms|s|m|h)
    //
    // rollback-delay           no          0s                      delay between task rollbacks (ns|us|ms|s|m|h)
    //
    // rollback-failure-action  no          pause                   action to take on service container rollback failure
    //                                                              (pause|continue)
    //
    // rollback-max-failure-ratio no        0                       failure rate to tolerate during a rollback.
    //
    // rollback-monitor         no          5s                      time to monitor rolled back service containers for
    //                                                              failure (ns|us|ms|s|m|h)
    //
    // rollback-order           no          stop-first              service container rollback order (stop-first|start-first)
    //
    // rollback-parallelism     no          1                       maximum number of service tasks to be rolled back
    //                                                              simultaneously (0 to roll back all at once)
    //
    // secret                   no                                  array of secret names to be be exposed to the service
    //
    // stop-grace-period        no          10s                     maximum time to wait for a service container to 
    //                                                              terminate gracefully (ns|us|ms|s|m|h)
    //
    // stop-signal              no          SIGTERM                 signal to be used to stop service containers
    //
    // tty                      no          false                   allocate a TTY for service containers
    //
    // update-delay             no          0s                      delay between task updates (ns|us|ms|s|m|h)
    //
    // update-failure-action    no          pause                   action to take on service container update failure
    //                                                              (pause|continue)
    //
    // update-max-failure-ratio no          0                       failure rate to tolerate during an update.
    //
    // update-monitor           no          5s                      time to monitor updated service containers for
    //                                                              failure (ns|us|ms|s|m|h)
    //
    // update-order             no          stop-first              service container update order (stop-first|start-first)
    //
    // update-parallelism       no          1                       maximum number of service tasks to be updated
    //                                                              simultaneously (0 to update all at once)
    //
    // user                     no                                  container username of group: <name|uid>[:<group|gid>]
    //
    // with-registry-auth       no          false                   send registry authentication details to Swarm nodes
    //
    // workdir                  no                                  specifies command working directory within containers

    public partial class AnsibleCommand : CommandBase
    {
        //---------------------------------------------------------------------
        // Private types

        // NOTE: The types below are accurate as of Docker API version 1.35.

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
            /// Optionally specifies service arguments.
            /// </summary>
            public List<string> Args { get; private set; } = new List<string>();

            /// <summary>
            /// Optionally specifies credential specifications for Windows managed services.
            /// These are formatted like <b>file://NAME</b> or <b>registry://KEY</b>.
            /// </summary>
            public List<string> CredentialSpec { get; private set; } = new List<string>();

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
            public bool? Detach { get; set; }

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
            public EndpointMode? EndpointMode { get; private set; }

            /// <summary>
            /// Optionally overrides the image entrypoint command and arguments.
            /// </summary>
            public List<string> Entrypoint { get; private set; } = new List<string>();

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
            /// with an optional unit: <b>ns|us|ms|s|m|h</b> (defaults to <b>s</b>).
            /// </summary>
            public string HeathInterval { get; set; }

            /// <summary>
            /// Optionally specifies the number of times the <see cref="HealthCmd"/> can
            /// fail before a service container will be considered unhealthy.
            /// </summary>
            public int? HealthRetries { get; set; }

            /// <summary>
            /// Optionally specifies the period after the service container starts when
            /// health check failures will be ignored. This is an integer with an 
            /// optional unit: <b>ns|us|ms|s|m|h</b> (defaults to <b>s</b>).
            /// </summary>
            public string HealthStartPeriod { get; set; }

            /// <summary>
            /// Optionally specifies the maximum time to wait for a health check to
            /// be completed.   This is an integer with an optional unit: <b>ns|us|ms|s|m|h</b>
            /// (defaults to <b>s</b>).
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
            /// Specifies the Docker image.
            /// </summary>
            public string Image { get; set; }

            /// <summary>
            /// Service container isolation mode (Windows only).
            /// </summary>
            public IsolationMode? IsolationMode { get; set; }

            /// <summary>
            /// Optionally specifies service labels.  These are formatted like <b>NAME=VALUE</b>.
            /// </summary>
            public List<string> Label { get; set; } = new List<string>();

            /// <summary>
            /// Limits the number of CPUs to be assigned to the service containers.
            /// </summary>
            public int? LimitCpu { get; set; }

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
            public ServiceMode? Mode { get; set; }

            /// <summary>
            /// Optionally specifies any service filesystem mounts.
            /// </summary>
            public List<Mount> Mount { get; private set; } = new List<Mount>();

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
            public List<string> PlacementPref { get; private set; } = new List<string>();

            /// <summary>
            /// Optionally publish a service port to the ingress network.
            /// </summary>
            public List<PublishPort> Publish { get; private set; } = new List<PublishPort>();

            /// <summary>
            /// Optionally mount the container's root filesystem as read-only.
            /// </summary>
            public bool? ReadOnly { get; set; }

            /// <summary>
            /// Specifies the number of service instances deploy.
            /// </summary>
            public int? Replicas { get; set; }

            /// <summary>
            /// Optionally specifies the number of CPUs to reserve for each service
            /// instance.  This is a double so you can specify things like 1.5 CPUs.
            /// </summary>
            public double? ReserveCpu { get; set; }

            /// <summary>
            /// Optionally specifies the RAM to reserver for each service instance.
            /// This is an integer followed by a unit: <b>b|k|m|g</b>.
            /// </summary>
            public string ReserveMemory { get; set; }

            /// <summary>
            /// Optionally specifies the condition when service containers will
            /// be restarted.
            /// </summary>
            public RestartCondition? RestartCondition { get; set; }

            /// <summary>
            /// Optionally specifies the delay between restart attempts.  This is
            /// an integer with one of the following units: <b>ns|us|ms|s|m|h</b>
            /// (defaults to <b>s</b>).
            /// </summary>
            public string RestartDelay { get; set; }

            /// <summary>
            /// Optionally specifies the maximum number of service container restart attempts.
            /// </summary>
            public int RestartMaxAttempts { get; set; } = -1;

            /// <summary>
            /// Optionally specifies the Window used to evaluate the restart policy.
            /// This is an integer with one of the following units: <b>ns|us|ms|s|m|h</b>
            /// (defaults to <b>s</b>).
            /// </summary>
            public string RestartWindow { get; set; }

            /// <summary>
            /// Optionally specifies the delay between service task rollbacks.
            /// This is an integer with one of the following units: <b>ns|us|ms|s|m|h</b>
            /// (defaults to <b>s</b>).
            /// </summary>
            public string RollbackDelay { get; set; }

            /// <summary>
            /// Optionally specifies the failure rate to tolerate during a rollback.
            /// </summary>
            public double? RollbackMaxFailureRatio { get; set; }

            /// <summary>
            /// Optionally specifies the time to wait after each task rollback to 
            /// monitor for failure.  This is an integer with one of the following
            /// units: <b>ns|us|ms|s|m|h</b> (defaults to <b>s</b>).
            /// </summary>
            public string RollbackMonitor { get; set; }

            /// <summary>
            /// Optionally specifies the service task rollback order.
            /// </summary>
            public OperationOrder? RollbackOrder { get; set; }

            /// <summary>
            /// Optionally specifies the maximum number of service tasks to be
            /// rolled back at once.
            /// </summary>
            public int? RollbackParallism { get; set; }

            /// <summary>
            /// Optionally specifies the secrets to be exposed to the service.
            /// </summary>
            public List<string> Secret { get; private set; } = new List<string>();

            /// <summary>
            /// Optionally specifies the time to wait for a service container to
            /// stop gracefully after being signalled to stop before Docker will
            /// kill it forcefully.  This is an integer with one of the following
            /// units: <b>ns|us|ms|s|m|h</b> (defaults to <b>s</b>).
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
            public bool? Tty { get; set; }

            /// <summary>
            /// Optionally specifies the delay between service container updates.
            /// This is an integer with one of the following units: <b>ns|us|ms|s|m|h</b>
            /// (defaults to <b>s</b>).
            /// </summary>
            public string UpdateDelay { get; set; }

            /// <summary>
            /// Optionally specifies the action to take when a service container update fails.
            /// </summary>
            public UpdateFailureAction? UpdateFailureAction { get; set; }

            /// <summary>
            /// Optionally specifies the time to wait after each service task update to 
            /// monitor for failure.  This is an integer with one of the following
            /// units: <b>ns|us|ms|s|m|h</b> (defaults to <b>s</b>).
            /// </summary>
            public string UpdateMonitor { get; set; }

            /// <summary>
            /// Optionally specifies the service task update order.
            /// </summary>
            public OperationOrder? UpdateOrder { get; set; }

            /// <summary>
            /// Optionally specifies the maximum number of service tasks to be
            /// updated at once.
            /// </summary>
            public int? UpdateParallism { get; set; }

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

        private enum PortMode
        {
            [EnumMember(Value = "ingress")]
            Ingress = 0,

            [EnumMember(Value = "host")]
            Host
        }

        private enum PortProtocol
        {
            [EnumMember(Value = "tcp")]
            Tcp = 0,

            [EnumMember(Value = "udp")]
            Udp,

            [EnumMember(Value = "sctp")]
            Sctp
        }

        private class PublishPort
        {
            public int Published { get; set;}

            public int Target { get; set; }

            public PortMode Mode { get; set; }

            public PortProtocol Protocol { get; set; }
        }

        private enum MountType
        {
            [EnumMember(Value = "volume")]
            Volume = 0,

            [EnumMember(Value = "bind")]
            Bind,

            [EnumMember(Value = "tempfs")]
            Tmpfs
        }

        private enum MountConsistency
        {
            [EnumMember(Value = "default")]
            Default,

            [EnumMember(Value = "consistent")]
            Consistent,

            [EnumMember(Value = "cached")]
            Cached,

            [EnumMember(Value = "delegated")]
            Delegated
        }

        private enum BindPropagation
        {
            [EnumMember(Value = "rprivate")]
            RPrivate = 0,

            [EnumMember(Value = "shared")]
            Shared,

            [EnumMember(Value = "slave")]
            Slave,

            [EnumMember(Value = "private")]
            Private,

            [EnumMember(Value = "rshared")]
            RShared,

            [EnumMember(Value = "rslave")]
            RSlave
        }

        private class Mount
        {
            public MountType Type { get; set; }

            public string Source { get; set; }

            public string Target { get; set; }

            public bool ReadOnly { get; set; }

            public MountConsistency Consistency { get; set; }

            public BindPropagation BindPropagation { get; set; }

            public string VolumeDriver { get; set; }

            public List<string> VolumeLabel { get; private set; } = new List<string>();

            public bool VolumeNoCopy { get; set; }

            public List<string> VolumeOpt { get; private set; } = new List<string>();

            public long TmpfsSize { get; set; }

            public string TmpfsMode { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Implements the built-in <b>neon_certificate</b> module.
        /// </summary>
        /// <param name="context">The module execution context.</param>
        private void RunDockerServiceModule(ModuleContext context)
        {
            var cluster = NeonClusterHelper.Cluster;

            // Obtain common arguments.

            if (!context.Arguments.TryGetValue<string>("name", out var name))
            {
                throw new ArgumentException($"[name] module argument is required.");
            }

            if (!ClusterDefinition.IsValidName(name))
            {
                throw new ArgumentException($"[name={name}] is not a valid Docker service name.");
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

            // Parse the service definition from the context arguments.

            // $todo(jeff.lill): We could try harder to validate many fields.

            var serviceDef = new DockerService();

            serviceDef.Name = name;

            foreach (var item in context.ParseStringArray("args"))
            {
                serviceDef.Args.Add(item);
            }

            foreach (var item in context.ParseStringArray("config"))
            {
                serviceDef.Config.Add(item);
            }

            foreach (var item in context.ParseStringArray("constraint"))
            {
                serviceDef.Constraint.Add(item);
            }

            foreach (var item in context.ParseStringArray("container-label"))
            {
                serviceDef.ContainerLabel.Add(item);
            }

            foreach (var item in context.ParseStringArray("credential-spec"))
            {
                serviceDef.CredentialSpec.Add(item);
            }

            serviceDef.Detach = context.ParseBool("detach");

            foreach (var item in context.ParseIPAddressArray("dns"))
            {
                serviceDef.Dns.Add(item);
            }

            foreach (var item in context.ParseStringArray("dns-option"))
            {
                serviceDef.DnsOption.Add(item);
            }

            foreach (var item in context.ParseStringArray("dns-search"))
            {
                serviceDef.DnsSearch.Add(item);
            }

            // Abort the operation if any errors were reported during parsing.

            if (context.HasErrors)
            {
                return;
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
