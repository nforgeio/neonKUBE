//-----------------------------------------------------------------------------
// FILE:        KubernetesExtensions.TypedSecret.cs
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
        // Namespaced typed secret extensions.

        /// <summary>
        /// Creates a namespace scoped typed secret.
        /// </summary>
        /// <typeparam name="TSecretData">Specifies the secret data type.</typeparam>
        /// <param name="k8sCoreV1">The <see cref="Kubernetes"/> client's <see cref="ICoreV1Operations"/>.</param>
        /// <param name="typedSecret">Specifies the typed secret.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The new <see cref="TypedSecret{TSecret}"/>.</returns>
        /// <remarks>
        /// Typed secrets are <see cref="V1Secret"/> objects that wrap a strongly typed
        /// object formatted using the <see cref="TypedSecret{TSecretData}"/> class.  This
        /// makes it easy to persist and retrieve typed data to a Kubernetes cluster.
        /// </remarks>
        public static async Task<TypedSecret<TSecretData>> CreateNamespacedTypedSecretAsync<TSecretData>(
            this ICoreV1Operations      k8sCoreV1,
            TypedSecret<TSecretData>    typedSecret,
            CancellationToken           cancellationToken = default)

            where TSecretData: class, new()
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(typedSecret != null, nameof(typedSecret));

            return TypedSecret<TSecretData>.From(
                await k8sCoreV1.CreateNamespacedSecretAsync(
                    body:               typedSecret.UntypedSecret, 
                    namespaceParameter: typedSecret.UntypedSecret.Namespace(), 
                    cancellationToken:  cancellationToken));
        }

        /// <summary>
        /// Retrieves a namespace scoped typed secret.
        /// </summary>
        /// <typeparam name="TSecretData">Specifies the secret data type.</typeparam>
        /// <param name="k8sCoreV1">The <see cref="Kubernetes"/> client's <see cref="ICoreV1Operations"/>.</param>
        /// <param name="name">Specifies the object name.</param>
        /// <param name="namespaceParameter">The target Kubernetes namespace.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The retrieved <see cref="TypedSecret{TSecretData}"/>.</returns>
        /// <remarks>
        /// Typed secrets are <see cref="V1Secret"/> objects that wrap a strongly typed
        /// object formatted using the <see cref="TypedSecret{TSecretData}"/> class.  This
        /// makes it easy to persist and retrieve typed data to a Kubernetes cluster.
        /// </remarks>
        public static async Task<TypedSecret<TSecretData>> ReadNamespacedTypedSecretAsync<TSecretData>(
            this ICoreV1Operations      k8sCoreV1,
            string                      name,
            string                      namespaceParameter,
            CancellationToken           cancellationToken = default)

            where TSecretData : class, new()
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(namespaceParameter), nameof(namespaceParameter));

            return TypedSecret<TSecretData>.From(await k8sCoreV1.ReadNamespacedSecretAsync(name, namespaceParameter, pretty: false, cancellationToken: cancellationToken));
        }

        /// <summary>
        /// Replaces an existing typed secret.
        /// </summary>
        /// <typeparam name="TSecretData">Specifies the secret data type.</typeparam>
        /// <param name="k8sCoreV1">The <see cref="Kubernetes"/> client's <see cref="ICoreV1Operations"/>.</param>
        /// <param name="secret">Specifies the replacement secret data.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The updated <see cref="TypedSecret{TSecretData}"/>.</returns>
        /// <remarks>
        /// <note>
        /// This method calls <see cref="TypedSecret{TSecretData}.Update()"/> to ensure that
        /// the untyped secret data is up-to-date before persisting the changes.
        /// </note>
        /// <para>
        /// Typed secret are <see cref="V1Secret"/> objects that wrap a strongly typed
        /// object formatted using the <see cref="TypedSecret{TSecretData}"/> class.  This
        /// makes it easy to persist and retrieve typed data to a Kubernetes cluster.
        /// </para>
        /// </remarks>
        public static async Task<TypedSecret<TSecretData>> ReplaceNamespacedTypedSecretAsync<TSecretData>(
            this ICoreV1Operations      k8sCoreV1,
            TypedSecret<TSecretData>    secret,
            CancellationToken           cancellationToken = default)

            where TSecretData : class, new()
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(secret != null, nameof(secret));

            secret.Update();

            return TypedSecret<TSecretData>.From(await k8sCoreV1.ReplaceNamespacedSecretAsync(
                body:               secret.UntypedSecret, 
                name:               secret.UntypedSecret.Name(), 
                namespaceParameter: secret.UntypedSecret.Namespace(), 
                cancellationToken:  cancellationToken));
        }

        /// <summary>
        /// Replaces an existing typed secret with new data.
        /// </summary>
        /// <typeparam name="TSecretData">Specifies the secret data type.</typeparam>
        /// <param name="k8sCoreV1">The <see cref="Kubernetes"/> client's <see cref="ICoreV1Operations"/>.</param>
        /// <param name="data">Specifies the replacement secret data.</param>
        /// <param name="name">Specifies the object name.</param>
        /// <param name="namespaceParameter">The target Kubernetes namespace.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The updated <see cref="TypedSecret{TSecretData}"/>.</returns>
        /// <remarks>
        /// Typed secrets are <see cref="V1Secret"/> objects that wrap a strongly typed
        /// object formatted using the <see cref="TypedSecret{TSecretData}"/> class.  This
        /// makes it easy to persist and retrieve typed data to a Kubernetes cluster.
        /// </remarks>
        public static async Task<TypedSecret<TSecretData>> ReplaceNamespacedTypedSecretAsync<TSecretData>(
            this ICoreV1Operations      k8sCoreV1,
            TSecretData                 data,
            string                      name,
            string                      namespaceParameter,
            CancellationToken           cancellationToken = default)

            where TSecretData : class, new()
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(data != null, nameof(data));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(namespaceParameter), nameof(namespaceParameter));

            var secret = new TypedSecret<TSecretData>(name, namespaceParameter, data);

            return TypedSecret<TSecretData>.From(await k8sCoreV1.ReplaceNamespacedSecretAsync(secret.UntypedSecret, name, namespaceParameter, cancellationToken: cancellationToken));
        }

        /// <summary>
        /// Deletes a namespaced typed secret.
        /// </summary>
        /// <param name="k8sCoreV1">The <see cref="Kubernetes"/> client's <see cref="ICoreV1Operations"/>.</param>
        /// <param name="name">Specifies the object name.</param>
        /// <param name="namespaceParameter">The target Kubernetes namespace.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// Typed secrets are <see cref="V1Secret"/> objects that wrap a strongly typed
        /// object formatted using the <see cref="TypedSecret{TSecretData}"/> class.  This
        /// makes it easy to persist and retrieve typed data to a Kubernetes cluster.
        /// </remarks>
        public static async Task DeleteNamespacedTypedSecretAsync(
            this ICoreV1Operations k8sCoreV1,
            string                 name,
            string                 namespaceParameter,
            CancellationToken      cancellationToken = default)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(namespaceParameter), nameof(namespaceParameter));

            await k8sCoreV1.DeleteNamespacedSecretAsync(name, namespaceParameter, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Upserts a namespaced typed secret.
        /// </summary>
        /// <param name="k8sCoreV1">The <see cref="Kubernetes"/> client's <see cref="ICoreV1Operations"/>.</param>
        /// <param name="secret">Specifies the secret.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// Typed secrets are <see cref="V1Secret"/> objects that wrap a strongly typed
        /// object formatted using the <see cref="TypedSecret{TSecretData}"/> class.  This
        /// makes it easy to persist and retrieve typed data to a Kubernetes cluster.
        /// </remarks>
        public static async Task<TypedSecret<TSecretData>> UpsertNamespacedTypedSecretAsync<TSecretData>(
            this ICoreV1Operations   k8sCoreV1,
            TypedSecret<TSecretData> secret,
            CancellationToken        cancellationToken = default)
            where TSecretData : class, new()
        {
            await SyncContext.Clear;

            Covenant.Requires<ArgumentNullException>(secret != null, nameof(secret));

            try
            {
                await k8sCoreV1.ReadNamespacedSecretAsync(secret.UntypedSecret.Name(), secret.UntypedSecret.Namespace(), cancellationToken: cancellationToken);
            }
            catch (HttpOperationException e)
            {
                if (e.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    return await k8sCoreV1.CreateNamespacedTypedSecretAsync(secret, cancellationToken);
                }
                else
                {
                    throw;
                }
            }

            return await k8sCoreV1.ReplaceNamespacedTypedSecretAsync(secret, cancellationToken);
        }
    }
}
