//-----------------------------------------------------------------------------
// FILE:	    AnsibleCommand.DockerServiceSpec.cs
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
    // NOTE: The types below are accurate as of Docker API version 1.35.

    /// <summary>
    /// Specifies a Docker service.
    /// </summary>
    public class DockerServiceSpec
    {
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

    public enum EndpointMode
    {
        [EnumMember(Value = "vip")]
        Vip = 0,

        [EnumMember(Value = "dnsrr")]
        DnsRR
    }

    public enum IsolationMode
    {
        [EnumMember(Value = "default")]
        Default = 0,

        [EnumMember(Value = "process")]
        Process,

        [EnumMember(Value = "hyperv")]
        HyperV
    }

    public enum ServiceMode
    {
        [EnumMember(Value = "replicated")]
        Replicated = 0,

        [EnumMember(Value = "global")]
        Global
    }

    public enum RestartCondition
    {
        [EnumMember(Value = "any")]
        Any = 0,

        [EnumMember(Value = "none")]
        None,

        [EnumMember(Value = "on-failure")]
        OnFailure
    }

    public enum UpdateOrder
    {
        [EnumMember(Value = "stop-first")]
        StopFirst = 0,

        [EnumMember(Value = "start-first")]
        StartFirst
    }

    public enum RollbackOrder
    {
        [EnumMember(Value = "stop-first")]
        StopFirst = 0,

        [EnumMember(Value = "start-first")]
        StartFirst
    }

    public enum UpdateFailureAction
    {
        [EnumMember(Value = "pause")]
        Pause = 0,

        [EnumMember(Value = "continue")]
        Continue,

        [EnumMember(Value = "rollback")]
        Rollback
    }

    public enum RollbackFailureAction
    {
        [EnumMember(Value = "pause")]
        Pause = 0,

        [EnumMember(Value = "continue")]
        Continue,
    }

    public enum PortMode
    {
        [EnumMember(Value = "ingress")]
        Ingress = 0,

        [EnumMember(Value = "host")]
        Host
    }

    public enum PortProtocol
    {
        [EnumMember(Value = "tcp")]
        Tcp = 0,

        [EnumMember(Value = "udp")]
        Udp,

        [EnumMember(Value = "sctp")]
        Sctp
    }

    public class PublishPort
    {
        public int Published { get; set;}

        public int Target { get; set; }

        public PortMode Mode { get; set; }

        public PortProtocol Protocol { get; set; }
    }

    public enum MountType
    {
        [EnumMember(Value = "volume")]
        Volume = 0,

        [EnumMember(Value = "bind")]
        Bind,

        [EnumMember(Value = "tmpfs")]
        Tmpfs
    }

    public enum MountConsistency
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

    public enum MountBindPropagation
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

    public class Mount
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
}
