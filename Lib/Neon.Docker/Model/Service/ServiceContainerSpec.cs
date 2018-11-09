//-----------------------------------------------------------------------------
// FILE:	    ServiceContainerSpec.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

namespace Neon.Docker
{
    /// <summary>
    /// Service container/task specification.
    /// </summary>
    public class ServiceContainerSpec : INormalizable
    {
        /// <summary>
        /// <para>
        /// The image used to provision the service container.
        /// </para>
        /// <note>
        /// This may include the image's <b>@sha256:...</b> appended to the tag.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Image", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string Image { get; set; }

        /// <summary>
        /// Returns the <see cref="Image"/> without any SHA hash appended to the tag.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public string ImageWithoutSHA
        {
            get
            {
                if (string.IsNullOrEmpty(Image))
                {
                    return Image;
                }

                var pos = Image.IndexOf(':');

                if (pos == -1)
                {
                    return Image;
                }

                pos = Image.IndexOf('@', pos);

                if (pos == -1)
                {
                    return Image;
                }

                return Image.Substring(0, pos);
            }
        }

        /// <summary>
        /// The container labels formatted as <b>LABEL=VALUE</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Labels", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public Dictionary<string, string> Labels { get; set; }

        /// <summary>
        /// The command to be run in the image.
        /// </summary>
        [JsonProperty(PropertyName = "Command", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<string> Command { get; set; }

        /// <summary>
        /// Arguments to the command.
        /// </summary>
        [JsonProperty(PropertyName = "Args", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<string> Args { get; set; }

        /// <summary>
        /// Hostname for the container.
        /// </summary>
        [JsonProperty(PropertyName = "Hostname", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string Hostname { get; set; }

        /// <summary>
        /// Environment variables of the form <b>VARIABLE=VALUE</b> or <b>VARIABLE</b>
        /// to pass environment variables from the Docker host.
        /// </summary>
        [JsonProperty(PropertyName = "Env", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<string> Env { get; set; }

        /// <summary>
        /// The container working directory where commands will run.
        /// </summary>
        [JsonProperty(PropertyName = "Dir", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string Dir { get; set; }

        /// <summary>
        /// The user within the container.
        /// </summary>
        [JsonProperty(PropertyName = "User", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string User { get; set; }

        /// <summary>
        /// The list of additional groups that the command will run as.
        /// </summary>
        [JsonProperty(PropertyName = "Groups", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<string> Groups { get; set; }

        /// <summary>
        /// Security options for the container.
        /// </summary>
        [JsonProperty(PropertyName = "Privileges", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServicePrivileges Privileges { get; set;}

        /// <summary>
        /// Optionally create a pseudo TTY.
        /// </summary>
        [JsonProperty(PropertyName = "TTY", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool TTY { get; set; }

        /// <summary>
        /// Open STDIN.
        /// </summary>
        [JsonProperty(PropertyName = "OpenStdin", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool OpenStdin { get; set; }

        /// <summary>
        /// Optionally mount the service container file system as read-only.
        /// </summary>
        [JsonProperty(PropertyName = "ReadOnly", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool ReadOnly { get; set; }

        /// <summary>
        /// Specifies file system mounts to be added to the service containers.
        /// </summary>
        [JsonProperty(PropertyName = "Mounts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<ServiceMount> Mounts { get; set; }

        /// <summary>
        /// Signal to be used to gracefully stop the service containers.
        /// </summary>
        [JsonProperty(PropertyName = "StopSignal", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public string StopSignal { get; set; }

        /// <summary>
        /// Time to wait for a service container to stop gracefully before killing it
        /// forcefully (in nanoseconds).
        /// </summary>
        [JsonProperty(PropertyName = "StopGracePeriod", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(0)]
        public long StopGracePeriod { get; set; }

        /// <summary>
        /// Specifies how service container health check are to be performed.
        /// </summary>
        [JsonProperty(PropertyName = "HealthCheck", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceHealthCheck HealthCheck { get; set; }

        /// <summary>
        /// <para>
        /// Lists the hostname/IP address mappings to add to the service container
        /// [/etc/hosts] file.  Each entry is formatted like:
        /// </para>
        /// <example>
        /// IP_address canonical_hostname [aliases...]
        /// </example>
        /// </summary>
        [JsonProperty(PropertyName = "Hosts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<string> Hosts { get; set; }

        /// <summary>
        /// DNS resolver configuration.
        /// </summary>
        [JsonProperty(PropertyName = "DNSConfig", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public ServiceDnsConfig DNSConfig { get; set; }

        /// <summary>
        /// Specifies the secrets to be exposed to the service containers.
        /// </summary>
        [JsonProperty(PropertyName = "Secrets", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<ServiceSecret> Secrets { get; set; }

        /// <summary>
        /// Specifies the configs to be exposed to the service containers.
        /// </summary>
        [JsonProperty(PropertyName = "Configs", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(null)]
        public List<ServiceConfig> Configs { get; set; }

        /// <summary>
        /// <b>Windows Only:</b> Specifies the isolation technology to be used
        /// for the service containers.
        /// </summary>
        [JsonProperty(PropertyName = "Isolation", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(default(ServiceIsolationMode))]
        public ServiceIsolationMode Isolation { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Labels      = Labels ?? new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            Command     = Command ?? new List<string>();
            Args        = Args ?? new List<string>();
            Env         = Env ?? new List<string>();
            Groups      = Groups ?? new List<string>();
            Secrets     = Secrets ?? new List<ServiceSecret>();
            Configs     = Configs ?? new List<ServiceConfig>();
            Mounts      = Mounts ?? new List<ServiceMount>();
            HealthCheck = HealthCheck ?? new ServiceHealthCheck();
            Hosts       = Hosts ?? new List<string>();

            Privileges?.Normalize();
            HealthCheck?.Normalize();
            DNSConfig?.Normalize();

            foreach (var item in Secrets)
            {
                item?.Normalize();
            }

            foreach (var item in Configs)
            {
                item?.Normalize();
            }

            foreach (var item in Mounts)
            {
                item?.Normalize();
            }
        }
    }
}
