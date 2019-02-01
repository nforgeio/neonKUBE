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
        private KubeContextExtension cachedExtension;

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
        /// <param name="contextName">The context name formatted as <b>USER@CLUSTER[/NAMESPACE]</b>.</param>
        public KubeConfigContext(string contextName)
        {
            var name = KubeContextName.Parse(contextName);

            this.Name = name.ToString();
        }

        /// <summary>
        /// Constructs a configuration from a structured name.
        /// </summary>
        /// <param name="contextName">The structured context name.</param>
        public KubeConfigContext(KubeContextName contextName)
            : this()
        {
            Covenant.Requires<ArgumentNullException>(contextName != null);

            this.Name = contextName.ToString();
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

        /// <summary>
        /// Returns the context extension information for the context.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public KubeContextExtension Extension
        {
            get
            {
                if (cachedExtension != null)
                {
                    return cachedExtension;
                }

                return cachedExtension = NeonHelper.YamlDeserialize<KubeContextExtension>(File.ReadAllText(KubeHelper.GetContextExtensionPath(Name)));
            }
        }
    }
}
