//-----------------------------------------------------------------------------
// FILE:	    KubeConfigContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
        private bool                    extensionsLoaded;
        private KubeContextExtension    cachedExtensions;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeConfigContext()
        {
            Properties = new KubeConfigContextProperties();
        }

        /// <summary>
        /// Constructs a configuration from a structured name.
        /// </summary>
        /// <param name="contextName">The structured context name.</param>
        public KubeConfigContext(KubeContextName contextName)
            : this()
        {
            Covenant.Requires<ArgumentNullException>(contextName != null, nameof(contextName));

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
        /// The context extension information for the context.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public KubeContextExtension Extension
        {
            get
            {
                if (cachedExtensions != null)
                {
                    return cachedExtensions;
                }

                if (extensionsLoaded)
                {
                    return null;
                }

                var extensionsPath = KubeHelper.GetContextExtensionPath((KubeContextName)Name);

                if (File.Exists(extensionsPath))
                {
                    cachedExtensions = NeonHelper.YamlDeserialize<KubeContextExtension>(KubeHelper.ReadFileTextWithRetry(extensionsPath));

                    // Validate the extension's cluster definition.

                    cachedExtensions.ClusterDefinition?.Validate();

                    // We need to fixup some references.

                    foreach (var nodeDefinition in cachedExtensions.ClusterDefinition.NodeDefinitions.Values)
                    {
                        nodeDefinition.Labels.Node = nodeDefinition;
                    }
                }

                extensionsLoaded = true;

                return cachedExtensions;
            }

            set
            {
                extensionsLoaded = true;
                cachedExtensions = value;
            }
        }
    }
}
