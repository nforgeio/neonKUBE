//-----------------------------------------------------------------------------
// FILE:        KubernetesExtensions.TypedConfigmap.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Threading;

using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Retry;
using Neon.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using k8s;
using k8s.Autorest;
using k8s.KubeConfigModels;
using k8s.Models;

namespace Neon.Kube
{
    public static partial class KubernetesExtensions
    {
        //---------------------------------------------------------------------
        // Namespaced typed configmap extensions.

        /// <summary>
        /// Creates a namespace scoped typed configmap.
        /// </summary>
        /// <typeparam name="TConfigMapData">Specifies the configmap data type.</typeparam>
        /// <param name="k8sCoreV1">The <see cref="Kubernetes"/> client's <see cref="ICoreV1Operations"/>.</param>
        /// <param name="typedConfigMap">Specifies the typed configmap.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The new <see cref="TypedConfigMap{TConfigMap}"/>.</returns>
        /// <remarks>
        /// Typed configmaps are <see cref="V1ConfigMap"/> objects that wrap a strongly typed
        /// configmap formatted using the <see cref="TypedConfigMap{TConfigMap}"/> class.  This
        /// makes it easy to persist and retrieve typed data to a Kubernetes cluster.
        /// </remarks>
        public static async Task<TypedConfigMap<TConfigMapData>> CreateNamespacedTypedConfigMapAsync<TConfigMapData>(
            this ICoreV1Operations          k8sCoreV1,
            TypedConfigMap<TConfigMapData>  typedConfigMap,
            CancellationToken               cancellationToken = default)

            where TConfigMapData: class, new()
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(typedConfigMap != null, nameof(typedConfigMap));

            return TypedConfigMap<TConfigMapData>.From(
                await k8sCoreV1.CreateNamespacedConfigMapAsync(
                    body:               typedConfigMap.UntypedConfigMap, 
                    namespaceParameter: typedConfigMap.UntypedConfigMap.Namespace(), 
                    cancellationToken:  cancellationToken));
        }

        /// <summary>
        /// Retrieves a namespace scoped typed configmap.
        /// </summary>
        /// <typeparam name="TConfigMapData">Specifies the configmap data type.</typeparam>
        /// <param name="k8sCoreV1">The <see cref="Kubernetes"/> client's <see cref="ICoreV1Operations"/>.</param>
        /// <param name="name">Specifies the object name.</param>
        /// <param name="namespaceParameter">The target Kubernetes namespace.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The retrieved <see cref="TypedConfigMap{TConfigMap}"/>.</returns>
        /// <remarks>
        /// Typed configmaps are <see cref="V1ConfigMap"/> objects that wrap a strongly typed
        /// object formatted using the <see cref="TypedConfigMap{TConfigMap}"/> class.  This
        /// makes it easy to persist and retrieve typed data to a Kubernetes cluster.
        /// </remarks>
        public static async Task<TypedConfigMap<TConfigMapData>> ReadNamespacedTypedConfigMapAsync<TConfigMapData>(
            this ICoreV1Operations      k8sCoreV1,
            string                      name,
            string                      namespaceParameter,
            CancellationToken           cancellationToken = default)

