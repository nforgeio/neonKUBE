//-----------------------------------------------------------------------------
// FILE:	    HiveComponentVersions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Cryptography;

namespace Neon.Cluster
{
    /// <summary>
    /// Lists the latest versions of neonHIVE related infrastructure components
    /// that are compatible with a specific hive deployment.
    /// </summary>
    public class HiveComponentVersions
    {
        /// <summary>
        /// The latest compatible Docker Engine.
        /// </summary>
        [JsonProperty(PropertyName = "Docker", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Docker { get; set; }

        /// <summary>
        /// The latest fully qualified Docker Debian package name.
        /// </summary>
        [JsonProperty(PropertyName = "DockerPackage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string DockerPackage { get; set; }

        /// <summary>
        /// The latest compatible HashiCorp Consul.
        /// </summary>
        [JsonProperty(PropertyName = "Consul", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Consul { get; set; }

        /// <summary>
        /// The latest compatible HashiCorp Vault.
        /// </summary>
        [JsonProperty(PropertyName = "Vault", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Vault { get; set; }

        /// <summary>
        /// Maps the unqualified name of a neonHIVE image like <b>neon-log-collector</b> to the
        /// full path to the image including the Docker Hub organization, registry hostname and
        /// image tag, like <b>neoncluster/neon-log-collector:latest</b> of the latest image that
        /// is compatible with a specific hive deployment.
        /// </summary>
        [JsonProperty(PropertyName = "Vault", Required = Required.Always)]
        public Dictionary<string, string> Images { get; set; } = new Dictionary<string, string>();
    }
}
