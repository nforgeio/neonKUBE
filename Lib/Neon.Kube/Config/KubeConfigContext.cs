//-----------------------------------------------------------------------------
// FILE:	    KubeConfigContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

using k8s.KubeConfigModels;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;

namespace Neon.Kube.Config
{
    /// <summary>
    /// Describes a Kubernetes cluster configuration.
    /// </summary>
    public class KubeConfigContext
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeConfigContext()
        {
            this.Context = new KubeConfigContextConfig();
        }

        /// <summary>
        /// Specifies the context name.
        /// </summary>
        /// <param name="clusterName">Specifies the cluster name.</param>
        public KubeConfigContext(string clusterName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterName), nameof(clusterName));

            this.Name = clusterName;
        }

        /// <summary>
        /// Specifies the context name.
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        public string Name { get; set; }

        /// <summary>
        /// Specifies the linked cluster name.
        /// </summary>
        [JsonProperty(PropertyName = "cluster", Required = Required.Always)]
        [YamlMember(Alias = "cluster", ApplyNamingConventions = false)]
        public string Cluster { get; set; }

        /// <summary>
        /// Specifies the linked user name.
        /// </summary>
        [JsonProperty(PropertyName = "user", Required = Required.Always)]
        [YamlMember(Alias = "user", ApplyNamingConventions = false)]
        public string User { get; set; }

        /// <summary>
        /// The cluster properties.
        /// </summary>
        [JsonProperty(PropertyName = "context", Required = Required.Always)]
        [YamlMember(Alias = "context", ApplyNamingConventions = false)]
        public KubeConfigContextConfig Context { get; set; }

        /// <summary>
        /// Returns an extension value.
        /// </summary>
        /// <typeparam name="T">Specifies the value type.</typeparam>
        /// <param name="name">Specifies the extension name.</param>
        /// <param name="default">Specifies the value to be returned when the extension is not found.</param>
        /// <returns>The extension value.</returns>
        public T GetExtensionValue<T>(string name, T @default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            if (Context == null || Context.Extensions == null)
            {
                return @default;
            }

            return Context.Extensions.Get<T>(name, @default);
        }

        /// <summary>
        /// Sets an extension value.
        /// </summary>
        /// <typeparam name="T">Specifies the value type.</typeparam>
        /// <param name="name">Specifies the extension name.</param>
        /// <param name="value">Specifies the value being set.</param>
        public void SetExtensionValue<T>(string name, T value)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            if (Context == null)
            {
                Context = new KubeConfigContextConfig();
            }

            if (Context.Extensions == null)
            {
                Context.Extensions = new List<NamedExtension>();
            }

            Context.Extensions.Set<T>(name, value);
        }
    }
}
