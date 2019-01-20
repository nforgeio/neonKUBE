//-----------------------------------------------------------------------------
// FILE:	    KubeConfigContext.cs
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
    /// Describes a Kubernetes context.
    /// </summary>
    public class KubeConfigContext
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeConfigContext()
        {
            Properties = new KubeConfigContextProperties();
        }

        /// <summary>
        /// Constructs a configuration from a Kubernetes configuration name string.
        /// </summary>
        /// <param name="configName">The configuration name formatted as <b>USER@CLUSTER[/NAMESPACE]</b>.</param>
        public KubeConfigContext(string configName)
        {
            var name = KubeConfigName.Parse(configName);

            this.Name = name.ToString();
        }

        /// <summary>
        /// Constructs a configuration from a structured name.
        /// </summary>
        /// <param name="configName">The structured configuration name.</param>
        public KubeConfigContext(KubeConfigName configName)
            : this()
        {
            Covenant.Requires<ArgumentNullException>(configName != null);

            this.Name = configName.ToString();
        }

        /// <summary>
        /// The local nickname for the context.
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        public string Name { get; set; }

        /// <summary>
        /// The context properties.
        /// </summary>
        [JsonProperty(PropertyName = "context", Required = Required.Always)]
        [YamlMember(Alias = "context", ApplyNamingConventions = false)]
        public KubeConfigContextProperties Properties { get; set; }
    }
}
