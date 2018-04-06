//-----------------------------------------------------------------------------
// FILE:	    AnsibleCommand.DockerServiceSpec.cs
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
using Neon.IO;
using Neon.Net;

namespace NeonCli.Ansible.DockerService
{
    // NOTE: The types below are accurate as of Docker API version 1.35.

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
        public int Published { get; set; }

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

    /// <summary>
    /// Specifies a Docker service.
    /// </summary>
    public class DockerServiceSpec
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Creates a <see cref="DockerServiceSpec"/> by parsing the JSON responses from a
        /// <b>docker service inspect SERVICE</b> for a service as well as the table output
        /// from a <b>docker network ls --no-trunc</b> command listing the current networks.
        /// </summary>
        /// <param name="context">The Annsible module context.</param>
        /// <param name="inspectJson"><b>docker service inspect SERVICE</b> command output for the service.</param>
        /// <param name="networksText"><b>docker network ls --no-trunc</b> command output.</param>
        /// <returns>The parsed <see cref="DockerServiceSpec"/>.</returns>
        public static DockerServiceSpec FromDockerInspect(ModuleContext context, string inspectJson, string networksText)
        {
            var service = new DockerServiceSpec();

            service.Parse(context, inspectJson, networksText);

            return service;
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

        /// <summary>
        /// Parsing the JSON responses from a <b>docker service inspect SERVICE</b> for a 
        /// service as well as the table output from a <b>docker network ls --no-trunc</b> 
        /// command listing the current networks.
        /// </summary>
        /// <param name="context">The Annsible module context.</param>
        /// <param name="inspectJson"><b>docker service inspect SERVICE</b> command output for the service.</param>
        /// <param name="networksText"><b>docker network ls --no-trunc</b> command output.</param>
        /// <returns>The parsed <see cref="DockerServiceSpec"/>.</returns>
        /// <exception cref="Exception">Various exceptions are thrown for errors.</exception>
        private void Parse(ModuleContext context, string inspectJson, string networksText)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(inspectJson));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(networksText));

            // See the link below for more information on the REST API JSON format:
            //
            //      https://docs.docker.com/engine/api/v1.35/#operation/ServiceInspect
            //
            // The parsing code below basically follows the order of the properties
            // as defined by the REST specification.

            // We're expecting the inspection JSON to be a single item
            // array holding the service information.

            var jArray = JArray.Parse(inspectJson);

            if (jArray.Count != 1)
            {
                throw new ArgumentException("Invalid service inspection: expected a single element array.");
            }

            // Parse the network definitions so we can map network UUIDs to network names.
            // We're expecting the [docker network ls --no-trunc] output to look like:
            //
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

            // Extract the service properties from the service JSON.

            var spec         = (JObject)jArray[0]["Spec"];
            var taskTemplate = (JObject)spec["TaskTemplate"];

            // Service Labels

            var labels = (JObject)spec.GetValue("Labels");

            foreach (var item in labels)
            {
                this.Label.Add($"{item.Key}={item.Value}");
            }

            // ContainerSpec

            var containerSpec = (JObject)taskTemplate["ContainerSpec"];

            this.Image = (string)containerSpec["Image"];

            foreach (var item in GetObjectProperty(containerSpec, "Labels"))
            {
                this.ContainerLabel.Add($"{item.Key}={item.Value}");
            }

            foreach (string arg in GetArrayProperty(containerSpec, "Command"))
            {
                this.Entrypoint.Add(arg);
            }

            foreach (string arg in GetArrayProperty(containerSpec, "Args"))
            {
                this.Args.Add(arg);
            }

            this.Hostname = GetStringProperty(containerSpec, "Hostname");

            foreach (string env in GetArrayProperty(containerSpec, "Env"))
            {
                this.Env.Add(env);
            }

            this.WorkDir = GetStringProperty(containerSpec, "Dir");
            this.User    = GetStringProperty(containerSpec, "User");

            foreach (string group in GetArrayProperty(containerSpec, "Groups"))
            {
                this.Group.Add(group);
            }

            this.Tty      = GetBoolProperty(containerSpec, "TTY");
            this.ReadOnly = GetBoolProperty(containerSpec, "ReadOnly");

            foreach (JObject item in GetArrayProperty(containerSpec, "Mounts"))
            {
                var mount = new Mount();

                mount.Target          = GetStringProperty(item, "Target");
                mount.Source          = GetStringProperty(item, "Source");
                mount.Type            = GetEnumProperty<MountType>(item, "Type").Value;
                mount.ReadOnly        = GetBoolProperty(item, "ReadOnbly");
                mount.Consistency     = GetEnumProperty<MountConsistency>(item, "Consistency").Value;

                var bindOptions = GetObjectProperty(item, "BindOptions");

                mount.BindPropagation = GetEnumProperty<MountBindPropagation>(bindOptions, "Propagation");

                var volumeOptions = GetObjectProperty(item, "VolumeOptions");

                mount.VolumeNoCopy = GetBoolProperty(volumeOptions, "NoCopy");

                foreach (var label in GetObjectProperty(volumeOptions, "Labels"))
                {
                    mount.VolumeLabel.Add($"{label.Key}={label.Value}");
                }

                var driverConfig = GetObjectProperty(item, "DriverConfig");

                mount.VolumeDriver = GetStringProperty(driverConfig, "Name");

                var sb = new StringBuilder();

                foreach (var option in GetObjectProperty(volumeOptions, "Options"))
                {
                    sb.AppendWithSeparator($"{option.Key}={option.Value}", ",");
                }

                var tmpfsOptions = GetObjectProperty(item, "TempfsOptions");

                mount.TmpfsSize = GetLongProperty(tmpfsOptions, "SizeBytes");
                mount.TmpfsMode = GetModeProperty(tmpfsOptions, "Mode");

                this.Mount.Add(mount);
            }

            this.StopSignal      = GetStringProperty(containerSpec, "StopSignal");
            this.StopGracePeriod = GetLongProperty(containerSpec, "StopGracePeriod");

            var healthCheck = GetObjectProperty(containerSpec, "HealthCheck");

            foreach (string arg in GetArrayProperty(healthCheck, "Test"))
            {
                this.HealthCmd.Add(arg);
            }

            this.HealthInterval    = GetLongProperty(healthCheck, "Interval");
            this.HealthTimeout     = GetLongProperty(healthCheck, "Timeout");
            this.HealthRetries     = GetIntProperty(healthCheck, "Retries");
            this.HealthStartPeriod = GetLongProperty(healthCheck, "StartPeriod");

            foreach (string host in GetArrayProperty(containerSpec, "Hosts"))
            {
                // $note: The REST API allows additional aliases to be specified after
                //        the first host name.  We're going to ignore these because 
                //        there's no way to specify these on the command line which
                //        specifies these as:
                //
                //              HOST:IP

                var fields = host.Split(',', StringSplitOptions.RemoveEmptyEntries);

                this.Host.Add($"{fields[0]}:{fields[1]}");
            }

            var dnsConfig = GetObjectProperty(containerSpec, "DNSConfig");

            foreach (string nameserver in GetArrayProperty(dnsConfig, "Nameservers"))
            {
                this.Dns.Add(IPAddress.Parse(nameserver));
            }

            foreach (string domain in GetArrayProperty(dnsConfig, "Search"))
            {
                this.DnsSearch.Add(domain);
            }

            foreach (string option in GetArrayProperty(dnsConfig, "Options"))
            {
                // $todo(jeff.lill):
                //
                // I'm guessing here that the service inspect JSON uses ':'
                // instead of '=' like the command line.  I'm going to 
                // convert any colons.

                this.DnsOption.Add(option.Replace(':', '='));
            }

            foreach (JObject secretSpec in GetArrayProperty(containerSpec, "secrets"))
            {
                var secret = new Secret();

                secret.Source = GetStringProperty(secretSpec, "SecretName");

                var secretFile = GetObjectProperty(secretSpec, "File");

                secret.Target = GetStringProperty(secretFile, "Name");
                secret.Uid    = GetStringProperty(secretFile, "UID");
                secret.Gid    = GetStringProperty(secretFile, "GID");
                secret.Mode   = GetModeProperty(secretFile, "Mode");

                this.Secret.Add(secret);
            }

            foreach (JObject configSpec in GetArrayProperty(containerSpec, "Configs"))
            {
                // $todo(jeff.lill):
                //
                // It appears that the REST API has more options than are supported
                // by the command line (user, group, mode,...).  We're going to 
                // ignore these.

                this.Config.Add(GetStringProperty(configSpec, "ConfigName"));
            }

            this.Isolation = GetEnumProperty<IsolationMode>(containerSpec, "Isolation");

            // Resources

            // $todo(jeff.lill):
            //
            // I'm ignoring the [Limits.GenerticResources] and [Reservation.GenerticResources]
            // right now because I suprised that there are two of these.  The command line
            // looks like it only supports one global combined [GenericResources] concept.

            var resources   = GetObjectProperty(taskTemplate, "Resources");
            var limits      = GetObjectProperty(resources, "Limits");
            var reservation = GetObjectProperty(resources, "Reservation");

            var nanoCpus = GetLongProperty(limits, "NanoCPUs");

            if (nanoCpus.HasValue)
            {
                this.LimitCpu = nanoCpus / 1000000000;
            }

            this.LimitMemory = GetLongProperty(limits, "MemoryBytes");

            nanoCpus = GetLongProperty(reservation, "NanoCPUs");

            if (nanoCpus.HasValue)
            {
                this.ReserveCpu = nanoCpus / 1000000000;
            }

            this.ReserveMemory = GetLongProperty(reservation, "MemoryBytes");

            // RestartPolicy

            var restartPolicy = GetObjectProperty(taskTemplate, "RestartPolicy");

            this.RestartCondition   = GetEnumProperty<RestartCondition>(restartPolicy, "Condition");
            this.RestartDelay       = GetLongProperty(restartPolicy, "Delay");
            this.RestartMaxAttempts = GetIntProperty(restartPolicy, "MaxAttempts");
            this.RestartWindow      = GetLongProperty(restartPolicy, "Windows");

            // Placement

            // $todo(jeff.lill):
            //
            // We're going to ignore the [Preferences] and [Platforms] fields for now.

            var placement = GetObjectProperty(taskTemplate, "Placement");

            foreach (string constraint in GetArrayProperty(placement, "Constraints"))
            {
                this.Constraint.Add(constraint);
            }

            // $todo(jeff.lill):
            //
            // Networks are referenced by UUID, not name.  We'll use the network
            // map passed to the method to try to associate the network names.
            //
            // Note that it's possible (but unlikely) for the set of cluster networks
            // to have changed between listing them and inspecting the service, so
            // we might not be able to map a network ID to a name.
            //
            // In this case, we won't add the referenced network to this service
            // specification.  The ultimate effect will be to potentially trigger 
            // an uncessary service update, but since this will be super rare,
            // I'm not going to worry about it.

            foreach (JObject network in GetArrayProperty(taskTemplate, "Networks"))
            {
            }
        }

        //---------------------------------------------------------------------
        // JSON helpers:

        /// <summary>
        /// Looks up an object property, returning <c>null</c> if the
        /// property doesn't exist.
        /// </summary>
        /// <param name="jObject">The parent object or <c>null</c>.</param>
        /// <param name="name">The property name.</param>
        /// <returns>
        /// The property token or <c>null</c> if the property doesn't exist or
        /// if <see cref="jObject"/> is <c>null</c>.
        /// </returns>
        private static JToken GetProperty(JObject jObject, string name)
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
            var jToken = GetProperty(jObject, name);

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
            var jToken = GetProperty(jObject, name);

            if (jToken != null)
            {
                return (bool)jToken;
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
            var jToken = GetProperty(jObject, name);

            if (jToken != null)
            {
                return (int)jToken;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Looks up an <c>long</c> property, returning <c>null</c> if the
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
            var jToken = GetProperty(jObject, name);

            if (jToken != null)
            {
                return (long)jToken;
            }
            else
            {
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
        private static string GetModeProperty(JObject jObject, string name)
        {
            var jToken = GetProperty(jObject, name);

            if (jToken != null)
            {
                return Convert.ToString((int)jToken, 8);
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
            var jToken = GetProperty(jObject, name);

            if (jToken != null)
            {
                return (JObject)jToken;
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
            var jToken = GetProperty(jObject, name);

            if (jToken != null)
            {
                return (JArray)jToken;
            }
            else
            {
                return new JArray();
            }
        }
    }
}