            where TConfigMapData : class, new()
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(namespaceParameter), nameof(namespaceParameter));

            return TypedConfigMap<TConfigMapData>.From(await k8sCoreV1.ReadNamespacedConfigMapAsync(name, namespaceParameter, pretty: false, cancellationToken: cancellationToken));
        }

        /// <summary>
        /// Replaces an existing typed configmap.
        /// </summary>
        /// <typeparam name="TConfigMapData">Specifies the configmap data type.</typeparam>
        /// <param name="k8sCoreV1">The <see cref="Kubernetes"/> client's <see cref="ICoreV1Operations"/>.</param>
        /// <param name="configmap">Specifies the replacement configmap data.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The updated <see cref="TypedConfigMap{TConfigMap}"/>.</returns>
        /// <remarks>
        /// <note>
        /// This method calls <see cref="TypedConfigMap{TConfigMapData}.Update()"/> to ensure that
        /// the untyped configmap data is up-to-date before persisting the changes.
        /// </note>
        /// <para>
        /// Typed configmaps are <see cref="V1ConfigMap"/> objects that wrap a strongly typed
        /// object formatted using the <see cref="TypedConfigMap{TConfigMap}"/> class.  This
        /// makes it easy to persist and retrieve typed data to a Kubernetes cluster.
        /// </para>
        /// </remarks>
        public static async Task<TypedConfigMap<TConfigMapData>> ReplaceNamespacedTypedConfigMapAsync<TConfigMapData>(
            this ICoreV1Operations          k8sCoreV1,
            TypedConfigMap<TConfigMapData>  configmap,
            CancellationToken               cancellationToken = default)

            where TConfigMapData : class, new()
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(configmap != null, nameof(configmap));

            configmap.Update();

            return TypedConfigMap<TConfigMapData>.From(await k8sCoreV1.ReplaceNamespacedConfigMapAsync(
                body:               configmap.UntypedConfigMap, 
                name:               configmap.UntypedConfigMap.Name(), 
                namespaceParameter: configmap.UntypedConfigMap.Namespace(), 
                cancellationToken:  cancellationToken));
        }

        /// <summary>
        /// Replaces an existing typed configmap with new data.
        /// </summary>
        /// <typeparam name="TConfigMapData">Specifies the configmap data type.</typeparam>
        /// <param name="k8sCoreV1">The <see cref="Kubernetes"/> client's <see cref="ICoreV1Operations"/>.</param>
        /// <param name="data">Specifies the replacement configmap data.</param>
        /// <param name="name">Specifies the object name.</param>
        /// <param name="namespaceParameter">The target Kubernetes namespace.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The updated <see cref="TypedConfigMap{TConfigMap}"/>.</returns>
        /// <remarks>
        /// Typed configmaps are <see cref="V1ConfigMap"/> objects that wrap a strongly typed
        /// object formatted using the <see cref="TypedConfigMap{TConfigMap}"/> class.  This
        /// makes it easy to persist and retrieve typed data to a Kubernetes cluster.
        /// </remarks>
        public static async Task<TypedConfigMap<TConfigMapData>> ReplaceNamespacedTypedConfigMapAsync<TConfigMapData>(
            this ICoreV1Operations      k8sCoreV1,
            TConfigMapData              data,
            string                      name,
            string                      namespaceParameter,
            CancellationToken           cancellationToken = default)

            where TConfigMapData : class, new()
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(data != null, nameof(data));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(namespaceParameter), nameof(namespaceParameter));

            var configmap = new TypedConfigMap<TConfigMapData>(name, namespaceParameter, data);

            return TypedConfigMap<TConfigMapData>.From(await k8sCoreV1.ReplaceNamespacedConfigMapAsync(configmap.UntypedConfigMap, name, namespaceParameter, cancellationToken: cancellationToken));
        }

        /// <summary>
        /// Deletes a namespaced typed configmap.
        /// </summary>
        /// <param name="k8sCoreV1">The <see cref="Kubernetes"/> client's <see cref="ICoreV1Operations"/>.</param>
        /// <param name="name">Specifies the object name.</param>
        /// <param name="namespaceParameter">The target Kubernetes namespace.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// Typed configmaps are <see cref="V1ConfigMap"/> objects that wrap a strongly typed
        /// object formatted using the <see cref="TypedConfigMap{TConfigMap}"/> class.  This
        /// makes it easy to persist and retrieve typed data to a Kubernetes cluster.
        /// </remarks>
        public static async Task DeleteNamespacedTypedConfigMapAsync(
            this ICoreV1Operations  k8sCoreV1,
            string                  name,
            string                  namespaceParameter,
            CancellationToken       cancellationToken = default)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(namespaceParameter), nameof(namespaceParameter));

            await k8sCoreV1.DeleteNamespacedConfigMapAsync(name, namespaceParameter, cancellationToken: cancellationToken);
        }
    }
}
