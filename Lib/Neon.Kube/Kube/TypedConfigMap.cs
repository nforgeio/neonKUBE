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
    /// Extends a Kubernetes <see cref="V1ConfigMap"/> to support strongly typed configurations.
    /// This is implemented by serializing the config data as JSON and adding that to the 
    /// low-level configmap under the <see cref="DataPropertyName"/> key.
    /// </para>
    /// <note>
    /// This is typically used for persisting state to the cluster rather than for setting
    /// configuration for pods but can be used for that as well.
    /// </note>
    /// </summary>
    /// <typeparam name="TConfigMapData">Specifies the configmap data type.</typeparam>
    /// <remarks>
    /// <para>
    /// To create a configmap, use the <see cref="TypedConfigMap(string, string, TConfigMapData)"/>
    /// constructor, specifying the configmap's Kubernetes name and namespace as well as an
    /// instance of the typesafe config; your typed config will be available as the <see cref="Data"/>
    /// property.  Configure your config as required and then call <b>IKubernetes.CreateNamespacedTypedConfigMapAsync()</b>,
    /// passing <see cref="UntypedConfigMap"/> as the request body (this holds the <see cref="V1ConfigMap"/>).
    /// </para>
    /// <para>
    /// To read an existing configmap, call <b>IKubernetes.CoreV1.ReadNamespacedTypedConfigMapAsync()</b> 
    /// to retrieve the Kubernetes configmap and then call the static <see cref="From"/> method to wrap
    /// the result into a <see cref="TypedConfigMap{TConfigMapData}"/> where your typesafe values can be accessed
    /// via the <see cref="Data"/> property.
    /// </para>
    /// <para>
    /// To update an existing config, call <b>IKubernetes.CoreV1.ReadNamespacedTypedConfigMapAsync</b> to retrieve it, 
    /// modify it via the <see cref="Data"/> property and then call <b>IKubernetes.CoreV1.ReplaceNamespacedTypedConfigMapAsync()</b>
    /// passing the new <see cref="UntypedConfigMap"/>.
    /// </para>
    /// </remarks>
    public class TypedConfigMap<TConfigMapData>
        where TConfigMapData : class, new()
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// <para>
        /// Identifies the key used to store typed data within an untyped configmap.
        /// </para>
        /// <note>
        /// <b>WARNING:</b> DO NOT MODIFY!  Any change will break existing clusters.
        /// </note>
        /// </summary>
        public const string DataPropertyName = "typed-data";

        /// <summary>
        /// Constructs an instance by parsing a <see cref="V1ConfigMap"/>.
        /// </summary>
        /// <param name="untypedonfigMap">Specifies the untyped config map..</param>
        /// <returns>The typed configmap.</returns>
        public static TypedConfigMap<TConfigMapData> From(V1ConfigMap untypedonfigMap)
        {
            Covenant.Requires<ArgumentNullException>(untypedonfigMap != null, nameof(untypedonfigMap));

            return new TypedConfigMap<TConfigMapData>(untypedonfigMap);
        }

        //---------------------------------------------------------------------
        // Instance members

        private TConfigMapData data;

        /// <summary>
        /// Constructs an instance from an untyped <see cref="V1ConfigMap"/>.
        /// </summary>
        /// <param name="untypedConfigMap">The config map name as it will be persisted to Kubernetes.</param>
        public TypedConfigMap(V1ConfigMap untypedConfigMap)
        {
            Covenant.Requires<ArgumentNullException>(untypedConfigMap != null, nameof(untypedConfigMap));

            if (!untypedConfigMap.Data.TryGetValue(DataPropertyName, out var json))
            {
                throw new InvalidDataException($"Expected the [{untypedConfigMap}] to have a [{DataPropertyName}] property.");
            }

            UntypedConfigMap = untypedConfigMap;
            Data             = NeonHelper.JsonDeserialize<TConfigMapData>(json, strict: true);
        }

        /// <summary>
        /// Constructs a typed configmap with the specified name and an optional initial value
        /// <typeparamref name="TConfigMapData"/> value.
        /// </summary>
        /// <param name="name">Specifies the configmap name.</param>
        /// <param name="namespace">specifies the namespace.</param>
        /// <param name="data">
        /// Optionally specifies the configmap data.  A default instance will be created
        /// when this is <c>null</c>.
        /// </param>
        public TypedConfigMap(string name, string @namespace, TConfigMapData data = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(@namespace), nameof(@namespace));
            Covenant.Requires<ArgumentNullException>(data != null, nameof(data));

            Data                                        = data ?? new TConfigMapData();
            UntypedConfigMap                            = KubeHelper.CreateKubeObject<V1ConfigMap>(name);
            UntypedConfigMap.Metadata.NamespaceProperty = @namespace;
            UntypedConfigMap.Data                       = new Dictionary<string, string>();
            UntypedConfigMap.Data[DataPropertyName]     = NeonHelper.JsonSerialize(Data);
        }

        /// <summary>
        /// Returns the associated untyped configmap.
        /// </summary>
        public V1ConfigMap UntypedConfigMap { get; private set; }

        /// <summary>
        /// Specifies the current typed configmap data.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the value being set is <c>null</c>.</exception>
        public TConfigMapData Data
        {
            get => data;

            set
            {
                Covenant.Requires<ArgumentNullException>(value != null, nameof(Data));

                data = value;
            }
        }

        /// <summary>
        /// Updates the configmap by persisting any changes to <see cref="Data"/> back to
        /// the Kubernetes configmap's <see cref="DataPropertyName"/> key.
        /// </summary>
        public void Update()
        {
            UntypedConfigMap.Data[DataPropertyName] = NeonHelper.JsonSerialize(Data);
        }
    }
}
