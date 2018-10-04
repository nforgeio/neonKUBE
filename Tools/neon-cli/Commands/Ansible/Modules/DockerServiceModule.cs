//-----------------------------------------------------------------------------
// FILE:	    DockerServiceModule.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
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

using Neon.Cryptography;
using Neon.Common;
using Neon.Docker;
using Neon.IO;
using Neon.Hive;
using Neon.Net;

using NeonCli.Ansible.Docker;

// $todo(jeff.lill):
//
// Note that this implementation is designed to look more like the Docker
// service create/update commands rather than the Docker REST API.  I think
// this makes sense, since users will tend to have more experience with the
// command line as opposed to the API.
//
// One consequence of this is that we won't be able to take advantage of the
// REST API's built-in CAS (check-and-set) functionality around the version
// returned by [docker service inspect].  I don't believe this is important
// at this point.

// $todo(jeff.lill):
//
// The module doesn't currently support renaming a service although that
// is allowed by Docker.  Perhaps we could add a [new_name] parameter?

namespace NeonCli.Ansible
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
    // state                    no          present     present     indicates whether the service should
    //                                                  absent      be created, removed, or rolled back.
    //                                                  rollback    Note that all properties except for
    //                                                              [name] are ignored for [absent] and
    //                                                              [rollback].
    //
    // force                    no          false                   forces service update when [state=present]
    //                                                              even when there are no changes
    //
    // args                     no                                  array of service arguments
    //
    // config                   no                                  array of configuration names
    //                                                              each config entry looks like:
    //
    //                                                                  source      - secret name (required)
    //                                                                  target      - target file path (required)
    //                                                                  uid         - user ID (optional)
    //                                                                  gid         - group ID (optional)
    //                                                                  mode        - Linux file mode (optional/octal)
    //
    // constraint               no                                  array of placement constraints like
    //                                                              LABEL==VALUE or LABEL!=VALUE
    //
    // container_label          no                                  array of container labels like
    //                                                              LABEL=VALUE
    //
    // credential_spec          no                                  array of Windows credential specifications
    //
    // detach                   no          false       true        specifies whether the service command should
    //                                                  false       exit immediately or wait for the service changes
    //                                                              to converge
    //
    // dns                      no                                  array of DNS nameserver IP addresses
    //
    // dns_option               no                                  array of DNS options.  See:
    //                                                              http://manpages.ubuntu.com/manpages/precise/man5/resolvconf.conf.5.html
    //
    // dns_search               no                                  array of  DNS domains to be searched for 
    //                                                              non-fully qualified hostnames
    //
    // endpoint_mode            no          vip         vip         service endpoint mode
    //                                                  dnsrr
    //
    // entrypoint               no                                  array of strings overriding the image entrypoint
    //                                                              command and arguments
    //
    // env                      no                                  array specifying environment variables to be
    //                                                              passed to the container like VARIABLE=VALUE
    //                                                              or just VARIABLE to import the variable from
    //                                                              the Docker host
    //
    // generic_resource         no                                  array of generic resource requirements for
    //                                                              service placement.
    //
    // group                    no                                  array of service container supplementary user groups 
    //
    // health_cmd               no                                  the service container health check
    //
    // health_interval          no                                  interval between service container health checks
    //
    // health_retries           no                                  number of consecutive health check failures before
    //                                                              a service container is consider unhealthy
    //
    // health_start_period      no                                  delay after service container start before health
    //                                                              checks are enforced
    //
    // health_timeout           no                                  maximum time to wait for a healh check command
    //                                                          
    // host                     no                                  array of hostname to IP address mappings to be
    //                                                              resolved automatically within service containers,
    //                                                              essentially like adding theses to the local 
    //                                                              [/etc/hosts] file.  These are formatted like:
    //
    //                                                                  HOST:IP
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
    // limit_cpu                no                                  specifies how many CPUs the service containers requires.
    //                                                              This can be a floating point number (e.g. 1.5)
    //
    // limit_memory             no                                  specifies the maximum service container RAM as size 
    //                                                              and units (b|k|m|g)
    //
    // log_driver               no                                  specifies the logging driver
    //
    // log_opt                  no                                  specifies the logging options as an array of OPTION=VALUE strings.
    //
    // mode                     no          replicated  replicated  specifies the service mode
    //                                                  global
    //
    // mount                    no                                  array of items specifying container bind mounts like:
    //                                                              
    //                                                                  type: volume                default: volume     (volume|bind|tmpfs)
    //                                                                  source: NAME/PATH
    //                                                                  target: PATH
    //                                                                  readonly: true/false        default: false      (true|false)
    //                                                                  consistency: default        default: default    (default|consistent|cached|delegated)
    //
    //                                                                  options for: type=bind
    //                                                                  ----------------------
    //                                                                  bind_propagation: rprivate  default: rprivate   (shared|slave|private|rshared,
    //                                                                                                                   rslave|rprivate)
    //                                                                  options for: type=volume
    //                                                                  ------------------------
    //                                                                  volume_driver: local        default: local
    //                                                                  volume_label: [ ... ]       array of NAME=VALUE volume label strings
    //                                                                  volume_nocopy: true         default: true       (true|false)
    //                                                                  volume_opt: [ ... ]         array of OPTION=VALUE volume options
    //
    //                                                                  options for: type=tmpfs
    //                                                                  -----------------------
    //                                                                  tmpfs_size: 1000000         default: unlimited
    //                                                                  tmpfs_mode: 1777            default: 1777
    //
    // network                  no                                  array of networks to be attached
    //
    // no_healthcheck           no          false       true        disable service container health checks
    //                                                  false
    //
    // no_resolve_image         no          false       true        disable registry query to resolve image digest 
    //                                                  false       and supported platforms
    //
    // placement_pref           no                                  array of placement preferences
    //
    // publish                  no                                  array of network port publication specifications like:
    //                                      
    //                                                                  published: 8080     (required)
    //                                                                  target: 80          (retured)
    //                                                                  mode: ingress       (optional: ingress|host}
    //                                                                  protocol: tcp       (optional: tcp|udp)
    //
    // read_only                no          false       true        mount container filesystem as read-only
    //                                                  false
    //
    // replicas                 no          1                       number of service tasks
    //
    // reserve_cpu              no                                  CPUs to be reserved for each service container.
    //                                                              This is a floating point number.
    //
    // reserve_memory           no                                  RAM to be reserved for each service container as size 
    //                                                              and units (b|k|m|g)
    //
    // restart_condition        no          any         any         service restart condition
    //                                                  none
    //                                                  on-failure
    //
    // restart_delay            no          5s          any         Delay between service container restart attempts
    //                                                              (ns|us|ms|s|m|h)
    //
    // restart_max_attempts     no          unlimited               maximum number of container restarts to be attempted
    //
    // restart_window           no                                  time window used to evaluate restart policy (ns|us|ms|s|m|h)
    //
    // rollback_delay           no          0s                      delay between task rollbacks (ns|us|ms|s|m|h)
    //
    // rollback_failure_action  no          pause       pause       action to take on service container rollback failure
    //                                                  continue
    //
    // rollback_max_failure_ratio no        0                       failure rate to tolerate during a rollback.
    //
    // rollback_monitor         no          5s                      time to monitor rolled back service containers for
    //                                                              failure (ns|us|ms|s|m|h)
    //
    // rollback_order           no          stop-first  stop-first  service container rollback order (stop-first|start-first)
    //                                                  start-first
    //
    // rollback_parallelism     no          1                       maximum number of service tasks to be rolled back
    //                                                              simultaneously (0 to rollback all at once)
    //
    // secret                   no                                  array of secrets to be be exposed to the service.
    //                                                              each secret entry looks like:
    //
    //                                                                  source      - secret name (required)
    //                                                                  target      - target file path (required)
    //                                                                  uid         - user ID (optional)
    //                                                                  gid         - group ID (optional)
    //                                                                  mode        - Linux file mode (optional/octal)
    //
    // stop_grace_period        no          10s                     maximum time to wait for a service container to 
    //                                                              terminate gracefully (ns|us|ms|s|m|h)
    //
    // stop_signal              no          SIGTERM                 signal to be used to stop service containers
    //
    // tty                      no          false                   allocate a TTY for service containers
    //
    // update_delay             no          0s                      delay between task updates (ns|us|ms|s|m|h)
    //
    // update_failure_action    no          pause       pause       action to take on service container update failure
    //                                                  continue
    //                                                  rollback
    //
    // update_max_failure_ratio no          0                       failure rate to tolerate during an update.
    //
    // update_monitor           no          5s                      time to monitor updated service containers for
    //                                                              failure (ns|us|ms|s|m|h)
    //
    // update_order             no          stop-first  stop-first  service container update order
    //                                                  start-first
    //
    // update_parallelism       no          1                       maximum number of service tasks to be updated
    //                                                              simultaneously (0 to update all at once)
    //
    // user                     no                                  container username/group: <name|uid>[:<group|gid>]
    //
    // with_registry_auth       no          false                   send registry authentication details to Swarm nodes
    //
    // workdir                  no                                  command working directory within containers
    //
    // Check Mode:
    // -----------
    //
    // This module supports the [--check] Ansible command line option and [check_mode] task
    // property by determining whether any changes would have been made and also logging
    // a desciption of the changes when Ansible verbosity is increased.
    //
    // Examples:
    // ---------
    //
    // This example creates a basic do-nothing service:
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: service
    //        neon_docker_service:
    //          name: test
    //          state: present
    //          image: nhive/test:0
    //
    // This example creates or upgrades a service by updating the
    // container image, adding a network and publishing a TCP port:
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: service
    //        neon_docker_service:
    //          name: test
    //          state: present
    //          image: nhive/test:1
    //          network: foo-network
    //          mount:
    //            - type: volume
    //              source: test-volume
    //              target: /mnt/test
    //          publish:
    //            - published: 8080
    //              target: 80
    //              mode: ingress
    //              protocol: tcp
    //
    // This example rolls a service back to its previous state:
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: service
    //        neon_docker_service:
    //          name: test
    //          state: rollback
    //
    // This example removes a service if it's present:
    //
    //  - name: test
    //    hosts: localhost
    //    tasks:
    //      - name: service
    //        neon_docker_service:
    //          name: test
    //          state: absent

    /// <summary>
    /// Implements the <b>neon_docker_service</b> Ansible module.
    /// </summary>
    public class DockerServiceModule : IAnsibleModule
    {
        private HashSet<string> validModuleArgs = new HashSet<string>()
        {
            "name",
            "state",
            "force",
            "args",
            "config",
            "constraint",
            "container_label",
            "credential_spec",
            "detach",
            "dns",
            "dns_option",
            "dns_search",
            "endpoint_mode",
            "entrypoint",
            "env",
            "generic_resource",
            "group",
            "health_cmd",
            "health_interval",
            "health_retries",
            "health_start_period",
            "health_timeout",
            "host",
            "hostname",
            "image",
            "isolation",
            "label",
            "limit_cpu",
            "limit_memory",
            "log_driver",
            "log_opt",
            "mode",
            "mount",
            "network",
            "no_healthcheck",
            "no_resolve_image",
            "placement_pref",
            "publish",
            "read_only",
            "replicas",
            "reserve_cpu",
            "reserve_memory",
            "restart_condition",
            "restart_delay",
            "restart_max_attempts",
            "restart_window",
            "rollback_delay",
            "rollback_failure_action",
            "rollback_max_failure_ratio",
            "rollback_monitor",
            "rollback_order",
            "rollback_parallelism",
            "secret",
            "stop_grace_period",
            "stop_signal",
            "tty",
            "update_delay",
            "update_failure_action",
            "update_max_failure_ratio",
            "update_monitor",
            "update_order",
            "update_parallelism",
            "user",
            "with_registry_auth",
            "workdir"
        };

        private HashSet<string> validConfigArgs = new HashSet<string>()
        {
            "source",
            "target",
            "uid",
            "gid",
            "mode"
        };

        private HashSet<string> validSecretArgs = new HashSet<string>()
        {
            "source",
            "target",
            "uid",
            "gid",
            "mode"
        };

        private HashSet<string> validMountArgs = new HashSet<string>()
        {
            "type",
            "source",
            "target",
            "readonly",
            "consistency",
            "bind_propagation",
            "volume_driver",
            "volume_label",
            "volume_nocopy",
            "volume_opt",
            "tmpfs_size",
            "tmpfs_mode"
        };

        private HashSet<string> validPublishArgs = new HashSet<string>()
        {
            "published",
            "target",
            "mode",
            "protocol"
        };

        /// <inheritdoc/>
        public void Run(ModuleContext context)
        {
            var hive = HiveHelper.Hive;

            if (!context.ValidateArguments(context.Arguments, validModuleArgs))
            {
                context.Failed = true;
                return;
            }

            // Obtain common arguments.

            context.WriteLine(AnsibleVerbosity.Trace, "Parsing common parameters.");

            if (!context.Arguments.TryGetValue<string>("name", out var name))
            {
                throw new ArgumentException($"[name] module argument is required.");
            }

            if (!HiveDefinition.IsValidName(name))
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

            var service = new DockerServiceSpec();

            service.Name                    = name;

            service.Args                    = context.ParseStringArray("args");
            service.Config                  = ParseConfigs(context, "config");
            service.Constraint              = context.ParseStringArray("constraint");
            service.ContainerLabel          = context.ParseStringArray("container_label");
            service.CredentialSpec          = context.ParseStringArray("credential_spec");
            service.Detach                  = context.ParseBool("detach");
            service.Dns                     = context.ParseIPAddressArray("dns");
            service.DnsOption               = context.ParseStringArray("dns_option");
            service.DnsSearch               = context.ParseStringArray("dns_search");
            service.EndpointMode            = context.ParseEnum<ServiceEndpointMode>("endpoint_mode");
            service.Command                 = context.ParseStringArray("entrypoint");
            service.Env                     = context.ParseStringArray("env");
            service.GenericResource         = context.ParseStringArray("generic_resource");
            service.Groups                  = context.ParseStringArray("group");
            service.HealthCmd               = context.ParseString("health_cmd");
            service.HealthInterval          = context.ParseDockerInterval("health_interval");
            service.HealthRetries           = context.ParseLong("health_retries", v => v >= 0);
            service.HealthStartPeriod       = context.ParseDockerInterval("health_start_period");
            service.HealthTimeout           = context.ParseDockerInterval("health_timeout");
            service.Host                    = context.ParseStringArray("host");
            service.Hostname                = context.ParseString("hostname");
            service.Image                   = context.ParseString("image");
            service.Isolation               = context.ParseEnum<ServiceIsolationMode>("isolation", ServiceIsolationMode.Default);
            service.Label                   = context.ParseStringArray("label");
            service.LimitCpu                = context.ParseDouble("limit_cpu", v => v > 0);
            service.LimitMemory             = context.ParseDockerByteSize("limit_memory");
            service.LogDriver               = context.ParseString("log_driver");
            service.LogOpt                  = context.ParseStringArray("log_opt");
            service.Mode                    = context.ParseEnum<ServiceMode>("mode");
            service.Mount                   = ParseMounts(context, "mount");
            service.Network                 = context.ParseStringArray("network");
            service.NoHealthCheck           = context.ParseBool("no_healthcheck");
            service.NoResolveImage          = context.ParseBool("no_resolve_image");
            service.PlacementPref           = context.ParseStringArray("placement_pref");
            service.Publish                 = ParsePublishPorts(context, "publish");
            service.ReadOnly                = context.ParseBool("read_only");
            service.Replicas                = context.ParseLong("replicas", v => v >= 0);
            service.ReserveCpu              = context.ParseDouble("reserve_cpu", v => v > 0);
            service.ReserveMemory           = context.ParseDockerByteSize("reserve_memory");
            service.RestartCondition        = context.ParseEnum<ServiceRestartCondition>("restart_condition", default(ServiceRestartCondition));
            service.RestartDelay            = context.ParseDockerInterval("restart_delay");
            service.RestartMaxAttempts      = context.ParseLong("restart_max_attempts", v => v >= 0);
            service.RestartWindow           = context.ParseDockerInterval("restart_window");
            service.RollbackDelay           = context.ParseDockerInterval("rollback_delay");
            service.RollbackFailureAction   = context.ParseEnum<ServiceRollbackFailureAction>("rollback_failure_action", default(ServiceRollbackFailureAction));
            service.RollbackMaxFailureRatio = context.ParseDouble("rollback_max_failure_ratio", v => v >= 0);
            service.RollbackMonitor         = context.ParseDockerInterval("rollback_monitor");
            service.RollbackOrder           = context.ParseEnum<ServiceRollbackOrder>("rollback_order", default(ServiceRollbackOrder));
            service.RollbackParallism       = context.ParseInt("rollback_parallelism", v => v > 0);
            service.Secret                  = ParseSecrets(context, "secret");
            service.StopGracePeriod         = context.ParseDockerInterval("stop_grace_period");
            service.StopSignal              = context.ParseString("stop_signal");
            service.ReadOnly                = context.ParseBool("read_only");
            service.TTY                     = context.ParseBool("tty");
            service.UpdateDelay             = context.ParseDockerInterval("update_delay");
            service.UpdateFailureAction     = context.ParseEnum<ServiceUpdateFailureAction>("update_failure_action", default(ServiceUpdateFailureAction));
            service.UpdateMaxFailureRatio   = context.ParseDouble("update_max_failure_ratio", v => v >= 0);
            service.UpdateMonitor           = context.ParseDockerInterval("update_monitor");
            service.UpdateOrder             = context.ParseEnum<ServiceUpdateOrder>("update_order", default(ServiceUpdateOrder));
            service.UpdateParallism         = context.ParseInt("update_parallelism", v => v > 0);
            service.User                    = context.ParseString("user");
            service.WithRegistryAuth        = context.ParseBool("with_registry_auth");
            service.Dir                     = context.ParseString("workdir");

            // Abort the operation if any errors were reported during parsing.

            if (context.HasErrors)
            {
                return;
            }

            // We have the required arguments, so perform the operation.
            //
            // Detect whether the service is already running by inspecting it
            // then start it when it's not already running or update if it is.

            context.WriteLine(AnsibleVerbosity.Trace, $"Inspecting [{service.Name}] service.");

            var manager        = hive.GetReachableManager();
            var response       = manager.DockerCommand(RunOptions.None, "docker service inspect", service.Name);
            var serviceDetails = (ServiceDetails)null;

            if (response.ExitCode == 0)
            {
                try
                {
                    // The inspection response is actually an array with a single
                    // service details element, so we'll need to extract that element
                    // and then parse it.

                    var jArray = JArray.Parse(response.OutputText);

                    serviceDetails = NeonHelper.JsonDeserialize<ServiceDetails>(jArray[0].ToString());
                    serviceDetails.Normalize();
                }
                catch
                {
                    context.WriteErrorLine("INTERNAL ERROR: Cannot parse existing service state.");
                    throw;
                }

                context.WriteLine(AnsibleVerbosity.Trace, $"[{service.Name}] service exists.");
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
                    context.WriteLine(AnsibleVerbosity.Trace, $"[{service.Name}] service does not exist.");
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

                    context.WriteLine(AnsibleVerbosity.Trace, $"[state=absent] so removing [{service.Name}] service if it exists.");

                    if (serviceDetails == null)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"No change required because [{service.Name}] service doesn't exist.");
                    }
                    else
                    {
                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"[{service.Name}] service will be removed when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Removing [{service.Name}] service.");

                            response = manager.DockerCommand(RunOptions.None, $"docker service rm {service.Name}");

                            if (response.ExitCode == 0)
                            {
                                context.WriteLine(AnsibleVerbosity.Info, $"[{service.Name}] service was removed.");
                                context.Changed = true;
                            }
                            else
                            {
                                context.WriteErrorLine(response.AllText);
                            }
                        }
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

                    if (serviceDetails == null)
                    {
                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"[{service.Name}] service will be created when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            // NOTE: UpdateService() handles the CHECK-MODE logic and context logging.

                            CreateService(manager, context, service);
                        }
                    }
                    else
                    {
                        // NOTE: UpdateService() handles the CHECK-MODE logic and context logging.

                        UpdateService(manager, context, force, service, serviceDetails);
                    }
                    break;

                case "rollback":

                    if (serviceDetails == null)
                    {
                        context.WriteErrorLine($"[{service.Name}] service is not running and cannot be rolled back.");
                    }
                    else if (serviceDetails.PreviousSpec == null)
                    {
                        context.WriteLine(AnsibleVerbosity.Important, $"[{service.Name}] service cannot be rolled back because it has no previous configuration.");
                    }
                    else
                    {
                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"[{service.Name}] service will be rolled back when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Rolling back [{service.Name}] service will be created when CHECK-MODE is disabled.");

                            response = manager.DockerCommand(RunOptions.None, "docker", "service", "rollback", service.Name);

                            if (response.ExitCode == 0)
                            {
                                context.WriteLine(AnsibleVerbosity.Info, $"[{service.Name}] service was rolled back.");
                                context.Changed = true;
                            }
                            else
                            {
                                context.WriteErrorLine(response.AllText);
                            }
                        }
                    }
                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [present], [absent], or [rollback].");
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

                if (jObject == null)
                {
                    context.WriteErrorLine($"One or more of the [{argName}] array elements is not a valid bind mount specification.");
                    return mounts;
                }

                if (!context.ValidateArguments(jObject, validMountArgs, argName))
                {
                    return mounts;
                }

                var mount = new Mount();
                var value = String.Empty;

                // Parse [type]

                if (jObject.TryGetValue<string>("type", out value))
                {
                    mount.Type = context.ParseEnumValue<ServiceMountType>(value, default(ServiceMountType));
                }
                else
                {
                    mount.Type = default(ServiceMountType);
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
                    mount.ReadOnly = context.ParseBoolValue(value, "Invalid [mount.readonly] value.");
                }

                // Parse [consistency]

                if (jObject.TryGetValue<string>("consistency", out value))
                {
                    mount.Consistency = context.ParseEnumValue<ServiceMountConsistency>(value, default(ServiceMountConsistency), $"Invalid [mount.consistency={value}] value.");
                }

                // Parse [bind] related options.

                if (mount.Type == ServiceMountType.Bind)
                {
                    // Parse [bind_propagation]

                    if (jObject.TryGetValue<string>("bind_propagation", out value))
                    {
                        mount.BindPropagation = context.ParseEnumValue<ServiceMountBindPropagation>(value, default(ServiceMountBindPropagation), $"Invalid [mount.bind_propagation={value}] value.");
                    }
                }
                else
                {
                    if (jObject.ContainsKey("bind_propagation"))
                    {
                        context.WriteErrorLine($"[mount.bind_*] options are not allowed for [mount.type={mount.Type}].");
                    }
                }

                // Parse the [volume] related options.

                if (mount.Type == ServiceMountType.Volume)
                {
                    // Parse [volume_driver]

                    if (jObject.TryGetValue<string>("volume_driver", out value))
                    {
                        mount.VolumeDriver = value;
                    }
                    else
                    {
                        mount.VolumeDriver = "local";
                    }

                    // Parse [volume_label]

                    if (jObject.TryGetValue<JToken>("volume_label", out jToken))
                    {
                        jArray = jToken as JArray;

                        if (jArray == null)
                        {
                            context.WriteErrorLine("Expected [mount.volume_label] to be an array of [LABEL=VALUE] strings.");
                        }
                        else
                        {
                            foreach (var label in jArray)
                            {
                                mount.VolumeLabel.Add(label.Value<string>());
                            }
                        }
                    }

                    // Parse [volume_nocopy]

                    if (jObject.TryGetValue<string>("volume_nocopy", out value))
                    {
                        mount.VolumeNoCopy = context.ParseBoolValue(value, $"Invalid [mount.volume_nocopy={value}] value.");
                    }

                    // Parse [volume_opt]

                    if (jObject.TryGetValue<JToken>("volume_opt", out jToken))
                    {
                        jArray = jToken as JArray;

                        if (jArray == null)
                        {
                            context.WriteErrorLine("Expected [mount.volume_opt] to be an array of [OPTION=VALUE] strings.");
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
                    if (jObject.ContainsKey("volume_driver") ||
                        jObject.ContainsKey("volume_label") ||
                        jObject.ContainsKey("volume_nocopy") ||
                        jObject.ContainsKey("volume_opt"))
                    {
                        context.WriteErrorLine($"[mount.volume_*] options are not allowed for [mount.type={mount.Type}].");
                    }
                }

                // Parse [tmpfs] related options.

                if (mount.Type == ServiceMountType.Tmpfs)
                {
                    // Parse [tmpfs_size].

                    if (jObject.TryGetValue<string>("tmpfs_size", out value))
                    {
                        mount.TmpfsSize = context.ParseDockerByteSizeValue(value, $"[mount.tmpfs_size={value}] is not a valid byte size.");
                    }

                    // Parse [tmpfs_mode]: We're going to allow 3 or 4 octal digits.

                    if (jObject.TryGetValue<string>("tmpfs_mode", out value))
                    {
                        // Note that unlike the secret/config file mode, the docker cli
                        // Tmpfs mount option doesn't seem to consider a leading "0"
                        // to specify octal, so we'll default this to decimal encoding.

                        mount.TmpfsMode = ParseFileMode(context, value, $"[mount.tmpfs_mode={value}] is not a valid Linux file mode.");
                    }
                }
                else
                {
                    if (jObject.ContainsKey("tmpfs_size") ||
                        jObject.ContainsKey("tmpfs_mode"))
                    {
                        context.WriteErrorLine($"[mount.tmpfs_*] options are not allowed for [mount.type={mount.Type}].");
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

            if (input == null || (input.Length != 3 && input.Length != 4))
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
                // We're going to prefix this with "0" to indicate
                // to Docker that this octal.

                if (!input.StartsWith("0"))
                {
                    input = "0" + input;
                }

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

                if (jObject == null)
                {
                    context.WriteErrorLine($"One or more of the [{argName}] array elements is not a valid published port specification.");
                    return publishedPorts;
                }

                if (!context.ValidateArguments(jObject, validPublishArgs, argName))
                {
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
                    if (Enum.TryParse<ServicePortMode>(value, true, out var portMode))
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
                    port.Mode = ServicePortMode.Ingress;
                }

                // Parse [protocol]

                if (jObject.TryGetValue<string>("protocol", out value))
                {
                    if (Enum.TryParse<ServicePortProtocol>(value, true, out var portProtocol))
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
                    port.Protocol = ServicePortProtocol.Tcp;
                }

                // Add the mount to the list.

                publishedPorts.Add(port);
            }

            return publishedPorts;
        }

        /// <summary>
        /// Parses the service's configs.
        /// </summary>
        /// <param name="context">The module context.</param>
        /// <param name="argName">The argument name.</param>
        /// <returns>The list of <see cref="Config"/> instances.</returns>
        private List<Config> ParseConfigs(ModuleContext context, string argName)
        {
            var configs = new List<Config>();

            if (!context.Arguments.TryGetValue(argName, out var jToken))
            {
                return configs;
            }

            var jArray = jToken as JArray;

            if (jArray == null)
            {
                context.WriteErrorLine($"Expected [{argName}] to be an array of config specifications.");
                return configs;
            }

            foreach (var item in jArray)
            {
                var jObject = item as JObject;

                if (jObject == null)
                {
                    context.WriteErrorLine($"One or more of the [{argName}] array items is not valid.");
                    return configs;
                }

                if (!context.ValidateArguments(jObject, validConfigArgs, argName))
                {
                    return configs;
                }

                var config = new Config();
                var value  = String.Empty;

                // Parse [source]

                if (jObject.TryGetValue<string>("source", out value))
                {
                    config.Source = value;
                }
                else
                {
                    context.WriteErrorLine($"A [{argName}] array element lacks the required [source] property.");
                    return configs;
                }

                // Parse [target]

                if (jObject.TryGetValue<string>("target", out value))
                {
                    config.Target = value;
                }
                else
                {
                    context.WriteErrorLine($"A [{argName}] array element lacks the required [target] property.");
                    return configs;
                }

                // Parse [mode]: We're going to allow 3 or 4 octets.

                if (jObject.TryGetValue<string>("mode", out value))
                {
                    config.Mode = ParseFileMode(context, value, $"[{argName}.mode={value}] is not a valid Linux file mode.");
                }

                // Parse [uid]

                if (jObject.TryGetValue<string>("uid", out value))
                {
                    if (int.TryParse(value, out var parsed))
                    {
                        config.UID = value;
                    }
                    else
                    {
                        context.WriteErrorLine($"[{argName}.uid={value}] property is not a valid user ID.");
                    }
                }

                // Parse [gid]

                if (jObject.TryGetValue<string>("gid", out value))
                {
                    if (int.TryParse(value, out var parsed))
                    {
                        config.GID = value;
                    }
                    else
                    {
                        context.WriteErrorLine($"[{argName}.gid={value}] property is not a valid group ID.");
                    }
                }

                // Add the config to the list.

                configs.Add(config);
            }

            return configs;
        }

        /// <summary>
        /// Parses the service's secrets.
        /// </summary>
        /// <param name="context">The module context.</param>
        /// <param name="argName">The argument name.</param>
        /// <returns>The list of <see cref="Secret"/> instances.</returns>
        private List<Secret> ParseSecrets(ModuleContext context, string argName)
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

                if (jObject == null)
                {
                    context.WriteErrorLine($"One or more of the [{argName}] array items is not valid.");
                    return secrets;
                }

                if (!context.ValidateArguments(jObject, validConfigArgs, argName))
                {
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
                    if (int.TryParse(value, out var parsed))
                    {
                        secret.UID = value;
                    }
                    else
                    {
                        context.WriteErrorLine($"[{argName}.uid={value}] property is not a valid user ID.");
                    }
                }

                // Parse [gid]

                if (jObject.TryGetValue<string>("gid", out value))
                {
                    if (int.TryParse(value, out var parsed))
                    {
                        secret.GID = value;
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
        /// Appends a command line option for a non-enum value if it's not
        /// <c>null</c> or set to the default.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="args">The argument list being appended.</param>
        /// <param name="option">The command line option (with leading dashes).</param>
        /// <param name="value">The option value.</param>
        /// <param name="defaultValue">Optionally specifies the default value.</param>
        /// <param name="units">Optional units to be appended to ther value.</param>
        private void AppendCreateOption<T>(List<object> args, string option, T? value, T defaultValue = default, string units = null)
            where T : struct
        {
            Covenant.Requires<ArgumentNullException>(args != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(option));

            units = units ?? string.Empty;

            if (value.HasValue && !value.Value.Equals(defaultValue))
            {
                if (typeof(T) == typeof(bool))
                {
                    // Special-case booleans by normalizing them to lowercase.

                    var boolValue = value as bool?;

                    var output = boolValue.Value ? "true" : "false";

                    args.Add($"{option}={output}{units}");
                }
                else
                {
                    args.Add($"{option}={value}{units}");
                }
            }
        }

        /// <summary>
        /// Appends a command line option for a <c>string</c> value if it's not
        /// <c>null</c> or empty.
        /// </summary>
        /// <param name="args">The argument list being appended.</param>
        /// <param name="option">The command line option (with leading dashes).</param>
        /// <param name="value">The option value.</param>
        private void AppendCreateOption(List<object> args, string option, string value)
        {
            Covenant.Requires<ArgumentNullException>(args != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(option));

            if (!string.IsNullOrEmpty(value))
            {
                args.Add($"{option}={value}");
            }
        }
        
        /// <summary>
        /// Appends a command line option for an <c>enum</c> value if it's not
        /// <c>null</c> or set to the default.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type.</typeparam>
        /// <param name="args">The argument list being appended.</param>
        /// <param name="option">The command line option (with leading dashes).</param>
        /// <param name="value">The option value.</param>
        /// <param name="defaultValue">Optionally specifies the default value.</param>
        private void AppendCreateOptionEnum<TEnum>(List<object> args, string option, TEnum? value, TEnum defaultValue = default)
            where TEnum : struct
        {
            Covenant.Requires<ArgumentNullException>(args != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(option));

            if (value.HasValue && !value.Value.Equals(defaultValue))
            {
                args.Add($"{option}={NeonHelper.EnumToString(value.Value)}");
            }
        }

        /// <summary>
        /// Appends a command line option for a <c>double</c> value if it's not
        /// <c>null</c> or set to the default.
        /// </summary>
        /// <param name="args">The argument list being appended.</param>
        /// <param name="option">The command line option (with leading dashes).</param>
        /// <param name="value">The option value.</param>
        /// <param name="defaultValue">Optionally specifies the default value.</param>
        private void AppendCreateOptionDouble(List<object> args, string option, double? value, double defaultValue = 0.0)
        {
            Covenant.Requires<ArgumentNullException>(args != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(option));

            if (value.HasValue && !value.Value.Equals(defaultValue))
            {
                args.Add($"{option}={value.Value.ToString("0.#")}");
            }
        }

        /// <summary>
        /// Starts a Docker service from a service definition.
        /// </summary>
        /// <param name="manager">The manager where the command will be executed.</param>
        /// <param name="context">The Ansible module context.</param>
        /// <param name="service">The Service definition.</param>
        /// <returns><c>true</c> if the service was created.</returns>
        private bool CreateService(SshProxy<NodeDefinition> manager, ModuleContext context, DockerServiceSpec service)
        {
            var args = new List<object>();

            args.Add($"--name={service.Name}");

            foreach (var config in service.Config)
            {
                var sb = new StringBuilder();

                if (!string.IsNullOrEmpty(config.Source))
                {
                    sb.AppendWithSeparator($"source={config.Source}", ",");
                }

                if (!string.IsNullOrEmpty(config.Target))
                {
                    sb.AppendWithSeparator($"target={config.Target}", ",");
                }

                if (config.UID != null)
                {
                    sb.AppendWithSeparator($"uid={config.UID}", ",");
                }

                if (config.GID != null)
                {
                    sb.AppendWithSeparator($"gid={config.GID}", ",");
                }

                if (config.Mode != null)
                {
                    sb.AppendWithSeparator($"mode={config.Mode}", ",");
                }

                args.Add($"--config={sb}");
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

            AppendCreateOption(args, "--detach", service.Detach);

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

            AppendCreateOptionEnum(args, "--endpoint-mode", service.EndpointMode);

            if (service.Command.Count > 0)
            {
                var sb = new StringBuilder();

                foreach (var item in service.Command)
                {
                    sb.AppendWithSeparator(item);
                }

                args.Add($"--entrypoint={sb}");
            }

            foreach (var env in service.Env)
            {
                args.Add($"--env={env}");
            }

            foreach (var resource in service.GenericResource)
            {
                args.Add($"--generic-resource={resource}");
            }

            foreach (var group in service.Groups)
            {
                args.Add($"--group={group}");
            }

            AppendCreateOption(args, "--health-cmd", service.HealthCmd);
            AppendCreateOption(args, "--health-interval", service.HealthInterval, units: "ns");
            AppendCreateOption(args, "--health-retries", service.HealthRetries);
            AppendCreateOption(args, "--health-start-period", service.HealthStartPeriod, units: "ns");
            AppendCreateOption(args, "--health-timeout", service.HealthTimeout, units: "ns");

            foreach (var host in service.Host)
            {
                args.Add($"--host={host}");
            }

            if (!string.IsNullOrEmpty(service.Hostname))
            {
                args.Add($"--hostname={service.Hostname}");
            }

            AppendCreateOptionEnum(args, "--isolation", service.Isolation);

            foreach (var label in service.Label)
            {
                args.Add($"--label={label}");
            }

            AppendCreateOption(args, "--limit-cpu", service.LimitCpu);
            AppendCreateOption(args, "--limit-memory", service.LimitMemory);
            AppendCreateOption(args, "--log-driver", service.LogDriver);

            if (service.LogOpt.Count > 0)
            {
                var sb = new StringBuilder();

                foreach (var option in service.LogOpt)
                {
                    sb.AppendWithSeparator(option, ",");
                }

                args.Add($"--log-opt={sb}");
            }

            AppendCreateOptionEnum(args, "--mode", service.Mode);

            foreach (var mount in service.Mount)
            {
                var sb = new StringBuilder();

                sb.AppendWithSeparator($"type={NeonHelper.EnumToString(mount.Type.Value)}", ",");

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
                    sb.AppendWithSeparator($"consistency={NeonHelper.EnumToString(mount.Consistency.Value)}", ",");
                }

                if (mount.BindPropagation.HasValue)
                {
                    sb.AppendWithSeparator($"bind-propagation={NeonHelper.EnumToString(mount.BindPropagation.Value)}", ",");
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

                foreach (var label in mount.VolumeLabel)
                {
                    sb.AppendWithSeparator($"volume-label=\"{label}\"", ",");
                }

                if (mount.TmpfsSize.HasValue)
                {
                    sb.AppendWithSeparator($"tmpfs-size={mount.TmpfsSize}", ",");
                }

                if (!string.IsNullOrEmpty(mount.TmpfsMode))
                {
                    sb.AppendWithSeparator($"tmpfs-mode={mount.TmpfsMode}", ",");
                }

                args.Add($"--mount={sb}");
            }

#if USERNS_REMAP
            // $todo(jeff.lill): https://github.com/moby/moby/issues/37560

            var needsHostNetwork = false;

            foreach (var network in service.Network)
            {
                args.Add($"--network={network}");

                if (network.Equals("host", StringComparison.InvariantCultureIgnoreCase))
                {
                    needsHostNetwork = true;
                }
            }
#endif

            AppendCreateOption(args, "--no-healthcheck", service.NoHealthCheck);

            if (service.NoResolveImage ?? false)
            {
                args.Add($"--no-resolve-image");
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

                if (port.Protocol.HasValue)
                {
                    sb.AppendWithSeparator($"protocol={NeonHelper.EnumToString(port.Protocol.Value)}", ",");
                }

                if (port.Mode.HasValue)
                {
                    sb.AppendWithSeparator($"mode={NeonHelper.EnumToString(port.Mode.Value)}", ",");
                }

                args.Add($"--publish={sb}");
            }

            args.Add("--quiet");    // Always suppress progress.

            AppendCreateOption(args, "--read-only", service.ReadOnly);
            AppendCreateOption(args, "--replicas", service.Replicas);
            AppendCreateOption(args, "--reserve-cpu", service.ReserveCpu);
            AppendCreateOption(args, "--reserve-memory", service.ReserveMemory);
            AppendCreateOptionEnum(args, "--restart-condition", service.RestartCondition);
            AppendCreateOption(args, "--restart-delay", service.RestartDelay, units: "ns");
            AppendCreateOption(args, "--restart-max-attempts", service.RestartMaxAttempts, 1);
            AppendCreateOption(args, "--restart-window", service.RestartWindow, units: "ns");
            AppendCreateOption(args, "--rollback-delay", service.RollbackDelay, units: "ns");
            AppendCreateOptionEnum(args, "--rollback-failure-action", service.RollbackFailureAction);
            AppendCreateOptionDouble(args, "--rollback-max-failure-ratio", service.RollbackMaxFailureRatio);
            AppendCreateOption(args, "--rollback-monitor", service.RollbackMonitor, units: "ns");
            AppendCreateOptionEnum(args, "--rollback-order", service.RollbackOrder);
            AppendCreateOption(args, "--rollback-parallelism", service.RollbackParallism, 1);

            foreach (var network in service.Network)
            {
                AppendCreateOption(args, "--network", network);
            }

            foreach (var secret in service.Secret)
            {
                var sb = new StringBuilder();

                if (!string.IsNullOrEmpty(secret.Source))
                {
                    sb.AppendWithSeparator($"source={secret.Source}", ",");
                }

                if (!string.IsNullOrEmpty(secret.Target))
                {
                    sb.AppendWithSeparator($"target={secret.Target}", ",");
                }

                if (secret.UID != null)
                {
                    sb.AppendWithSeparator($"uid={secret.UID}", ",");
                }

                if (secret.GID != null)
                {
                    sb.AppendWithSeparator($"gid={secret.GID}", ",");
                }

                if (secret.Mode != null)
                {
                    sb.AppendWithSeparator($"mode={secret.Mode}", ",");
                }

                args.Add($"--secret={sb}");
            }

            AppendCreateOption(args, "--stop-grace-period", service.StopGracePeriod, units: "ns");
            AppendCreateOption(args, "--stop-signal", service.StopSignal);
            AppendCreateOption(args, "--tty", service.TTY);
            AppendCreateOption(args, "--update-delay", service.UpdateDelay, units: "ns");
            AppendCreateOptionEnum(args, "--update-failure-action", service.UpdateFailureAction);
            AppendCreateOption(args, "--update-max-failure-ratio", service.UpdateMaxFailureRatio);
            AppendCreateOption(args, "--update-monitor", service.UpdateMonitor, units: "ns");
            AppendCreateOptionEnum(args, "--update-order", service.UpdateOrder);
            AppendCreateOption(args, "--update-parallelism", service.UpdateParallism, 1);
            AppendCreateOption(args, "--user", service.User);
            AppendCreateOption(args, "--with-registry-auth", service.WithRegistryAuth);
            AppendCreateOption(args, "--workdir", service.Dir);

            // The Docker image any service arguments are passed as regular
            // arguments, not command line options.

            args.Add(service.Image);

            foreach (var arg in service.Args)
            {
                args.Add(arg);
            }

            // Create the service.

            context.WriteLine(AnsibleVerbosity.Trace, $"Creating [{service.Name}] service.");
            context.WriteLine(AnsibleVerbosity.Important, $"COMMAND: docker service create {NeonHelper.NormalizeExecArgs(args.ToArray())}");

            var response = manager.DockerCommand(RunOptions.None, "docker service create", args.ToArray());

            if (response.ExitCode != 0)
            {
                context.WriteErrorLine($"[{service.Name}] service start failed.");
                context.WriteErrorLine($"[exitcode={response.ExitCode}]: {response.BashCommand}");
                context.WriteErrorLine(response.AllText);

                return false;
            }
            else
            {
                context.WriteLine(AnsibleVerbosity.Info, $"[{service.Name}] service created.");
                context.Changed = !context.CheckMode;

                return true;
            }
        }

        /// <summary>
        /// Updates an existing Docker service from a service definition.
        /// </summary>
        /// <param name="manager">The manager where the command will be executed.</param>
        /// <param name="context">The Ansible module context.</param>
        /// <param name="force">Optionally specifies that the </param>
        /// <param name="newServiceSpec">The desired service state.</param>
        /// <param name="currentDetails">The service state from a <b>docker service inspect</b> command parsed from JSON.</param>
        private void UpdateService(SshProxy<NodeDefinition> manager, ModuleContext context, bool force, DockerServiceSpec newServiceSpec, ServiceDetails currentDetails)
        {
            var serviceName = currentDetails.Spec.Name;

            // We need to list the networks so we'll be able to map network
            // IDs to network names when parsing the service details.

            var networksResponse = manager.DockerCommand(RunOptions.None, "docker", "network", "ls", "--no-trunc");

            if (networksResponse.ExitCode != 0)
            {
                context.WriteErrorLine($"Cannot list Docker networks: {networksResponse.AllText}");
                return;
            }

            // Convert the current Docker service details into a [DockerServiceSpec] so we'll
            // be able to compare the current and expected state and generate an update command
            // if required.

            var currentServiceSpec = DockerServiceSpec.FromDockerInspect(context, currentDetails, networksResponse.OutputText);
            var updateCmdArgs      = currentServiceSpec.DockerUpdateCommandArgs(context, newServiceSpec);

            if (updateCmdArgs == null)
            {
                if (force)
                {
                    if (context.CheckMode)
                    {
                        context.WriteLine(AnsibleVerbosity.Important, $"[{serviceName}] service is already configured as specified but we'll force an update because [force=true] when CHECK-MODE is disabled.");
                    }
                    else
                    {
                        context.WriteLine(AnsibleVerbosity.Info, $"[{serviceName}] service is already configured as specified but we'll force an update because [force=true].");
                        context.WriteLine(AnsibleVerbosity.Info, $"COMMAND: docker service update --force {serviceName}");

                        var response = manager.DockerCommand(RunOptions.None, "docker", "service", "update", "--force", serviceName);

                        if (response.ExitCode != 0)
                        {
                            context.WriteErrorLine($"Cannot update service [{serviceName}]: {response.AllText}");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Important, $"[{serviceName}] service was updated.");
                            context.Changed = true;
                        }
                    }
                }
                else
                {
                    context.WriteLine(AnsibleVerbosity.Important, $"[{serviceName}] is already configured as specified.");
                }
            }
            else
            {
                context.WriteLine(AnsibleVerbosity.Info, $"COMMAND: docker service update {NeonHelper.NormalizeExecArgs(updateCmdArgs)}");

                if (context.CheckMode)
                {
                    context.WriteLine(AnsibleVerbosity.Info, $"[{serviceName}] service will be updated when CHECK-MODE is disabled.");
                }
                else
                {
                    context.WriteLine(AnsibleVerbosity.Trace, $"Updating [{serviceName}] service.");

                    var response = manager.DockerCommand(RunOptions.None, "docker", "service", "update", updateCmdArgs);

                    if (response.ExitCode != 0)
                    {
                        context.WriteErrorLine($"Cannot update service [{serviceName}]: {response.AllText}");
                    }
                    else
                    {
                        context.WriteLine(AnsibleVerbosity.Important, $"[{serviceName}] service was updated.");
                        context.Changed = true;
                    }
                }
            }
        }
    }
}
