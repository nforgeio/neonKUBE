//-----------------------------------------------------------------------------
// FILE:	    KubeConfig.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;

namespace Neon.Kube
{
    /// <summary>
    /// Used to manage serialization of Kubernetes <b>kubeconfig</b> files. 
    /// These are used to manage cluster contexts on client machines:
    /// <a href="https://github.com/eBay/Kubernetes/blob/master/docs/user-guide/kubeconfig-file.md">more information</a>.
    /// </summary>
    /// <remarks>
    /// <note>
    /// This implementation currently supports only the a single kubeconfig
    /// located at <c>$HOME/.kube/config</c> (within the current user's
    /// HOME folder).  The <c>KUBECONFIG</c> environment variable is ignored.
    /// </note>
    /// </remarks>
    public class KubeConfig
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Reads and returns the current kubconfig.
        /// </summary>
        /// <returns>The parsed <see cref="KubeConfig"/> or an empty config if the file doesn't exist.</returns>
        public static KubeConfig Load()
        {
            var path = Path.Combine(KubeHelper.GetKubeUserFolder(), "config");

            if (File.Exists(path))
            {
                return NeonHelper.YamlDeserialize<KubeConfig>(File.ReadAllText(path));
            }
            else
            {
                return new KubeConfig();
            }
        }

        //---------------------------------------------------------------------
        // Instance members.

        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeConfig()
        {
        }

        /// <summary>
        /// The name of the current context or <c>null</c> when there is no current context.
        /// </summary>
        [JsonProperty(PropertyName = "current-context", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string CurrentContext { get; set; }

        /// <summary>
        /// The cluster API server protocol version (defaults to <b>v1</b>).
        /// </summary>
        [JsonProperty(PropertyName = "apiVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("v1")]
        public string ApiVersion { get; set; } = "v1";

        /// <summary>
        /// Identifies the document type: <b>Config</b>.
        /// </summary>
        [JsonProperty(PropertyName = "kind", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("Config")]
        public string Kind { get; set; } = "Config";

        /// <summary>
        /// The list of cluster configurations.
        /// </summary>
        [JsonProperty(PropertyName = "clusters", Required = Required.Always)]
        public List<KubeConfigCluster> Clusters { get; set; } = new List<KubeConfigCluster>();

        /// <summary>
        /// The list of user configurations.
        /// </summary>
        [JsonProperty(PropertyName = "users", Required = Required.Always)]
        public List<KubeConfigCluster> Users { get; set; } = new List<KubeConfigCluster>();

        /// <summary>
        /// The list of contexts.
        /// </summary>
        [JsonProperty(PropertyName = "contexts", Required = Required.Always)]
        public List<KubeConfigContext> Contexts { get; set; } = new List<KubeConfigContext>();

        /// <summary>
        /// The optional dictionary of preferences.
        /// </summary>
        [JsonProperty(PropertyName = "preferences", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, string> Preferences { get; set; } = null;
    }
}
