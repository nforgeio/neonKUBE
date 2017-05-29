//-----------------------------------------------------------------------------
// FILE:	    DockerOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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

using Neon.Common;
using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes the Docker options for a NeonCluster.
    /// </summary>
    public class DockerOptions
    {
        private const string    defaultLogOptions    = "--log-driver=fluentd --log-opt tag= --log-opt fluentd-async-connect=true";
        private const string    defaultRegistry      = "https://registry-1.docker.io";
        private const bool      defaultRegistryCache = true;
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
        public int SwarmPort
        {
            get { return NetworkPorts.DockerSwarm; }
        }

        /// <summary>
        /// <para>
        /// The version of Docker to be installed.  This can be an older released Docker version
        /// like <b>1.13.0</b> a new version like [17.03.0-ce].  You may also specify <b>latest</b>
        /// to install the most recent production release or <b>test</b> or <b>experimental</b> to
        /// install the latest releases from the test or experimental channels.
        /// </para>
        /// <para>
        /// You can also specify the HTTP/HTTPS URI to the binary package to be installed.
        /// This is useful for installing a custom build or a development snapshot copied 
        /// from https://master.dockerproject.org/.  Be sure to copy the TAR file from:
        /// </para>
        /// <example>
 		/// linux/amd64/docker-<b>docker-version</b>-dev.tgz
        /// </example>
        /// <para>
        /// This defaults to <b>latest</b>.
        /// </para>
        /// <note>
        /// <para><b>IMPORTANT!</b></para>
        /// <para>
        /// Production clusters should always install a specific version of Docker so 
        /// it will be easy to add new hosts in the future that will have the same 
        /// Docker version as the rest of the cluster.  This also prevents the package
        /// manager from inadvertently upgrading Docker.
        /// </para>
        /// </note>
        /// <note>
        /// <para><b>IMPORTANT!</b></para>
        /// <para>
        /// It is not possible for the <b>neon-cli</b> tool to upgrade Docker on clusters
        /// that deployed the <b>test</b> or <b>experimental</b> build.
        /// </para>
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Version", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("latest")]
        public string Version { get; set; } = "latest";

        /// <summary>
        /// Specifies the URL of the Docker registry the cluster will use to download Docker images.
        /// This defaults to the Public Docker registry: <b>https://registry-1.docker.io</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Registry", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultRegistry)]
        public string Registry { get; set; } = defaultRegistry;

        /// <summary>
        /// Optionally specifies the username to be used to authenticate against the Docker registry
        /// and mirrors.
        /// </summary>
        [JsonProperty(PropertyName = "RegistryUserName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string RegistryUserName { get; set; } = null;

        /// <summary>
        /// Optionally specifies the password to be used to authenticate against the Docker registry
        /// and mirrors.
        /// </summary>
        [JsonProperty(PropertyName = "RegistryPassword", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string RegistryPassword { get; set; } = null;

        /// <summary>
        /// Optionally indicates that local pull-thru Docker registry caches are to be deployed
        /// on the cluster manager nodes.  This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "RegistryCache", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultRegistryCache)]
        public bool RegistryCache { get; set; } = defaultRegistryCache;

        /// <summary>
        /// <para>
        /// The Docker daemon container logging options.  This defaults to:
        /// </para>
        /// <code language="none">
        /// --log-driver=fluentd --log-opt tag= --log-opt fluentd-async-connect=true
        /// </code>
        /// <para>
        /// which by default, will forward container logs to the cluster logging pipeline.
        /// </para>
        /// </summary>
        /// <remarks>
        /// <note>
        /// <b>IMPORTANT:</b> Always use the <b>--log-opt fluentd-async-connect=true</b> option
        /// when using the <b>fluentd</b> log driver.  Containers without this will stop if
        /// the logging pipeline is not ready when the container starts.
        /// </note>
        /// <para>
        /// You may have individual services and containers opt out of cluster logging by setting
        /// <b>--log-driver=json-text</b> or <b>--log-driver=none</b>.  This can be handy while
        /// debugging Docker images.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "LogOptions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultLogOptions)]
        public string LogOptions { get; set; } = defaultLogOptions;

        /// <summary>
        /// Enables experimental Docker features.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Experimental", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultExperimental)]
        public bool Experimental { get; set; } = defaultExperimental;

        /// <summary>
        /// Validates the options definition and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            Version          = Version ?? "latest";
            Registry         = Registry ?? defaultRegistry;
            RegistryUserName = RegistryUserName ?? string.Empty;
            RegistryPassword = RegistryPassword ?? string.Empty;

            var version = Version.Trim().ToLower();
            Uri uri     = null;

            if (version == "latest" ||
                version == "test" ||
                version == "experimental")
            {
                Version   = version.ToLowerInvariant();
            }
            else if (Uri.TryCreate(Version, UriKind.Absolute, out uri))
            {
                Version = uri.ToString();   // Ensure that the scheme is lowercase.
            }
            else
            {
                Version = version;
                uri     = new Uri($"https://get.docker.com/builds/Linux/x86_64/docker-{version}.tgz");
            }

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
                        throw new ClusterDefinitionException($"Cannot confirm that Docker release [{version}] exists at [{uri}].  {NeonHelper.ExceptionError(e)}");
                    }
                }
            }

            if (string.IsNullOrEmpty(Registry) || !Uri.TryCreate(Registry, UriKind.Absolute, out uri))
            {
                throw new ClusterDefinitionException($"[{nameof(DockerOptions)}.{nameof(Registry)}={Registry}] is not a valid registry URI.");
            }
        }
    }
}
