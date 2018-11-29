//-----------------------------------------------------------------------------
// FILE:	    DockerOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// Describes the Docker options for a neonHIVE.
    /// </summary>
    public class DockerOptions
    {
        private const string    defaultLogDriver     = "fluentd";
        private const string    defaultLogOptions    = "tag=;fluentd-async-connect=true;fluentd-max-retries=1000000000;fluentd-buffer-limit=5242880";
        private const bool      defaultRegistryCache = true;
        private const bool      defaultUsernsRemap   = true;
        private const bool      defaultExperimental  = false;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public DockerOptions()
        {
        }

        /// <summary>
        /// Returns the Swarm node advertise port.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public int SwarmPort
        {
            get { return NetworkPorts.DockerSwarm; }
        }

        /// <summary>
        /// <para>
        /// The version of Docker to be installed like [18.03.0-ce].  You may also specify <b>latest</b>
        /// to install the most recent compatible stable release.
        /// </para>
        /// <note>
        /// Only Community Editions of Docker are supported at this time.
        /// </note>
        /// <para>
        /// This defaults to <b>latest</b>.
        /// </para>
        /// <note>
        /// <para><b>IMPORTANT!</b></para>
        /// <para>
        /// Production hives should always install a specific version of Docker so 
        /// it will be easy to add new hosts in the future that will have the same 
        /// Docker version as the rest of the hive.  This also prevents the package
        /// manager from inadvertently upgrading Docker.
        /// </para>
        /// </note>
        /// <note>
        /// <para><b>IMPORTANT!</b></para>
        /// <para>
        /// It is not possible for the <b>neon-cli</b> tool to upgrade Docker on hives
        /// that deployed the <b>test</b> or <b>experimental</b> build.
        /// </para>
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Version", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("latest")]
        public string Version { get; set; } = "latest";

        /// <summary>
        /// Specifies the Docker Registries and the required credentials that will
        /// be made available to the hive.  Note that the Docker public registry
        /// will always be available to new hives.
        /// </summary>
        [JsonProperty(PropertyName = "Registries", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<RegistryCredentials> Registries { get; set; } = new List<RegistryCredentials>();

        /// <summary>
        /// Optionally indicates that local pull-thru Docker registry caches are to be deployed
        /// on the hive manager nodes.  This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "RegistryCache", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultRegistryCache)]
        public bool RegistryCache { get; set; } = defaultRegistryCache;

        /// <summary>
        /// Specifies the Docker daemon default log driver.  This defaults to <b>fluentd</b>
        /// so that container logs will be forwarded to the hive logging pipeline.
        /// </summary>
        [JsonProperty(PropertyName = "LogDriver", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultLogDriver)]
        public string LogDriver { get; set; } = defaultLogDriver;

        /// <summary>
        /// <para>
        /// Specifies the Docker daemon container logging options separated by colons <b>(:)</b>.  This defaults to:
        /// </para>
        /// <code language="none">
        /// tag=;fluentd-async-connect=true;fluentd-max-retries=1000000000;fluentd-buffer-limit=5242880
        /// </code>
        /// <para>
        /// which by default, will forward container logs to the hive logging pipeline.
        /// </para>
        /// </summary>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> Always use the <b>fluentd-async-connect=true</b> option
        /// when using the <b>fluentd</b> log driver.  Containers without this will stop if
        /// the logging pipeline is not ready when the container starts.
        /// </note>
        /// <para>
        /// You may have individual services and containers opt out of hive logging by setting
        /// <b>--log-driver=json-file</b> or <b>--log-driver=none</b>.  This can be handy while
        /// debugging Docker images.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "LogOptions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultLogOptions)]
        public string LogOptions { get; set; } = defaultLogOptions;

        /// <summary>
        /// The seconds Docker should wait before restarting a container or service instance.
        /// This defaults to 10 seconds.
        /// </summary>
        [JsonProperty(PropertyName = "RestartDelaySeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(10)]
        public int RestartDelaySeconds { get; set; } = 10;

        /// <summary>
        /// Returns the <see cref="RestartDelaySeconds"/> property converted to a string with
        /// a seconds unit appended, suitable for passing to a Docker command.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public string RestartDelay
        {
            get { return $"{RestartDelaySeconds}s"; }
        }

        /// <summary>
        /// Controls whether the Docker Ingress network is used for for hive proxies.  This defaults to <c>null</c>
        /// which is currently equivalent to <c>true</c> for hives deployed locally on <see cref="HostingEnvironments.HyperVDev"/>
        /// and <c>false</c> for all other hosting environments.
        /// </summary>
        [JsonProperty(PropertyName = "AvoidIngressNetwork", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public bool? AvoidIngressNetwork { get; set; } = null;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Indicates whether the Docker ingress network should be used
        /// for traffic manager instances based on <see cref="AvoidIngressNetwork"/> and the current
        /// hosting environment.
        /// </summary>
        /// <param name="hiveDefinition">The current hive definition.</param>
        public bool GetAvoidIngressNetwork(HiveDefinition hiveDefinition)
        {
            Covenant.Requires<ArgumentNullException>(hiveDefinition != null);

            if (AvoidIngressNetwork.HasValue)
            {
                return AvoidIngressNetwork.Value;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// <note>
        /// This setting is currently being ignored due to <a href="https://github.com/moby/moby/issues/37560">this issue</a>.
        /// </note>
        /// <para>
        /// Enables Docker <a href="namespace remapping">https://docs.docker.com/engine/security/userns-remap/</a>
        /// such that containers will run as the <b>dockremap</b> user rather than <b>root</b>.
        /// This defaults to <c>true</c> for better container security.
        /// </para>
        /// </summary>
        /// <remarks>
        /// <para>
        /// It is generally considered to be a best practice to avoid running containers
        /// as <b>root</b> even though Linux namespaces and cgroups try to prevent 
        /// containers from interacting with host resources or other containers.  One
        /// basic problem is that containers running as <b>root</b> will have root
        /// permissions on any files mounted to the container.  This means that a
        /// container mounting the host root folder will have R/W access to the entire
        /// host file system.
        /// </para>
        /// <para>
        /// It's also potentially possible for a container running as root to elevate
        /// its permissions via obscure commands or possible due to Docker or Linux
        /// bugs.  Containers don't have the chance to do this if they're not running
        /// as <b>root</b>.
        /// </para>
        /// <para>
        /// You may find that some of your containers may need elevated permissions
        /// for example for <b>--network=host</b> or to listen on protected TCP/UDP
        /// ports below 1024.  You can specify <b>--userns=host</b> to override this
        /// setting and run your container as <b>root</b> and/or the <b>--cap-add CAPABILITY</b> 
        /// option to grant specific capabilities to your containers.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "UsernsRemap", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultUsernsRemap)]
        public bool UsernsRemap { get; set; } = defaultUsernsRemap;

        /// <summary>
        /// Enables experimental Docker features.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Experimental", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultExperimental)]
        public bool Experimental { get; set; } = defaultExperimental;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="HiveDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(HiveDefinition hiveDefinition)
        {
            Covenant.Requires<ArgumentNullException>(hiveDefinition != null);

            Version             = Version ?? "latest";
            Registries = Registries ?? new List<RegistryCredentials>();

            if (RestartDelaySeconds < 0)
            {
                RestartDelaySeconds = 0;
            }

            var version = Version.Trim().ToLower();
            Uri uri     = null;

            if (version == "latest" ||
                version == "test" ||
                version == "experimental")
            {
                Version = version.ToLowerInvariant();
            }
            else
            {
                Version = version.ToLowerInvariant();
                uri     = new Uri($"https://codeload.github.com/moby/moby/tar.gz/v{version}");

                if (!version.EndsWith("-ce"))
                {
                    throw new HiveDefinitionException($"[{nameof(DockerOptions)}.{Version}] does not specify a Docker community edition.  neonHIVE only supports Docker Community Edition at this time.");
                }
            }

            LogDriver  = LogDriver ?? defaultLogDriver;
            LogOptions = LogOptions ?? defaultLogOptions;

            // $todo(jeff.lill): 
            //
            // This check doesn't work for non-stable releases now that Docker has implemented
            // the new stable, edge, testing release channel scheme.  At some point, it would
            // be interesting to try to figure out another way.
            //
            // Probably the best approach would be to actually use [apt-get] to list the 
            // available versions.  This would look something like:
            //
            //      # Configure the stable, edge, and testing repositorties
            //  
            //      add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable"
            //      add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) edge"
            //      add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) testing"
            //      apt-get update
            //
            // and then use the following the list the versions:
            //
            //      apt-get install -yq docker-ce=${docker_version}
            //
            // I'm doubtful that it's possible to implement this directly in the [neon-cli].
            // One approach would be to have a service that polls [apt-get] for this a few
            // times a day and then exposes a REST API that can answer the question.
#if TODO
            if (uri != null)
            {
                // Verify that the Docker download actually exists.

                using (var client = new HttpClient())
                {
                    try
                    {
                        var request  = new HttpRequestMessage(HttpMethod.Head, uri);
                        var response = client.SendAsync(request).Result;

                        response.EnsureSuccessStatusCode();
                    }
                    catch (Exception e)
                    {
                        throw new HiveDefinitionException($"Cannot confirm that Docker release [{version}] exists at [{uri}].  {NeonHelper.ExceptionError(e)}");
                    }
                }
            }
#endif
            foreach (var registry in Registries)
            {
                var hostname = registry.Registry;

                if (string.IsNullOrEmpty(hostname) || !HiveDefinition.DnsHostRegex.IsMatch(hostname))
                {
                    throw new HiveDefinitionException($"[{nameof(DockerOptions)}.{nameof(Registries)}] includes a [{nameof(Neon.Hive.RegistryCredentials.Registry)}={hostname}] is not a valid registry hostname.");
                }
            }
        }

        /// <summary>
        /// Clears any sensitive properties like the Docker registry credentials.
        /// </summary>
        public void ClearSecrets()
        {
            Registries.Clear();
        }
    }
}
