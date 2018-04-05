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
    // NOTE: Only the [name] parameter is required when [state=absent].
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
    // detach                   no          false       true        specifies whether the service command should
    //                                                  false       exit immediately or wait for the service changes
    //                                                              to converge
    //
    // dns                      no                                  array of DNS nameserver IP addresses
    //
    // dns-option               no                                  array of DNS options like OPTION=VALUE
    //
    // dns-search               no                                  array of  DNS domains to be searched for 
    //                                                              non-fully qualified hostnames
    //
    // endpoint-mode            no          vip         vip         service endpoint mode
    //                                                  dnsrr
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
    // health-cmd               no                                  array of the service container health check command
    //                                                              and arguments
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
    // image                    yes                                 specifies the Docker image 
    //
    // isolation                no          default     default     Windows isolation mode
    //                                                  process
    //                                                  hyperv
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
    // log-opt                  no                                  specifies the logging options as an array of OPTION=VALUE strings.
    //
    // mode                     no          replicated  replicated  specifies the service mode
    //                                                  global
    //
    // mount                    no                                  array of structures specifying container bind mounts like:
    //                                                              
    //                                                                  type: volume                default: volume     (volume|bind|tmpfs)
    //                                                                  source: NAME/PATH
    //                                                                  target: PATH
    //                                                                  readonly: true/false        default: false      (true|false)
    //                                                                  consistency: default        default: default    (default|consistent|cached|delegated)
    //                                                                  bind-propagation: rprivate  default: rprivate   (shared|slave|private|rshared,
    //                                                                                                                   rslave|rprivate)
    //                                                                  options for: type=volume
    //                                                                  ------------------------
    //                                                                  volume-driver: local        default: local
    //                                                                  volume-label: [ ... ]       array of NAME=VALUE volume label strings
    //                                                                  volume-nocopy: true         default: true       (true|false)
    //                                                                  volume-opt: [ ... ]         array of OPTION=VALUE volume options
    //
    //                                                                  options for: type=tmpfs
    //                                                                  -----------------------
    //                                                                  tmpfs-size: 1000000         default: unlimited
    //                                                                  tmpfs-mode: 1777            default: 1777
    //
    // network                  no                                  array of networks to be attached
    //
    // no-health-check          no          false       true        disable service container health checks
    //                                                  false
    //
    // no-resolve-image         no          false       true        disable registry query to resolve image digest 
    //                                                  false       and supported platforms
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
    // read-only                no          false       true        mount container root filesystem as read-only
    //                                                  false
    //
    // replicas                 no          1                       number of service tasks
    //
    // reserve-cpu              no                                  CPUs to be reserved for each service container.
    //                                                              This is a floating point number.
    //
    // reserve-memory           no                                  RAM to be reserved for each service container as size 
    //                                                              and units (b|k|m|g)
    //
    // restart-condition        no          any         any         specifies restart condition
    //                                                  none
    //                                                  on-failure
    //
    // restart-delay            no          5s          any         Delay between service container restart attempts
    //                                                              (ns|us|ms|s|m|h)
    //
    // restart-max-attempts     no          unlimited               maximum number of container restarts to be attempted
    //
    // restart-window           no                                  time window used to evaluate restart policy (ns|us|ms|s|m|h)
    //
    // rollback-delay           no          0s                      delay between task rollbacks (ns|us|ms|s|m|h)
    //
    // rollback-failure-action  no          pause       pause       action to take on service container rollback failure
    //                                                  continue
    //
    // rollback-max-failure-ratio no        0                       failure rate to tolerate during a rollback.
    //
    // rollback-monitor         no          5s                      time to monitor rolled back service containers for
    //                                                              failure (ns|us|ms|s|m|h)
    //
    // rollback-order           no          stop-first  stop-first  service container rollback order (stop-first|start-first)
    //                                                  start-first
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
    // update-failure-action    no          pause       pause       action to take on service container update failure
    //                                                  continue
    //                                                  rollback
    //
    // update-max-failure-ratio no          0                       failure rate to tolerate during an update.
    //
    // update-monitor           no          5s                      time to monitor updated service containers for
    //                                                              failure (ns|us|ms|s|m|h)
    //
    // update-order             no          stop-first  stop-first  service container update order
    //                                                  start-first
    //
    // update-parallelism       no          1                       maximum number of service tasks to be updated
    //                                                              simultaneously (0 to update all at once)
    //
    // user                     no                                  container username/group: <name|uid>[:<group|gid>]
    //
    // with-registry-auth       no          false                   send registry authentication details to Swarm nodes
    //
    // workdir                  no                                  specifies command working directory within containers
    //
    // Examples:
    // ---------
    //

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
            public List<string> Args { get; set; } = new List<string>();

            /// <summary>
            /// Optionally specifies credential specifications for Windows managed services.
            /// These are formatted like <b>file://NAME</b> or <b>registry://KEY</b>.
            /// </summary>
            public List<string> CredentialSpec { get; set; } = new List<string>();

            /// <summary>
            /// Identifies the configurations to be made available to the service.
            /// These appear to look like file names without a directory.
            /// </summary>
            public List<string> Config { get; set; } = new List<string>();

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
            public EndpointMode? EndpointMode { get; set; }

            /// <summary>
            /// Optionally overrides the image entrypoint command and arguments.
            /// </summary>
            public List<string> Entrypoint { get; set; } = new List<string>();

            /// <summary>
            /// Specifies environment variables to be passed to the service containers.  These
            /// will be formatted as <b>NAME=VALUE</b> to set explicit values or just <b>NAME</b>
            /// to pass the current value of a host variable.
            /// </summary>
            public List<string> Env { get; set; } = new List<string>();

            /// <summary>
            /// Specifies the host files with environment variable definitions to be
            /// passed to the service containers.
            /// </summary>
            public List<string> EnvFile { get; set; } = new List<string>();

            /// <summary>
            /// Specifies additional service container placement constraints.
            /// </summary>
            public List<string> GenericResource { get; set; } = new List<string>();

            /// <summary>
            /// Specifies supplementary user groups for the service containers.
            /// </summary>
            public List<string> Group { get; set; } = new List<string>();

            /// <summary>
            /// Optionally specifies the command to be executed within the service containers
            /// to determine the container health status.
            /// </summary>
            public List<string> HealthCmd { get; set; } = new List<string>();

            /// <summary>
            /// Optionally specifies the interval between health checks (nanoseconds).
            /// </summary>
            public long? HealthInterval { get; set; }

            /// <summary>
            /// Optionally specifies the number of times the <see cref="HealthCmd"/> can
            /// fail before a service container will be considered unhealthy.
            /// </summary>
            public int? HealthRetries { get; set; }

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
            /// Service container isolation mode (Windows only).
            /// </summary>
            public IsolationMode? Isolation { get; set; }

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
            public int? Replicas { get; set; }

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
            public RestartCondition? RestartCondition { get; set; }

            /// <summary>
            /// Optionally specifies the delay between restart attempts (nanoseconds).
            /// </summary>
            public long? RestartDelay { get; set; }

            /// <summary>
            /// Optionally specifies the maximum number of service container restart attempts.
            /// </summary>
            public int? RestartMaxAttempts { get; set; } = -1;

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
            public RollbackFailureAction? RollbackFailureAction { get; set; }

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
            public RollbackOrder? RollbackOrder { get; set; }

            /// <summary>
            /// Optionally specifies the maximum number of service tasks to be
            /// rolled back at once.
            /// </summary>
            public int? RollbackParallism { get; set; }

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
            public bool? Tty { get; set; }

            /// <summary>
            /// Optionally specifies the delay between service container updates (nanoseconds).
            /// </summary>
            public long? UpdateDelay { get; set; }

            /// <summary>
            /// Optionally specifies the action to take when a service container update fails.
            /// </summary>
            public UpdateFailureAction? UpdateFailureAction { get; set; }

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
            public UpdateOrder? UpdateOrder { get; set; }

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

        private enum UpdateOrder
        {
            [EnumMember(Value = "stop-first")]
            StopFirst = 0,

            [EnumMember(Value = "start-first")]
            StartFirst
        }

        private enum RollbackOrder
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

        private enum RollbackFailureAction
        {
            [EnumMember(Value = "pause")]
            Pause = 0,

            [EnumMember(Value = "continue")]
            Continue,
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

            [EnumMember(Value = "tmpfs")]
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

        private enum MountBindPropagation
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

            public bool? ReadOnly { get; set; }

            public MountConsistency? Consistency { get; set; }

            public MountBindPropagation? BindPropagation { get; set; }

            public string VolumeDriver { get; set; }

            public List<string> VolumeLabel { get; private set; } = new List<string>();

            public bool? VolumeNoCopy { get; set; }

            public List<string> VolumeOpt { get; private set; } = new List<string>();

            public long? TmpfsSize { get; set; }

            public string TmpfsMode { get; set; }
        }

        public class Secret
        {
            public string Source { get; set; }
            
            public string Target { get; set; }

            public string Uid { get; set; }

            public string Gid { get; set; }

            public string Mode { get; set; }
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

            context.WriteLine(AnsibleVerbosity.Trace, "Parsing common parameters.");

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

            // Parse the service definition from the context parameters.

            context.WriteLine(AnsibleVerbosity.Trace, "Parsing service parameters.");

            var service = new DockerService();

            service.Name                    = name;

            service.Args                    = context.ParseStringArray("args");
            service.Config                  = context.ParseStringArray("config");
            service.Constraint              = context.ParseStringArray("constraint");
            service.ContainerLabel          = context.ParseStringArray("container-label");
            service.CredentialSpec          = context.ParseStringArray("credential-spec");
            service.Detach                  = context.ParseBool("detach");
            service.Dns                     = context.ParseIPAddressArray("dns");
            service.Entrypoint              = context.ParseStringArray("entrypoint");
            service.Env                     = context.ParseStringArray("env");
            service.EnvFile                 = context.ParseStringArray("env-file");
            service.GenericResource         = context.ParseStringArray("generic-resource");
            service.Group                   = context.ParseStringArray("group");
            service.HealthCmd               = context.ParseStringArray("health-cmd");
            service.HealthInterval           = context.ParseDockerInterval("health-interval");
            service.HealthRetries           = context.ParseInt("health-retries", v => v >= 0);
            service.HealthStartPeriod       = context.ParseDockerInterval("health-start-period");
            service.HealthTimeout           = context.ParseDockerInterval("health-timeout");
            service.Host                    = context.ParseStringArray("host");
            service.Hostname                = context.ParseString("hostname");
            service.Image                   = context.ParseString("image");
            service.Isolation               = context.ParseEnum<IsolationMode>("isolation");
            service.Label                   = context.ParseStringArray("label");
            service.LimitCpu                = context.ParseDouble("limit-cpu", v => v > 0);
            service.LimitMemory             = context.ParseDockerMemorySize("limit-memory");
            service.LogDriver               = context.ParseString("log-driver");
            service.LogOpt                  = context.ParseStringArray("log-opt");
            service.Mode                    = context.ParseEnum<ServiceMode>("mode");
            service.Mount                   = ParseMounts(context, "mount");
            service.Network                 = context.ParseStringArray("network");
            service.NoHealthCheck           = context.ParseBool("no-health-check");
            service.NoResolveImage          = context.ParseBool("no-resolve-image");
            service.PlacementPref           = context.ParseStringArray("placement-pref");
            service.Publish                 = ParsePublishPorts(context, "publish");
            service.ReadOnly                = context.ParseBool("read-only");
            service.Replicas                = context.ParseInt("replicas", v => v >= 0);
            service.ReserveCpu              = context.ParseDouble("reserve-cpu", v => v > 0);
            service.ReserveMemory           = context.ParseDockerMemorySize("reserve-memory");
            service.RestartCondition        = context.ParseEnum<RestartCondition>("restart-condition");
            service.RestartDelay            = context.ParseDockerInterval("restart-delay");
            service.RestartMaxAttempts      = context.ParseInt("restart-max-attempts", v => v >= 0);
            service.RestartWindow           = context.ParseDockerInterval("restart-window");
            service.RollbackDelay           = context.ParseDockerInterval("rollback-delay");
            service.RollbackFailureAction   = context.ParseEnum<RollbackFailureAction>("rollback-failure-action");
            service.RollbackMaxFailureRatio = context.ParseDouble("rollback-max-failure-ratio", v => v >= 0);
            service.RollbackMonitor         = context.ParseDockerInterval("rollback-monitor");
            service.RollbackOrder           = context.ParseEnum<RollbackOrder>("rollback-order");
            service.RollbackParallism       = context.ParseInt("rollback-parallelism", v => v > 0);
            service.Secret                  = ParseSecretArray(context, "secret");
            service.StopGracePeriod         = context.ParseDockerInterval("stop-grace-period");
            service.StopSignal              = context.ParseString("stop-signal");
            service.ReadOnly                = context.ParseBool("read-only");
            service.Tty                     = context.ParseBool("tty");
            service.UpdateDelay             = context.ParseDockerInterval("update-delay");
            service.UpdateFailureAction     = context.ParseEnum<UpdateFailureAction>("update-failure-action");
            service.UpdateMaxFailureRatio   = context.ParseDouble("update-max-failure-ratio", v => v >= 0);
            service.UpdateMonitor           = context.ParseDockerInterval("update-monitor");
            service.UpdateOrder             = context.ParseEnum<UpdateOrder>("update-order");
            service.UpdateParallism         = context.ParseInt("update-parallelism", v => v > 0);
            service.User                    = context.ParseString("user");
            service.WithRegistryAuth        = context.ParseBool("with-registry-auth");
            service.WorkDir                 = context.ParseString("workdir");

            // Abort the operation if any errors were reported during parsing.

            if (context.HasErrors)
            {
                return;
            }

            // We have the required arguments, so perform the operation.
            //
            // Detect whether the service is already running by inspecting it
            // then start when it's nor already running or update it if it is.

            context.WriteLine(AnsibleVerbosity.Trace, $"Inspecting [{service.Name}] service.");

            var manager      = cluster.GetHealthyManager();
            var response     = manager.DockerCommand(RunOptions.None, "docker service inspect", service.Name);
            var serviceState = (string)null;

            if (response.ExitCode == 0)
            {
                serviceState = response.OutputText;
                context.WriteLine(AnsibleVerbosity.Trace, $"{service.Name}] service exists.");
            }
            else
            {
                // $todo(jeff.lill): 
                //
                // I'm trying to distinguish between a a failure because the service doesn't
                // exist and other potential failures (e.g. Docker is not running).
                //
                // This is a bit fragile.

                if (response.ErrorText.StartsWith("Status: Error: no such service:", StringComparison.InvariantCultureIgnoreCase))
                {
                    context.WriteLine(AnsibleVerbosity.Trace, $"{service.Name}] service does not exist.");
                }
                else
                {
                    context.WriteErrorLine(response.ErrorText);
                    return;
                }
            }

            if (context.HasErrors)
            {
                return;
            }

            switch (state)
            {
                case "absent":

                    context.WriteLine(AnsibleVerbosity.Trace, $"[state=absent] so removing [{service.Name}] service if it is running.");

                    if (serviceState == null)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"No change required because [{service.Name}] service is not running.");
                    }
                    else
                    {
                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"[{service.Name}] service would be removed when CHECKMODE is disabled.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Removing [{service.Name}] service.");

                            response = manager.DockerCommand(RunOptions.None, $"docker service rm {service.Name}");

                            if (response.ExitCode == 0)
                            {
                                context.WriteLine(AnsibleVerbosity.Info, $"[{service.Name}] service was removed.");
                            }
                            else
                            {
                                context.WriteErrorLine(response.AllText);
                            }
                        }

                        context.Changed = true;
                    }
                    break;

                case "present":

                    // Perform some minimal parameter validation.

                    // $todo(jeff.lill): We could try a lot harder to validate the service fields.

                    if (service.Image == null)
                    {
                        context.WriteErrorLine("The [image] parameter is required.");
                        return;
                    }

                    if (serviceState == null)
                    {
                        context.Changed = true;

                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"[{service.Name}] service would be created when CHECKMODE is disabled.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Creating [{service.Name}] service.");
                            CreateService(manager, context, service);
                            context.WriteLine(AnsibleVerbosity.Info, $"[{service.Name}] service created.");
                        }
                    }
                    else
                    {
                        // NOTE: UpdateService() handles the CheckMode logic and context logging.

                        UpdateService(manager, context, service, serviceState);
                    }
                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [absent] or [present].");
            }
        }

        /// <summary>
        /// Parses the service's bind mounts.
        /// </summary>
        /// <param name="context">The module context.</param>
        /// <param name="argName">The module argument name.</param>
        /// <returns>The list of <see cref="Mount"/> instances.</returns>
        private List<Mount> ParseMounts(ModuleContext context, string argName)
        {
            var mounts = new List<Mount>();

            if (!context.Arguments.TryGetValue(argName, out var jToken))
            {
                return mounts;
            }

            var jArray = jToken as JArray;

            if (jArray == null)
            {
                context.WriteErrorLine($"Expected [{argName}] to be an array of bind mount specifications.");
                return mounts;
            }

            foreach (var item in jArray)
            {
                var jObject = item as JObject;

                if (jObject != null)
                {
                    context.WriteErrorLine($"One or more of the [{argName}] array elements is not a valid bind mount specification.");
                    return mounts;
                }

                var mount = new Mount();
                var value = String.Empty;

                // Parse [type]

                if (jObject.TryGetValue<string>("type", out value))
                {
                    if (Enum.TryParse<MountType>(value, true, out var mountType))
                    {
                        mount.Type = mountType;
                    }
                    else
                    {
                        context.WriteErrorLine($"One of the [{argName}] array elements specifies the invalid [type={value}].");
                        return mounts;
                    }
                }
                else
                {
                    mount.Type = MountType.Volume;
                }

                // Parse [source]

                if (jObject.TryGetValue<string>("source", out value))
                {
                    mount.Source = value;
                }

                // Parse [target]

                if (jObject.TryGetValue<string>("target", out value))
                {
                    mount.Target = value;
                }

                // Parse [readonly]

                if (jObject.TryGetValue<string>("readonly", out value))
                {
                    mount.ReadOnly = context.ParseBool(value, "Invalid [mount.readonly] value.");
                }

                // Parse [consistency]

                if (jObject.TryGetValue<string>("consistency", out value))
                {
                    mount.Consistency = context.ParseEnum<MountConsistency>(value, $"Invalid [mount.consistency={value}] value.");
                }

                // Parse [bind-propagation]

                if (jObject.TryGetValue<string>("bind-propagation", out value))
                {
                    mount.BindPropagation = context.ParseEnum<MountBindPropagation>(value, $"Invalid [mount.bind-propagation={value}] value.");
                }

                // Parse the [volume] related options.

                if (mount.Type == MountType.Volume)
                {
                    // Parse [volume-driver]

                    if (jObject.TryGetValue<string>("volume-driver", out value))
                    {
                        mount.VolumeDriver = value;
                    }
                    else
                    {
                        mount.VolumeDriver = "local";
                    }

                    // Parse [volume-label]

                    if (jObject.TryGetValue<JToken>("volume-label", out jToken))
                    {
                        jArray = jToken as JArray;

                        if (jArray == null)
                        {
                            context.WriteErrorLine("Expected [mount.volume-label] to be an array of [LABEL=VALUE] strings.");
                        }
                        else
                        {
                            foreach (var label in jArray)
                            {
                                mount.VolumeLabel.Add(label.Value<string>());
                            }
                        }
                    }

                    // Parse [volume-nocopy]

                    if (jObject.TryGetValue<string>("volume-nocopy", out value))
                    {
                        mount.VolumeNoCopy = context.ParseBool(value, $"Invalid [mount.volume-nocopy={value}] value.");
                    }

                    // Parse [volume-opt]

                    if (jObject.TryGetValue<JToken>("volume-opt", out jToken))
                    {
                        jArray = jToken as JArray;

                        if (jArray == null)
                        {
                            context.WriteErrorLine("Expected [mount.volume-opt] to be an array of [OPTION=VALUE] strings.");
                        }
                        else
                        {
                            foreach (var opt in jArray)
                            {
                                mount.VolumeOpt.Add(opt.Value<string>());
                            }
                        }
                    }
                }
                else
                {
                    if (jObject.ContainsKey("volume-driver") ||
                        jObject.ContainsKey("volume-label") ||
                        jObject.ContainsKey("volume-nocopy") ||
                        jObject.ContainsKey("volume-opt"))
                    {
                        context.WriteErrorLine($"[mount.volume-*] options are not allowed for [mount.type={mount.Type}].");
                    }
                }

                // Parse [tmpfs] related options.

                if (mount.Type == MountType.Tmpfs)
                {
                    // Parse [tmpfs-size]

                    if (jObject.TryGetValue<string>("tmpfs-size", out value))
                    {
                        mount.TmpfsSize = context.ParseLong(value, $"Invalid [mount.tmpfs-size={value}] value.");
                    }

                    if (mount.TmpfsSize == 0)
                    {
                        mount.TmpfsSize = null; // Treat this as unlimited
                    }
                    else if (mount.TmpfsSize < 0)
                    {
                        mount.TmpfsSize = null;
                        context.WriteErrorLine($"Invalid [mount.tmpfs-size={value}] because negative sizes are not allowed.");
                    }

                    // Parse [tmpfs-mode]: We're going to allow 3 or 4 octal digits.

                    if (jObject.TryGetValue<string>("tmpfs-mode", out value))
                    {
                        mount.TmpfsMode = ParseFileMode(context, value, $"[mount.tmpfs-mode={value}] is not a valid Linux file mode.");
                    }
                }
                else
                {
                    if (jObject.ContainsKey("tmpfs-size") ||
                        jObject.ContainsKey("tmpfs-mode"))
                    {
                        context.WriteErrorLine($"[mount.tmpfs-*] options are not allowed for [mount.type={mount.Type}].");
                    }
                }

                // Add the mount to the list.

                mounts.Add(mount);
            }

            return mounts;
        }

        /// <summary>
        /// Attempts to parse a Linux-style file mode string (3 or 4 octets).
        /// </summary>
        /// <param name="context">The module context.</param>
        /// <param name="input">The input string.</param>
        /// <param name="errorMessage">The optional context error message to log when the input is not valid.</param>
        /// <returns>The parsed value or <c>null</c> if the input was invalid.</returns>
        private string ParseFileMode(ModuleContext context, string input, string errorMessage = null)
        {
            var error = false;

            if (input == null || input.Length != 3 || input.Length != 4)
            {
                error = true;
            }
            else
            {
                foreach (var ch in input)
                {
                    if (ch < '0' || '7' < ch)
                    {
                        error = true;
                        break;
                    }
                }
            }

            if (error)
            {
                if (errorMessage != null)
                {
                    context.WriteErrorLine(errorMessage);
                }

                return null;
            }
            else
            {
                return input;
            }
        }

        /// <summary>
        /// Parses the service's published ports.
        /// </summary>
        /// <param name="context">The module context.</param>
        /// <param name="argName">The module argument name.</param>
        /// /// <returns>The list of <see cref="PublishPort"/> instances.</returns>
        private List<PublishPort> ParsePublishPorts(ModuleContext context, string argName)
        {
            var publishedPorts = new List<PublishPort>();

            if (!context.Arguments.TryGetValue(argName, out var jToken))
            {
                return publishedPorts;
            }

            var jArray = jToken as JArray;

            if (jArray == null)
            {
                context.WriteErrorLine($"Expected [{argName}] to be an array of published port specifications.");
                return publishedPorts;
            }

            foreach (var item in jArray)
            {
                var jObject = item as JObject;

                if (jObject != null)
                {
                    context.WriteErrorLine($"One or more of the [{argName}] array elements is not a valid published port specification.");
                    return publishedPorts;
                }

                var port  = new PublishPort();
                var value = String.Empty;

                // Parse [published]

                if (jObject.TryGetValue<string>("published", out value))
                {
                    if (!int.TryParse(value, out var publishedPort) || publishedPort<= 0 && ushort.MaxValue < publishedPort)
                    {
                        context.WriteErrorLine($"[{argName}[].published={value}] is not a valid port number.");
                        return publishedPorts;
                    }

                    port.Published = publishedPort;
                }
                else
                {
                    context.WriteErrorLine($"[{argName}] array element lacks the required [published] property.");
                    return publishedPorts;
                }

                // Parse [target]

                if (jObject.TryGetValue<string>("target", out value))
                {
                    if (!int.TryParse(value, out var targetPort) || targetPort <= 0 && ushort.MaxValue < targetPort)
                    {
                        context.WriteErrorLine($"[{argName}[].target={value}] is not a valid port number.");
                        return publishedPorts;
                    }

                    port.Target = targetPort;
                }
                else
                {
                    context.WriteErrorLine($"[{argName}] array element lacks the required [target] property.");
                    return publishedPorts;
                }

                // Parse [mode]

                if (jObject.TryGetValue<string>("mode", out value))
                {
                    if (Enum.TryParse<PortMode>(value, true, out var portMode))
                    {
                        port.Mode = portMode;
                    }
                    else
                    {
                        context.WriteErrorLine($"[{argName}[].mode={value}] is invalid.");
                        return publishedPorts;
                    }
                }
                else
                {
                    port.Mode = PortMode.Ingress;
                }

                // Parse [protocol]

                if (jObject.TryGetValue<string>("protocol", out value))
                {
                    if (Enum.TryParse<PortProtocol>(value, true, out var portProtocol))
                    {
                        port.Protocol = portProtocol;
                    }
                    else
                    {
                        context.WriteErrorLine($"One of the [{argName}] elements specifies the invalid [protocol={value}].");
                        return publishedPorts;
                    }
                }
                else
                {
                    port.Protocol = PortProtocol.Tcp;
                }

                // Add the mount to the list.

                publishedPorts.Add(port);
            }

            return publishedPorts;
        }

        /// <summary>
        /// Parses the service's secrets.
        /// </summary>
        /// <param name="context">The module context.</param>
        /// <param name="argName">The module argument name.</param>
        /// /// <returns>The list of <see cref="Secret"/> instances.</returns>
        private List<Secret> ParseSecretArray(ModuleContext context, string argName)
        {
            var secrets = new List<Secret>();

            if (!context.Arguments.TryGetValue(argName, out var jToken))
            {
                return secrets;
            }

            var jArray = jToken as JArray;

            if (jArray == null)
            {
                context.WriteErrorLine($"Expected [{argName}] to be an array of secret specifications.");
                return secrets;
            }

            foreach (var item in jArray)
            {
                var jObject = item as JObject;

                if (jObject != null)
                {
                    context.WriteErrorLine($"One or more of the [{argName}] array elements is not a valid secret specification.");
                    return secrets;
                }

                var secret = new Secret();
                var value  = String.Empty; 

                // Parse [source]

                if (jObject.TryGetValue<string>("source", out value))
                {
                    secret.Source = value;
                }
                else
                {
                    context.WriteErrorLine($"A [{argName}] array element lacks the required [source] property.");
                    return secrets;
                }

                // Parse [target]

                if (jObject.TryGetValue<string>("target", out value))
                {
                    secret.Target = value;
                }
                else
                {
                    context.WriteErrorLine($"A [{argName}] array element lacks the required [target] property.");
                    return secrets;
                }

                // Parse [mode]: We're going to allow 3 or 4 octets.

                if (jObject.TryGetValue<string>("mode", out value))
                {
                    secret.Mode = ParseFileMode(context, value, $"[{argName}.mode={value}] is not a valid Linux file mode.");
                }

                // Parse [uid]

                if (jObject.TryGetValue<string>("uid", out value))
                {
                    if (!int.TryParse(value, out var parsed))
                    {
                        secret.Uid = value;
                    }
                    else
                    {
                        context.WriteErrorLine($"[{argName}.uid={value}] property is not a valid user ID.");
                    }
                }

                // Parse [gid]

                if (jObject.TryGetValue<string>("gid", out value))
                {
                    if (!int.TryParse(value, out var parsed))
                    {
                        secret.Gid = value;
                    }
                    else
                    {
                        context.WriteErrorLine($"[{argName}.gid={value}] property is not a valid group ID.");
                    }
                }

                // Add the secret to the list.

                secrets.Add(secret);
            }

            return secrets;
        }

        /// <summary>
        /// Starts a Docker service from a definition.
        /// </summary>
        /// <param name="manager">Manager where the command will be executed.</param>
        /// <param name="context">The Ansible module context.</param>
        /// <param name="service">The Service definition.</param>
        private void CreateService(SshProxy<NodeDefinition> manager, ModuleContext context, DockerService service)
        {
            var args = new List<object>();

            foreach (var config in service.Config)
            {
                args.Add($"--config={config}");
            }

            foreach (var constraint in service.Constraint)
            {
                args.Add($"--constraint={constraint}");
            }

            foreach (var label in service.ContainerLabel)
            {
                args.Add($"--container-label={label}");
            }

            foreach (var credential in service.CredentialSpec)
            {
                args.Add($"--credential-spec={credential}");
            }

            if (service.Detach.HasValue)
            {
                args.Add($"--detach={service.Detach.ToString().ToLowerInvariant()}");
            }

            foreach (var nameserver in service.Dns)
            {
                args.Add($"--dns={nameserver}");
            }

            foreach (var option in service.DnsOption)
            {
                args.Add($"--dns-option={option}");
            }

            foreach (var domain in service.DnsSearch)
            {
                args.Add($"--dns-search={domain}");
            }

            if (service.EndpointMode != EndpointMode.Vip)
            {
                args.Add($"--endpoint-mode={service.EndpointMode}");
            }

            if (service.Entrypoint.Count > 0)
            {
                var sb = new StringBuilder();

                foreach (var item in service.Entrypoint)
                {
                    sb.AppendWithSeparator(item);
                }

                args.Add($"--entrypoint={sb}");
            }

            foreach (var env in service.Env)
            {
                args.Add($"--env={env}");
            }

            foreach (var envFile in service.EnvFile)
            {
                args.Add($"--env-file={envFile}");
            }

            foreach (var resource in service.GenericResource)
            {
                args.Add($"--generic-resource={resource}");
            }

            if (service.HealthCmd.Count > 0)
            {
                var sb = new StringBuilder();

                foreach (var item in service.HealthCmd)
                {
                    sb.AppendWithSeparator(item);
                }

                args.Add($"--health-cmd={sb}");
            }

            if (service.HealthInterval.HasValue)
            {
                args.Add($"--health-interval={service.HealthInterval}");
            }

            if (service.HealthRetries.HasValue)
            {
                args.Add($"--health-retries={service.HealthRetries}");
            }

            if (service.HealthStartPeriod.HasValue)
            {
                args.Add($"--health-start-period={service.HealthStartPeriod}");
            }

            if (service.HealthTimeout.HasValue)
            {
                args.Add($"--health-timeout={service.HealthTimeout}");
            }

            foreach (var host in service.Host)
            {
                args.Add($"--host={host}");
            }

            if (!string.IsNullOrEmpty(service.Hostname))
            {
                args.Add($"--hostname={service.Hostname}");
            }

            if (service.Isolation.HasValue)
            {
                args.Add($"--isolation={service.Isolation}");
            }

            foreach (var label in service.Label)
            {
                args.Add($"--label={label}");
            }

            if (service.LimitCpu.HasValue)
            {
                args.Add($"--limit-cpu={service.LimitCpu}");
            }

            if (service.LimitMemory.HasValue)
            {
                args.Add($"--limit-memory={service.LimitMemory}");
            }

            if (service.LogDriver != null)
            {
                args.Add($"--log-driver={service.LogDriver}");
            }

            if (service.LogOpt.Count > 0)
            {
                var sb = new StringBuilder();

                foreach (var option in service.LogOpt)
                {
                    sb.AppendWithSeparator(option, ",");
                }

                args.Add($"--log-opt={sb}");
            }

            if (service.Mode.HasValue)
            {
                args.Add($"--mode={service.Mode}");
            }

            foreach (var mount in service.Mount)
            {
                var sb = new StringBuilder();

                sb.AppendWithSeparator($"type={mount.Type}", ",");

                if (!string.IsNullOrEmpty(mount.Source))
                {
                    sb.AppendWithSeparator($"source={mount.Source}", ",");
                }

                if (!string.IsNullOrEmpty(mount.Target))
                {
                    sb.AppendWithSeparator($"target={mount.Target}", ",");
                }

                if (mount.ReadOnly.HasValue)
                {
                    sb.AppendWithSeparator($"readonly={mount.ReadOnly.Value.ToString().ToLowerInvariant()}", ",");
                }

                if (mount.Consistency.HasValue)
                {
                    sb.AppendWithSeparator($"consistency={mount.Consistency.Value}", ",");
                }

                if (mount.BindPropagation.HasValue)
                {
                    sb.AppendWithSeparator($"bind-propagation={mount.BindPropagation.Value}", ",");
                }

                if (!string.IsNullOrEmpty(mount.VolumeDriver))
                {
                    sb.AppendWithSeparator($"volume-driver={mount.VolumeDriver}", ",");
                }

                if (mount.VolumeNoCopy.HasValue)
                {
                    sb.AppendWithSeparator($"volume-nocopy={mount.VolumeNoCopy.Value.ToString().ToLowerInvariant()}", ",");
                }

                if (mount.VolumeOpt.Count > 0)
                {
                    foreach (var option in mount.VolumeOpt)
                    {
                        sb.AppendWithSeparator($"volume-opt={option}", ",");
                    }
                }

                if (mount.TmpfsSize.HasValue)
                {
                    sb.AppendWithSeparator($"tmpfs-size={mount.TmpfsSize.Value}", ",");
                }

                if (!string.IsNullOrEmpty(mount.TmpfsMode))
                {
                    sb.AppendWithSeparator($"tmpfs-size={mount.TmpfsMode}", ",");
                }

                args.Add($"--mount={sb}");
            }

            args.Add($"--name={service.Name}");

            foreach (var network in service.Network)
            {
                args.Add($"network={network}");
            }

            if (service.NoHealthCheck.HasValue)
            {
                args.Add($"--no-healthcheck={service.NoHealthCheck.Value.ToString().ToLowerInvariant()}");
            }

            if (service.NoResolveImage.HasValue)
            {
                args.Add($"--no-resolveimage={service.NoResolveImage.Value.ToString().ToLowerInvariant()}");
            }

            foreach (var preference in service.PlacementPref)
            {
                args.Add($"--placement-pref={preference}");
            }

            foreach (var port in service.Publish)
            {
                var sb = new StringBuilder();

                sb.AppendWithSeparator($"published={port.Published}", ",");
                sb.AppendWithSeparator($"target={port.Target}", ",");
                sb.AppendWithSeparator($"protocol={port.Protocol}", ",");
                sb.AppendWithSeparator($"mode={port.Mode}", ",");

                args.Add($"--publish={sb}");
            }

            args.Add("--quiet");    // Always suppress progress.

            if (service.ReadOnly.HasValue)
            {
                args.Add($"--readonly={service.ReadOnly.Value.ToString().ToLowerInvariant()}");
            }

            if (service.Replicas.HasValue)
            {
                args.Add($"--replicas={service.Replicas.Value}");
            }

            if (service.ReserveCpu.HasValue)
            {
                args.Add($"--reserve-cpu={service.ReserveCpu.Value}");
            }

            if (service.ReserveMemory.HasValue)
            {
                args.Add($"--reserve-memory={service.ReserveMemory.Value}");
            }

            if (service.RestartCondition.HasValue)
            {
                args.Add($"--restart-condition={service.RestartCondition.Value}");
            }

            if (service.RestartDelay.HasValue)
            {
                args.Add($"--restart-delay={service.RestartDelay.Value}");
            }

            if (service.RestartMaxAttempts.HasValue)
            {
                args.Add($"--restart-max-attempts={service.RestartMaxAttempts.Value}");
            }

            if (service.RestartWindow.HasValue)
            {
                args.Add($"--restart-window={service.RestartWindow.Value}");
            }

            if (service.RollbackDelay.HasValue)
            {
                args.Add($"--rollback-delay={service.RollbackDelay.Value}");
            }

            if (service.RollbackFailureAction.HasValue)
            {
                args.Add($"--rollback-failure-action={service.RollbackFailureAction.Value}");
            }

            if (service.RollbackMaxFailureRatio.HasValue)
            {
                args.Add($"--rollback-max-failure-ratio={service.RollbackMaxFailureRatio.Value}");
            }

            if (service.RollbackMonitor.HasValue)
            {
                args.Add($"--rollback-monitor={service.RollbackMonitor.Value}");
            }

            if (service.RollbackOrder.HasValue)
            {
                args.Add($"--rollback-order={service.RollbackOrder.Value}");
            }

            if (service.RollbackParallism.HasValue)
            {
                args.Add($"--rollback-parallelism={service.RollbackParallism.Value}");
            }

            foreach (var secret in service.Secret)
            {
                var sb = new StringBuilder();

                sb.AppendWithSeparator($"source={secret.Source}", ",");
                sb.AppendWithSeparator($"target={secret.Target}", ",");

                if (secret.Uid != null)
                {
                    sb.AppendWithSeparator($"uid={secret.Uid}", ",");
                }

                if (secret.Gid != null)
                {
                    sb.AppendWithSeparator($"uid={secret.Gid}", ",");
                }

                if (secret.Mode != null)
                {
                    sb.AppendWithSeparator($"mode={secret.Mode}", ",");
                }
            }

            if (service.StopGracePeriod.HasValue)
            {
                args.Add($"--stop-grace-period={service.StopGracePeriod.Value}");
            }

            if (!string.IsNullOrEmpty(service.StopSignal))
            {
                args.Add($"--stop-signal={service.StopSignal}");
            }

            if (service.Tty.HasValue)
            {
                args.Add($"--tty={service.Tty.Value}");
            }

            if (service.UpdateDelay.HasValue)
            {
                args.Add($"--update-delay={service.UpdateDelay.Value}");
            }

            if (service.UpdateFailureAction.HasValue)
            {
                args.Add($"--update-failure-action={service.UpdateFailureAction.Value}");
            }

            if (service.UpdateMaxFailureRatio.HasValue)
            {
                args.Add($"--update-max-failure-ratio={service.UpdateMaxFailureRatio.Value}");
            }

            if (service.UpdateMonitor.HasValue)
            {
                args.Add($"--update-monitor={service.UpdateMonitor.Value}");
            }

            if (service.UpdateOrder.HasValue)
            {
                args.Add($"--update-order={service.UpdateOrder.Value}");
            }

            if (service.UpdateParallism.HasValue)
            {
                args.Add($"--update-parallelism={service.UpdateParallism.Value}");
            }

            if (!string.IsNullOrEmpty(service.User))
            {
                args.Add($"--user={service.User}");
            }

            if (service.WithRegistryAuth.HasValue)
            {
                args.Add($"--with-registry-auth={service.WithRegistryAuth.Value}");
            }

            if (!string.IsNullOrEmpty(service.WorkDir))
            {
                args.Add($"--workdir={service.WorkDir}");
            }

            // The Docker image any service arguments are passed as regular
            // arguments, not command line options.

            args.Add(service.Image);

            foreach (var arg in service.Args)
            {
                args.Add(arg);
            }

            // Create the service.

            var response = manager.DockerCommand(RunOptions.None, "docker service create", args.ToArray());


        }

        /// <summary>
        /// Updates an existing Docker service from a definition.
        /// </summary>
        /// <param name="manager">Manager where the command will be executed.</param>
        /// <param name="context">The Ansible module context.</param>
        /// <param name="service">The Service definition.</param>
        /// <param name="serviceState">The service state from a <b>docker service inspect</b> command formatted as JSON.</param>
        private void UpdateService(SshProxy<NodeDefinition> manager, ModuleContext context, DockerService service, string serviceState)
        {
        }
    }
}
