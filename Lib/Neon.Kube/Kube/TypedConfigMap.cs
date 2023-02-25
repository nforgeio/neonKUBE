//-----------------------------------------------------------------------------
// FILE:	    TypedConfigMap.cs
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Diagnostics;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// <para>
    /// Wraps a Kubernetes <see cref="V1ConfigMap"/> to support strongly typed configurations.
    /// This is persisted using a string dictionary where the configuration is persisted as
    /// JSON using the <b>"data"</b> key.
    /// </para>
    /// <note>
    /// This is typically used for persisting state to the cluster rather than for setting
    /// configuration for pods but can be used for that as well.
    /// </note>
    /// </summary>
    /// <typeparam name="TConfig">Specifies the configuration type.</typeparam>
    /// <remarks>
    /// <para>
    /// To create a configmap, use the <see cref="TypedConfigMap(string, string, TConfig)"/>
    /// constructor, specifying the configmap's Kubernetes name and namespace as well as an
    /// instance of the typesafe config; your typed config will be available as the <see cref="Config"/>
    /// property.  Configure your config as required and then call <b>IKubernetes.CreateNamespacedConfigMapAsync()</b>,
    /// passing <see cref="ConfigMap"/> as the request body (this holds the <see cref="V1ConfigMap"/>).
    /// </para>
    /// <para>
    /// To read an existing configmap, call <b>IKubernetes.CoreV1.ReadNamespacedConfigMapAsync</b> to retrieve the
    /// Kubernetes configmap and then call the static <see cref="From"/> method to wrap the result
    /// into a <see cref="TypedConfigMap{TConfig}"/> where your typesafe values can be accessed
    /// via the <see cref="Config"/> property.
    /// </para>
    /// <para>
    /// To update an existing config, call <b>IKubernetes.CoreV1.ReadNamespacedConfigMapAsync</b> to retrieve it, 
    /// modify it via the <see cref="Config"/> property and then call <b>IKubernetes.CoreV1.ReplaceNamespacedConfigMapAsync()</b>
    /// passing <see cref="ConfigMap"/>.
    /// </para>
    /// </remarks>
    public class TypedConfigMap<TConfig>
        where TConfig : class, new()
    {
        //---------------------------------------------------------------------
        // Static members

        private const string dataPropertyName = "data";

        /// <summary>
        /// Constructs an instance by parsing a <see cref="V1ConfigMap"/>.
        /// </summary>
        /// <param name="configMap">The source config map.</param>
        /// <returns>The parsed configuration</returns>
        public static TypedConfigMap<TConfig> From(V1ConfigMap configMap)
        {
            Covenant.Requires<ArgumentNullException>(configMap != null, nameof(configMap));

            return new TypedConfigMap<TConfig>(configMap);
        }

        //---------------------------------------------------------------------
        // Instance members

        private TConfig     config;

        /// <summary>
        /// Constructs an instance from an existing <see cref="V1ConfigMap"/>.
        /// </summary>
        /// <param name="configMap">The config map name as it will be persisted to Kubernetes.</param>
        public TypedConfigMap(V1ConfigMap configMap)
        {
            Covenant.Requires<ArgumentNullException>(configMap != null, nameof(configMap));

            if (!configMap.Data.TryGetValue(dataPropertyName, out var json))
            {
                throw new InvalidDataException($"Expected the [{configMap}] to have a [{dataPropertyName}] property.");
            }

            this.ConfigMap = configMap;
            this.Config    = NeonHelper.JsonDeserialize<TConfig>(json, strict: true);
        }

        /// <summary>
        /// Constructs an instance with the specified name and <typeparamref name="TConfig"/> value.
        /// </summary>
        /// <param name="name">Specifies the configmap name.</param>
        /// <param name="namespace">specifies the namespace.</param>
        /// <param name="config">
        /// Optionally specifies the initial config value.  A default instance will be created
        /// when this is <c>null</c>.
        /// </param>
        public TypedConfigMap(string name, string @namespace, TConfig config = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(@namespace), nameof(@namespace));
            Covenant.Requires<ArgumentNullException>(config != null, nameof(config));

            this.Config         = config ?? new TConfig();
            this.ConfigMap      = KubeHelper.CreateKubeObject<V1ConfigMap>(name);
            this.ConfigMap.Data = new Dictionary<string, string>();

            this.ConfigMap.Data[dataPropertyName] = NeonHelper.JsonSerialize(this.Config);
        }

        /// <summary>
        /// Returns the associated <see cref="V1ConfigMap"/>.
        /// </summary>
        public V1ConfigMap ConfigMap { get; private set; }

        /// <summary>
        /// Returns the current <typeparamref name="TConfig"/> value.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the value being set is <c>null</c>.</exception>
        public TConfig Config
        {
            get => config;

            set
            {
                Covenant.Requires<ArgumentNullException>(value != null, nameof(Config));

                config = value;
            }
        }

        /// <summary>
        /// Updates the configmap by persisting any changes to <see cref="Config"/> back to
        /// the Kubernetes configmap's <b>"data"</b> key.
        /// </summary>
        public void Update()
        {
            ConfigMap.Data[dataPropertyName] = NeonHelper.JsonSerialize(Config);
        }
    }
}
