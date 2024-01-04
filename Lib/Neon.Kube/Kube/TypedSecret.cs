// -----------------------------------------------------------------------------
// FILE:        TypedSecret.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
    /// Extends a Kubernetes <see cref="V1Secret"/> to support strongly typed secrets.
    /// This is implemented by serializing the secret data as JSON and adding that to the 
    /// low-level secret under the <see cref="DataPropertyName"/> key.
    /// </summary>
    /// <typeparam name="TSecretData">Specifies the secret data type.</typeparam>
    /// <remarks>
    /// <para>
    /// To create a secret, use the <see cref="TypedSecret(string, string, TSecretData)"/>
    /// constructor, specifying the secret's Kubernetes name and namespace as well as an
    /// instance of the typesafe secret; your typed secret will be available as the <see cref="Data"/>
    /// property.  Configure your secret as required and then call <b>IKubernetes.CreateNamespacedTypedSecretAsync()</b>,
    /// passing <see cref="UntypedSecret"/> as the request body (this holds the <see cref="V1Secret"/>).
    /// </para>
    /// <para>
    /// To read an existing secret, call <b>IKubernetes.CoreV1.ReadNamespacedTypedSecretAsync()</b> 
    /// to retrieve the Kubernetes secret and then call the static <see cref="From"/> method to wrap
    /// the result into a <see cref="TypedSecret{TSecretData}"/> where your typesafe values can be accessed
    /// via the <see cref="Data"/> property.
    /// </para>
    /// <para>
    /// To update an existing secret, call <b>IKubernetes.CoreV1.ReadNamespacedTypedSecretAsync</b> to retrieve it, 
    /// modify it via the <see cref="Data"/> property and then call <b>IKubernetes.CoreV1.ReplaceNamespacedTypedSecretAsync()</b>
    /// passing the new <see cref="UntypedSecret"/>.
    /// </para>
    /// </remarks>
    public class TypedSecret<TSecretData>
        where TSecretData : class, new()
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// <para>
        /// Identifies the key used to store typed data within an untyped secret.
        /// </para>
        /// <note>
        /// <b>WARNING:</b> DO NOT MODIFY!  Any change will break existing clusters.
        /// </note>
        /// </summary>
        public const string DataPropertyName = "typed-data";

        /// <summary>
        /// Constructs an instance by parsing a <see cref="V1Secret"/>.
        /// </summary>
        /// <param name="untypedSecret">Specifies the untyped secret.</param>
        /// <returns>The typed secret.</returns>
        public static TypedSecret<TSecretData> From(V1Secret untypedSecret)
        {
            Covenant.Requires<ArgumentNullException>(untypedSecret != null, nameof(untypedSecret));

            return new TypedSecret<TSecretData>(untypedSecret);
        }

        //---------------------------------------------------------------------
        // Instance members

        private TSecretData data;

        /// <summary>
        /// Constructs an instance from an untyped <see cref="V1Secret"/>.
        /// </summary>
        /// <param name="untypedSecret">The secret name as it will be persisted to Kubernetes.</param>
        public TypedSecret(V1Secret untypedSecret)
        {
            Covenant.Requires<ArgumentNullException>(untypedSecret != null, nameof(untypedSecret));

            if (!untypedSecret.Data.TryGetValue(DataPropertyName, out var json))
            {
                throw new InvalidDataException($"Expected the [{untypedSecret}] to have a [{DataPropertyName}] property.");
            }

            UntypedSecret = untypedSecret;
            Data          = NeonHelper.JsonDeserialize<TSecretData>(json, strict: true);
        }

        /// <summary>
        /// Constructs a typed secret with the specified name and an optional initial value
        /// <typeparamref name="TSecretData"/> value.
        /// </summary>
        /// <param name="name">Specifies the secret name.</param>
        /// <param name="namespace">specifies the namespace.</param>
        /// <param name="data">
        /// Optionally specifies the secret data.  A default instance will be created
        /// when this is <c>null</c>.
        /// </param>
        public TypedSecret(string name, string @namespace, TSecretData data = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(@namespace), nameof(@namespace));
            Covenant.Requires<ArgumentNullException>(data != null, nameof(data));

            Data                                     = data ?? new TSecretData();
            UntypedSecret                            = KubeHelper.CreateKubeObject<V1Secret>(name);
            UntypedSecret.Metadata.NamespaceProperty = @namespace;
            UntypedSecret.Data                       = new Dictionary<string, byte[]>();
            UntypedSecret.Data[DataPropertyName]     = Encoding.UTF8.GetBytes(NeonHelper.JsonSerialize(Data));
        }

        /// <summary>
        /// Returns the associated untyped secret.
        /// </summary>
        public V1Secret UntypedSecret { get; private set; }

        /// <summary>
        /// Specifies the current typed secret data.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the value being set is <c>null</c>.</exception>
        public TSecretData Data
        {
            get => data;

            set
            {
                Covenant.Requires<ArgumentNullException>(value != null, nameof(Data));

                data = value;
            }
        }

        /// <summary>
        /// Updates the secret by persisting any changes to <see cref="Data"/> back to
        /// the Kubernetes secret's <see cref="DataPropertyName"/> key.
        /// </summary>
        public void Update()
        {
            UntypedSecret.Data[DataPropertyName] = Encoding.UTF8.GetBytes(NeonHelper.JsonSerialize(Data));
        }
    }
}
