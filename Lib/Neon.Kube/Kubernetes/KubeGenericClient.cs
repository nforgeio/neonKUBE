//-----------------------------------------------------------------------------
// FILE:	    KubeGenericClient.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;

using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Rest;

using Neon.Common;
using Neon.Retry;
using Neon.Tasks;

using k8s;
using k8s.Models;

namespace Neon.Kube
{
    /// <summary>
    /// Wraps the official <see cref="GenericClient"/> to simplify usage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The model for using the official <see cref="GenericClient"/> seems somewhat ackward and
    /// potentially dangerous.  This requires that developers create a <see cref="GenericClient"/>
    /// instance for each custom resource type, passing the object's <b>group</b>, <b>version</b>
    /// and <b>plural</b> name values.  Then developers will call methods like <see cref="GenericClient.CreateAsync{T}(T, CancellationToken)"/>,
    /// passing the custom object instance.
    /// </para>
    /// <para>
    /// The problem with this is that the class methods don't actually confirm that the type of
    /// the object passed is actually consistent with the constructor attributes.  This combined
    /// with needing separate <see cref="GenericClient"/> instances for every time, makes me worry
    /// about developers mismatching clients and objects.
    /// </para>
    /// <para>
    /// Managing multiple clients is also going to be a hassle and it sure would be nice if the 
    /// <b>group</b>, <b>version</b> and <b>plural</b> type attributes could be handled automatically.
    /// </para>
    /// <para>
    /// The <see cref="KubeGenericClient"/> class attempts to address these issues at the cost of
    /// a bit of performance.  This class wraps an <see cref="IKubernetes"/> client and manages 
    /// multiple <see cref="GenericClient"/> instances internally.  This also uses reflection
    /// to retrieve the type's <see cref="KubernetesEntityAttribute"/> holding the objects <b>group</b>,
    /// <b>version</b>, <b>plural name</b>, and <b>kind</b> properties.
    /// </para>
    /// <note>
    /// <para>
    /// This class maintains a cache of <see cref="GenericClient"/> instances for the types passed
    /// to the class methods so you don't have to.  The performance cost should be acceptable for
    /// many scenarios:
    /// </para>
    /// <list type="bullet">
    /// <item>We need to reflect each object type once to load the attributes.</item>
    /// <item>Each subesquent call will need to lookup the correct client, which requires a lock.</item>
    /// </list>
    /// </note>
    /// </remarks>
    public class KubeGenericClient
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Holds information about a tracked <see cref="GenericClient"/> instance.
        /// </summary>
        private struct ClientProxy
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="k8s">The underlying Kubernetes client.</param>
            /// <param name="group">
            /// <para>
            /// The type's group.
            /// </para>
            /// <note>
            /// Empty strings are allowed here because core Kubernetes objects
            /// use this.
            /// </note>
            /// </param>
            /// <param name="version">The type's version.</param>
            /// <param name="kind">The type's kind.</param>
            /// <param name="plural">The type's plural name.</param>
            public ClientProxy(IKubernetes k8s, string group, string version, string kind, string plural)
            {
                Covenant.Requires<ArgumentNullException>(group != null, nameof(group));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(version), nameof(version));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(plural), nameof(plural));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(kind), nameof(kind));

                this.Client  = new GenericClient(k8s, group: group, version: version, plural: plural);
                this.Group   = group;
                this.Version = version;
                this.Kind    = kind;
                this.Plural  = plural;
            }

            /// <summary>
            /// Returns the associated client.
            /// </summary>
            public readonly GenericClient Client;

            /// <summary>
            /// Returns the associated type's group.
            /// </summary>
            public readonly string Group;

            /// <summary>
            /// Returns the associated type's version.
            /// </summary>
            public readonly string Version;

            /// <summary>
            /// Returns the associated types kind.
            /// </summary>
            public readonly string Kind;

            /// <summary>
            /// Returns the associated type's plural name.
            /// </summary>
            public readonly string Plural;
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The Kubernetes API appears to want the <b>default</b> namespace to be specified
        /// as an empty string.  This method converts namespaces passed as <b>default</b> or
        /// <c>null</c> to an empty string.
        /// </summary>
        /// <param name="namespace">The input namespace.</param>
        /// <returns>The normalized namespace.</returns>
        private static string NormalizeNamespace(string @namespace)
        {
            if (@namespace == null || @namespace == "default")
            {
                return string.Empty;
            }
            else
            {
                return @namespace;
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private object          syncLock = new object();
        private IKubernetes     k8s;

        /// <summary>
        /// Maps object types to the <see cref="GenericClient"/> use use to manage the type.
        /// </summary>
        private Dictionary<Type, ClientProxy> typeToProxy;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="k8s">The underlying Kubernetes client to be used.</param>
        public KubeGenericClient(IKubernetes k8s)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            this.k8s         = k8s;
            this.typeToProxy = new Dictionary<Type, ClientProxy>();
        }

        /// <summary>
        /// Returns the <see cref="GenericClient"/> instance to be used to manage custom
        /// resources of type <typeparamref name="T"/>, creating a new client when doesn't
        /// already exist..
        /// </summary>
        /// <typeparam name="T">The custom object type.</typeparam>
        /// <returns>The <see cref="GenericClient"/> for the type.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when <typeparamref name="T"/> is missing any of the <b>KubeGroup</b>,
        /// <b>KubeVersion</b>, or <b>KubePlural</b> constants.
        /// </exception>
        private GenericClient GetClient<T>()
        {
            var type = typeof(T);

            // First try retrieving an existing client.  This will be the common case.

            lock (syncLock)
            {
                if (typeToProxy.TryGetValue(type, out var proxy))
                {
                    return proxy.Client;
                }
            }

            // Reflect the type to obtain the custom object attributes.

            var attributes = type.GetCustomAttribute<KubernetesEntityAttribute>();

            if (attributes == null)
            {
                throw new NotSupportedException($"Type [{type.FullName}]: missing the [{nameof(KubernetesEntityAttribute)}].");
            }

            // Construct the new client and associate it with the type.

            var newProxy = new ClientProxy(k8s, attributes.Group, version: attributes.ApiVersion, kind: attributes.Kind, plural: attributes.PluralName);

            lock (syncLock)
            {
                typeToProxy.Add(type, newProxy);
            }

            return newProxy.Client;
        }

        /// <summary>
        /// Creates a cluster scoped resource.
        /// </summary>
        /// <typeparam name="T">Specifies the custom object type.</typeparam>
        /// <param name="obj">The object.</param>
        /// <param name="name">Specifies the object name.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The new custom resource.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when <typeparamref name="T"/> is missing any of the <b>KubeGroup</b>,
        /// <b>KubeVersion</b>, or <b>KubePlural</b> constants.
        /// </exception>
        public async Task<T> CreateAsync<T>(T obj, string name, CancellationToken cancellationToken = default)
            where T : IKubernetesObject, IMetadata<V1ObjectMeta>
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(obj != null, nameof(obj));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            obj.Metadata.Name = name;

            return await GetClient<T>().CreateAsync(obj, cancellationToken);
        }

        /// <summary>
        /// Creates a namespace scoped resource.
        /// </summary>
        /// <typeparam name="T">Specifies the custom object type.</typeparam>
        /// <param name="obj">The object.</param>
        /// <param name="namespaceParameter">Specifies the target namespace.</param>
        /// <param name="name">Specifies the object name.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The new custom resource.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when <typeparamref name="T"/> is missing any of the <b>KubeGroup</b>,
        /// <b>KubeVersion</b>, or <b>KubePlural</b> constants.
        /// </exception>
        public async Task<T> CreateNamespacedAsync<T>(T obj, string namespaceParameter, string name, CancellationToken cancellationToken = default)
            where T : IKubernetesObject, IMetadata<V1ObjectMeta>
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(obj != null, nameof(obj));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            obj.Metadata.Name = name;

            return await GetClient<T>().CreateNamespacedAsync(obj, NormalizeNamespace(namespaceParameter), cancellationToken);
        }

        /// <summary>
        /// Lists cluster scoped resources.
        /// </summary>
        /// <typeparam name="T">Specifies the custom object type.</typeparam>
        /// <param name="cancellationToken"></param>
        /// <returns>The resource list.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when <typeparamref name="T"/> is missing any of the <b>KubeGroup</b>,
        /// <b>KubeVersion</b>, or <b>KubePlural</b> constants.
        /// </exception>
        public async Task<V1CustomObjectList<T>> ListAsync<T>(CancellationToken cancellationToken = default)
            where T : IKubernetesObject, IMetadata<V1ObjectMeta>
        {
            await SyncContext.Clear;

            return await GetClient<T>().ListAsync<V1CustomObjectList<T>>(cancellationToken);
        }
        
        /// <summary>
        /// Lists namespace scoped resources.
        /// </summary>
        /// <typeparam name="T">Specifies the custom object type.</typeparam>
        /// <param name="namespaceParameter">Specifies the target namespace.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The resource list.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when <typeparamref name="T"/> is missing any of the <b>KubeGroup</b>,
        /// <b>KubeVersion</b>, or <b>KubePlural</b> constants.
        /// </exception>
        public async Task<V1CustomObjectList<T>> ListNamespacedAsync<T>(string namespaceParameter, CancellationToken cancellationToken = default)
            where T : IKubernetesObject, IMetadata<V1ObjectMeta>
        {
            await SyncContext.Clear;

            return await GetClient<T>().ListNamespacedAsync<V1CustomObjectList<T>>(NormalizeNamespace(namespaceParameter), cancellationToken);
        }

        /// <summary>
        /// Retrieves a cluster scoped resource.
        /// </summary>
        /// <typeparam name="T">Specifies the custom object type.</typeparam>
        /// <param name="name"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The custom resource.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when <typeparamref name="T"/> is missing any of the <b>KubeGroup</b>,
        /// <b>KubeVersion</b>, or <b>KubePlural</b> constants.
        /// </exception>
        public async Task<T> ReadAsync<T>(string name, CancellationToken cancellationToken = default)
            where T : IKubernetesObject
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            return await GetClient<T>().ReadAsync<T>(name, cancellationToken);
        }

        /// <summary>
        /// Retrieves a namespace scoped resource.
        /// </summary>
        /// <typeparam name="T">Specifies the custom object type.</typeparam>
        /// <param name="namespaceParameter">Specifies the target namespace.</param>
        /// <param name="name"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The custom resource.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when <typeparamref name="T"/> is missing any of the <b>KubeGroup</b>,
        /// <b>KubeVersion</b>, or <b>KubePlural</b> constants.
        /// </exception>
        public async Task<T> ReadNamespacedAsync<T>(string namespaceParameter, string name, CancellationToken cancellationToken = default)
            where T : IKubernetesObject
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            return await GetClient<T>().ReadNamespacedAsync<T>(NormalizeNamespace(namespaceParameter), name, cancellationToken);
        }

        /// <summary>
        /// Deletes a cluster scoped resource.
        /// </summary>
        /// <typeparam name="T">Specifies the custom object type.</typeparam>
        /// <param name="name"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when <typeparamref name="T"/> is missing any of the <b>KubeGroup</b>,
        /// <b>KubeVersion</b>, or <b>KubePlural</b> constants.
        /// </exception>
        public async Task DeleteAsync<T>(string name, CancellationToken cancellationToken = default)
            where T : IKubernetesObject
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            try
            {
                await GetClient<T>().DeleteAsync<T>(name, cancellationToken);
            }
            catch (JsonException)
            {
                // $todo(jefflill): I'm not sure why we're seeing these.
                //
                //      https://github.com/nforgeio/neonKUBE/issues/1586
            }
        }

        /// <summary>
        /// Deletes a namespace scoped resource.
        /// </summary>
        /// <typeparam name="T">Specifies the custom object type.</typeparam>
        /// <param name="namespaceParameter">Specifies the target namespace.</param>
        /// <param name="name"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when <typeparamref name="T"/> is missing any of the <b>KubeGroup</b>,
        /// <b>KubeVersion</b>, or <b>KubePlural</b> constants.
        /// </exception>
        public async Task DeleteNamespacedAsync<T>(string namespaceParameter, string name, CancellationToken cancellationToken = default)
            where T : IKubernetesObject
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            try
            {
                await GetClient<T>().DeleteNamespacedAsync<T>(NormalizeNamespace(namespaceParameter), name, cancellationToken);
            }
            catch (JsonException)
            {
                // $todo(jefflill): I'm not sure why we're seeing these.
                //
                //      https://github.com/nforgeio/neonKUBE/issues/1586
            }
        }
    }
}
