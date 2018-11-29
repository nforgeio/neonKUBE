//-----------------------------------------------------------------------------
// FILE:	    HiveComponentInfo.cs
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

namespace Neon.Hive
{
    /// <summary>
    /// Lists the latest versions of neonHIVE related infrastructure components
    /// that are compatible with a specific hive deployment.
    /// </summary>
    public class HiveComponentInfo
    {
        /// <summary>
        /// The latest compatible Docker Engine version.
        /// </summary>
        [JsonProperty(PropertyName = "Docker", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Docker { get; set; }

        /// <summary>
        /// The latest fully qualified Docker Debian package download URI.
        /// </summary>
        [JsonProperty(PropertyName = "DockerPackage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string DockerPackageUri { get; set; }

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
        /// image tag, like <b>nhive/neon-log-collector:latest</b> of the latest image that
        /// is compatible with a specific hive deployment.
        /// </summary>
        [JsonProperty(PropertyName = "ImageToFullyQualified", Required = Required.Always)]
        public Dictionary<string, string> ImageToFullyQualified { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// <para>
        /// Maps the name of a neonHIVE service or container like <b>neon-log-metricbeat</b>
        /// to the name of the Docker image (without a repository or tag) that implements the
        /// named component, like: <b>metricbeat</b>.
        /// </para>
        /// <note>
        /// Many components map to images with the same name, like the <b>neon-hive-manager</b>
        /// service and the <b>neon-hive-manager</b> image.  Thhis is not always the case though.
        /// For example, the <b>neon-proxy-public</b> and <b>neon-proxy-private</b> services and
        /// container are both implemented by the <b>neon-proxy</b> image and the <b>neon-log-metricbeat</b>
        /// containers are implemented by the <b>metricbeat</b> image.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "ComponentToImage", Required = Required.Always)]
        public Dictionary<string, string> ComponentToImage { get; set; } = new Dictionary<string, string>();
    }
}
