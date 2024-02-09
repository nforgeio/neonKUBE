//-----------------------------------------------------------------------------
// FILE:        KubernetesExtensions.ClusterCustomObject.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;

using Neon.Common;
using Neon.Tasks;

using k8s;
using k8s.Autorest;
using k8s.Models;

namespace Neon.Kube.K8s
{
    public static partial class KubernetesExtensions
    {
        //---------------------------------------------------------------------
        // Generic cluster-scoped generic custom object extensions:

        /// <summary>
        /// List or watch cluster scoped custom objects, deserializing them into the specified
        /// generic type.
        /// </summary>
        /// <typeparam name="T">The custom object list type.</typeparam>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="allowWatchBookmarks">
        /// allowWatchBookmarks requests watch events with type "BOOKMARK". Servers that
        /// do not implement bookmarks may ignore this flag and bookmarks are sent at the
        /// server's discretion. Clients should not assume bookmarks are returned at any
        /// specific interval, nor may they assume the server will send any BOOKMARK event
        /// during a session. If this is not a watch, this field is ignored. If the feature
        /// gate WatchBookmarks is not enabled in apiserver, this field is ignored.
        /// </param>
        /// <param name="continueParameter">
        /// The continue option should be set when retrieving more results from the server.
        /// Since this value is server defined, clients may only use the continue value from
        /// a previous query result with identical query parameters (except for the value
        /// of continue) and the server may reject a continue value it does not recognize.
        /// If the specified continue value is no longer valid whether due to expiration
        /// (generally five to fifteen minutes) or a configuration change on the server,
        /// the server will respond with a 410 ResourceExpired error together with a continue
        /// token. If the client needs a consistent list, it must restart their list without
        /// the continue field. Otherwise, the client may send another list request with
        /// the token received with the 410 error, the server will respond with a list starting
        /// from the next key, but from the latest snapshot, which is inconsistent from the
        /// previous list results - objects that are created, modified, or deleted after
        /// the first list request will be included in the response, as long as their keys
        /// are after the "next key". This field is not supported when watch is true. Clients
        /// may start a watch from the last resourceVersion value returned by the server
        /// and not miss any modifications.
        /// </param>
        /// <param name="fieldSelector">
        /// A selector to restrict the list of returned objects by their fields. Defaults
        /// to everything.
        /// </param>
        /// <param name="labelSelector">
        /// A selector to restrict the list of returned objects by their labels. Defaults
        /// to everything.
        /// </param>
        /// <param name="limit">
        /// limit is a maximum number of responses to return for a list call. If more items
        /// exist, the server will set the `continue` field on the list metadata to a value
        /// that can be used with the same initial query to retrieve the next set of results.
        /// Setting a limit may return fewer than the requested amount of items (up to zero
        /// items) in the event all requested objects are filtered out and clients should
        /// only use the presence of the continue field to determine whether more results
        /// are available. Servers may choose not to support the limit argument and will
        /// return all of the available results. If limit is specified and the continue field
        /// is empty, clients may assume that no more results are available. This field is
        /// not supported if watch is true. The server guarantees that the objects returned
        /// when using continue will be identical to issuing a single list call without a
        /// limit - that is, no objects created, modified, or deleted after the first request
        /// is issued will be included in any subsequent continued requests. This is sometimes
        /// referred to as a consistent snapshot, and ensures that a client that is using
        /// limit to receive smaller chunks of a very large result can ensure they see all
        /// possible objects. If objects are updated during a chunked list the version of
        /// the object that was present at the time the first list result was calculated
        /// is returned.
        /// </param>
        /// <param name="resourceVersion">
        /// When specified with a watch call, shows changes that occur after that particular
        /// version of a resource. Defaults to changes from the beginning of history. When
        /// specified for list: - if unset, then the result is returned from remote storage
        /// based on quorum-read flag; - if it's 0, then we simply return what we currently
        /// have in cache, no guarantee; - if set to non zero, then the result is at least
        /// as fresh as given rv.
        /// </param>
        /// <param name="resourceVersionMatch">
        /// resourceVersionMatch determines how resourceVersion is applied to list calls.
        /// It is highly recommended that resourceVersionMatch be set for list calls where
        /// resourceVersion is set See https://kubernetes.io/docs/reference/using-api/api-concepts/#resource-versions
        /// for details. Defaults to unset
        /// </param>
        /// <param name="timeoutSeconds">
        /// Timeout for the list/watch call. This limits the duration of the call, regardless
        /// of any activity or inactivity.
        /// </param>
        /// <param name="watch">
        /// Watch for changes to the described resources and return them as a stream of add,
        /// update, and remove notifications.
        /// </param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The deserialized object list.</returns>
        public static async Task<V1CustomObjectList<T>> ListClusterCustomObjectAsync<T>(
            this ICustomObjectsOperations   k8s,
            bool?                           allowWatchBookmarks  = null,
            string                          continueParameter    = null,
            string                          fieldSelector        = null,
            string                          labelSelector        = null,
            int?                            limit                = null,
            string                          resourceVersion      = null,
            string                          resourceVersionMatch = null,
            int?                            timeoutSeconds       = null,
            bool?                           watch                = null,
            CancellationToken               cancellationToken    = default)

            where T : IKubernetesObject<V1ObjectMeta>, new()
        {
            await SyncContext.Clear;

            var typeMetadata = typeof(T).GetKubernetesTypeMetadata();

            var result = await k8s.ListClusterCustomObjectAsync(
                group:                typeMetadata.Group,
                version:              typeMetadata.ApiVersion,
                plural:               typeMetadata.PluralName,
                allowWatchBookmarks:  allowWatchBookmarks,
                continueParameter:    continueParameter,
                fieldSelector:        fieldSelector,
                labelSelector:        labelSelector,
                limit:                limit,
                resourceVersion:      resourceVersion,
                resourceVersionMatch: resourceVersionMatch,
                timeoutSeconds:       timeoutSeconds,
                watch:                watch,
                pretty:               false,
                cancellationToken:    cancellationToken);

            return ((JsonElement)result).Deserialize<V1CustomObjectList<T>>(options: serializeOptions);
        }

        /// <summary>
        /// List or watch cluster scoped custom objects, deserializing them into the specified
        /// generic type.
        /// </summary>
        /// <typeparam name="T">The custom object list type.</typeparam>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="allowWatchBookmarks">
        /// allowWatchBookmarks requests watch events with type "BOOKMARK". Servers that
        /// do not implement bookmarks may ignore this flag and bookmarks are sent at the
        /// server's discretion. Clients should not assume bookmarks are returned at any
        /// specific interval, nor may they assume the server will send any BOOKMARK event
        /// during a session. If this is not a watch, this field is ignored. If the feature
        /// gate WatchBookmarks is not enabled in apiserver, this field is ignored.
        /// </param>
        /// <param name="continueParameter">
        /// The continue option should be set when retrieving more results from the server.
        /// Since this value is server defined, clients may only use the continue value from
        /// a previous query result with identical query parameters (except for the value
        /// of continue) and the server may reject a continue value it does not recognize.
        /// If the specified continue value is no longer valid whether due to expiration
        /// (generally five to fifteen minutes) or a configuration change on the server,
        /// the server will respond with a 410 ResourceExpired error together with a continue
        /// token. If the client needs a consistent list, it must restart their list without
        /// the continue field. Otherwise, the client may send another list request with
        /// the token received with the 410 error, the server will respond with a list starting
        /// from the next key, but from the latest snapshot, which is inconsistent from the
        /// previous list results - objects that are created, modified, or deleted after
        /// the first list request will be included in the response, as long as their keys
        /// are after the "next key". This field is not supported when watch is true. Clients
        /// may start a watch from the last resourceVersion value returned by the server
        /// and not miss any modifications.
        /// </param>
        /// <param name="fieldSelector">
        /// A selector to restrict the list of returned objects by their fields. Defaults
        /// to everything.
        /// </param>
        /// <param name="labelSelector">
        /// A selector to restrict the list of returned objects by their labels. Defaults
        /// to everything.
        /// </param>
        /// <param name="limit">
        /// limit is a maximum number of responses to return for a list call. If more items
        /// exist, the server will set the `continue` field on the list metadata to a value
        /// that can be used with the same initial query to retrieve the next set of results.
        /// Setting a limit may return fewer than the requested amount of items (up to zero
        /// items) in the event all requested objects are filtered out and clients should
        /// only use the presence of the continue field to determine whether more results
        /// are available. Servers may choose not to support the limit argument and will
        /// return all of the available results. If limit is specified and the continue field
        /// is empty, clients may assume that no more results are available. This field is
        /// not supported if watch is true. The server guarantees that the objects returned
        /// when using continue will be identical to issuing a single list call without a
        /// limit - that is, no objects created, modified, or deleted after the first request
        /// is issued will be included in any subsequent continued requests. This is sometimes
        /// referred to as a consistent snapshot, and ensures that a client that is using
        /// limit to receive smaller chunks of a very large result can ensure they see all
        /// possible objects. If objects are updated during a chunked list the version of
        /// the object that was present at the time the first list result was calculated
        /// is returned.
        /// </param>
        /// <param name="resourceVersion">
        /// When specified with a watch call, shows changes that occur after that particular
        /// version of a resource. Defaults to changes from the beginning of history. When
        /// specified for list: - if unset, then the result is returned from remote storage
        /// based on quorum-read flag; - if it's 0, then we simply return what we currently
        /// have in cache, no guarantee; - if set to non zero, then the result is at least
        /// as fresh as given rv.
        /// </param>
        /// <param name="resourceVersionMatch">
        /// resourceVersionMatch determines how resourceVersion is applied to list calls.
        /// It is highly recommended that resourceVersionMatch be set for list calls where
        /// resourceVersion is set See https://kubernetes.io/docs/reference/using-api/api-concepts/#resource-versions
        /// for details. Defaults to unset
        /// </param>
        /// <param name="timeoutSeconds">
        /// Timeout for the list/watch call. This limits the duration of the call, regardless
        /// of any activity or inactivity.
        /// </param>
        /// <param name="watch">
        /// Watch for changes to the described resources and return them as a stream of add,
        /// update, and remove notifications.
        /// </param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The deserialized object list.</returns>
        public static async Task<HttpOperationResponse<object>> ListClusterCustomObjectWithHttpMessagesAsync<T>(
            this ICustomObjectsOperations   k8s,
            bool?                           allowWatchBookmarks  = null,
            string                          continueParameter    = null,
            string                          fieldSelector        = null,
            string                          labelSelector        = null,
            int?                            limit                = null,
            string                          resourceVersion      = null,
            string                          resourceVersionMatch = null,
            int?                            timeoutSeconds       = null,
            bool?                           watch                = null,
            CancellationToken               cancellationToken    = default)

            where T : IKubernetesObject
        {
            await SyncContext.Clear;

            var typeMetadata = typeof(T).GetKubernetesTypeMetadata();

            return await k8s.ListClusterCustomObjectWithHttpMessagesAsync(
                group:                typeMetadata.Group,
                version:              typeMetadata.ApiVersion,
                plural:               typeMetadata.PluralName,
                allowWatchBookmarks:  allowWatchBookmarks,
                continueParameter:    continueParameter,
                fieldSelector:        fieldSelector,
                labelSelector:        labelSelector,
                limit:                limit,
                resourceVersion:      resourceVersion,
                resourceVersionMatch: resourceVersionMatch,
                timeoutSeconds:       timeoutSeconds,
                watch:                watch,
                pretty:               false,
                cancellationToken:    cancellationToken);
        }

        /// <summary>
        /// List or watch cluster scoped custom objects by group, version, and plural name, deserializing them into 
        /// <see cref="KubernetesObjectMetadata"/> instances holding just the common metadata properties.  This is 
        /// useful for managing objects without needing the resource type implementation.
        /// </summary>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="group">The custom resource's group name</param>
        /// <param name="version">The custom resource's version</param>
        /// <param name="plural">The custom resource's plural name. For TPRs this would be lowercase plural kind.</param>
        /// <param name="allowWatchBookmarks">
        /// allowWatchBookmarks requests watch events with type "BOOKMARK". Servers that
        /// do not implement bookmarks may ignore this flag and bookmarks are sent at the
        /// server's discretion. Clients should not assume bookmarks are returned at any
        /// specific interval, nor may they assume the server will send any BOOKMARK event
        /// during a session. If this is not a watch, this field is ignored. If the feature
        /// gate WatchBookmarks is not enabled in apiserver, this field is ignored.
        /// </param>
        /// <param name="continueParameter">
        /// The continue option should be set when retrieving more results from the server.
        /// Since this value is server defined, clients may only use the continue value from
        /// a previous query result with identical query parameters (except for the value
        /// of continue) and the server may reject a continue value it does not recognize.
        /// If the specified continue value is no longer valid whether due to expiration
        /// (generally five to fifteen minutes) or a configuration change on the server,
        /// the server will respond with a 410 ResourceExpired error together with a continue
        /// token. If the client needs a consistent list, it must restart their list without
        /// the continue field. Otherwise, the client may send another list request with
        /// the token received with the 410 error, the server will respond with a list starting
        /// from the next key, but from the latest snapshot, which is inconsistent from the
        /// previous list results - objects that are created, modified, or deleted after
        /// the first list request will be included in the response, as long as their keys
        /// are after the "next key". This field is not supported when watch is true. Clients
        /// may start a watch from the last resourceVersion value returned by the server
        /// and not miss any modifications.
        /// </param>
        /// <param name="fieldSelector">
        /// A selector to restrict the list of returned objects by their fields. Defaults
        /// to everything.
        /// </param>
        /// <param name="labelSelector">
        /// A selector to restrict the list of returned objects by their labels. Defaults
        /// to everything.
        /// </param>
        /// <param name="limit">
        /// limit is a maximum number of responses to return for a list call. If more items
        /// exist, the server will set the `continue` field on the list metadata to a value
        /// that can be used with the same initial query to retrieve the next set of results.
        /// Setting a limit may return fewer than the requested amount of items (up to zero
        /// items) in the event all requested objects are filtered out and clients should
        /// only use the presence of the continue field to determine whether more results
        /// are available. Servers may choose not to support the limit argument and will
        /// return all of the available results. If limit is specified and the continue field
        /// is empty, clients may assume that no more results are available. This field is
        /// not supported if watch is true. The server guarantees that the objects returned
        /// when using continue will be identical to issuing a single list call without a
        /// limit - that is, no objects created, modified, or deleted after the first request
        /// is issued will be included in any subsequent continued requests. This is sometimes
        /// referred to as a consistent snapshot, and ensures that a client that is using
        /// limit to receive smaller chunks of a very large result can ensure they see all
        /// possible objects. If objects are updated during a chunked list the version of
        /// the object that was present at the time the first list result was calculated
        /// is returned.
        /// </param>
        /// <param name="resourceVersion">
        /// When specified with a watch call, shows changes that occur after that particular
        /// version of a resource. Defaults to changes from the beginning of history. When
        /// specified for list: - if unset, then the result is returned from remote storage
        /// based on quorum-read flag; - if it's 0, then we simply return what we currently
        /// have in cache, no guarantee; - if set to non zero, then the result is at least
        /// as fresh as given rv.
        /// </param>
        /// <param name="resourceVersionMatch">
        /// resourceVersionMatch determines how resourceVersion is applied to list calls.
        /// It is highly recommended that resourceVersionMatch be set for list calls where
        /// resourceVersion is set See https://kubernetes.io/docs/reference/using-api/api-concepts/#resource-versions
        /// for details. Defaults to unset
        /// </param>
        /// <param name="timeoutSeconds">
        /// Timeout for the list/watch call. This limits the duration of the call, regardless
        /// of any activity or inactivity.
        /// </param>
        /// <param name="watch">
        /// Watch for changes to the described resources and return them as a stream of add,
        /// update, and remove notifications.
        /// </param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The deserialized object list.</returns>
        public static async Task<V1CustomObjectList<KubernetesObjectMetadata>> ListClusterCustomObjectMetadataAsync(
            this ICustomObjectsOperations   k8s,
            string                          group,
            string                          version,
            string                          plural,
            bool?                           allowWatchBookmarks  = null,
            string                          continueParameter    = null,
            string                          fieldSelector        = null,
            string                          labelSelector        = null,
            int?                            limit                = null,
            string                          resourceVersion      = null,
            string                          resourceVersionMatch = null,
            int?                            timeoutSeconds       = null,
            bool?                           watch                = null,
            CancellationToken               cancellationToken    = default)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(group), nameof(group));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(version), nameof(version));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(plural), nameof(plural));

            var result = await k8s.ListClusterCustomObjectAsync(
                group:                  group,
                version:                version,
                plural:                 plural,
                allowWatchBookmarks:    allowWatchBookmarks,
                continueParameter:      continueParameter,
                fieldSelector:          fieldSelector,
                labelSelector:          labelSelector,
                limit:                  limit,
                resourceVersion:        resourceVersion,
                resourceVersionMatch:   resourceVersionMatch,
                timeoutSeconds:         timeoutSeconds,
                watch:                  watch,
                pretty:                 false,
                cancellationToken:      cancellationToken);

            return ((JsonElement)result).Deserialize<V1CustomObjectList<KubernetesObjectMetadata>>(options: serializeOptions);
        }

        /// <summary>
        /// Create a cluster scoped custom object.
        /// </summary>
        /// <typeparam name="T">The custom object type.</typeparam>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="body">The object data.</param>
        /// <param name="name">Specifies the object name.</param>
        /// <param name="dryRun">
        /// When present, indicates that modifications should not be persisted. An invalid
        /// or unrecognized dryRun directive will result in an error response and no further
        /// processing of the request. Valid values are: - All: all dry run stages will be
        /// processed
        /// </param>
        /// <param name="fieldManager">
        /// fieldManager is a name associated with the actor or entity that is making these
        /// changes. The value must be less than or 128 characters long, and only contain
        /// printable characters, as defined by https://golang.org/pkg/unicode/#IsPrint.
        /// </param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The new object.</returns>
        public static async Task<T> CreateClusterCustomObjectAsync<T>(
            this ICustomObjectsOperations   k8s,
            T                               body,
            string                          name,
            string                          dryRun            = null,
            string                          fieldManager      = null,
            CancellationToken               cancellationToken = default) 

            where T : IKubernetesObject<V1ObjectMeta>, new()
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(body != null, nameof(body));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            body.Metadata.Name = name;

            var typeMetadata = body.GetKubernetesTypeMetadata();
            var result       = await k8s.CreateClusterCustomObjectAsync(
                body:              body, 
                group:             typeMetadata.Group, 
                version:           typeMetadata.ApiVersion,
                plural:            typeMetadata.PluralName, 
                dryRun:            dryRun, 
                fieldManager:      fieldManager, 
                pretty:            false, 
                cancellationToken: cancellationToken);

            return ((JsonElement)result).Deserialize<T>(options: serializeOptions);
        }

        /// <summary>
        /// Returns a cluster scoped custom object, deserialized as the specified generic object type.
        /// </summary>
        /// <typeparam name="T">The custom object type.</typeparam>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="name">Specifies the object name.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The deserialized object.</returns>
        public static async Task<T> ReadClusterCustomObjectAsync<T>(
            this ICustomObjectsOperations   k8s,
            string                          name,
            CancellationToken               cancellationToken = default) 
            
            where T : IKubernetesObject<V1ObjectMeta>, new()
        {
            await SyncContext.Clear;

            var typeMetadata = typeof(T).GetKubernetesTypeMetadata();
            var result       = await k8s.GetClusterCustomObjectAsync(
                group:             typeMetadata.Group,
                version:           typeMetadata.ApiVersion,
                plural:            typeMetadata.PluralName, 
                name:              name,
                cancellationToken: cancellationToken);

            return ((JsonElement)result).Deserialize<T>(options: serializeOptions);
        }

        /// <summary>
        /// Replace a cluster scoped custom object of the specified generic object type.
        /// </summary>
        /// <typeparam name="T">The custom object type.</typeparam>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="body">Specifies the new object data.</param>
        /// <param name="name">Specifies the object name.</param>
        /// <param name="dryRun">
        /// When present, indicates that modifications should not be persisted. An invalid
        /// or unrecognized dryRun directive will result in an error response and no further
        /// processing of the request. Valid values are: - All: all dry run stages will be
        /// processed
        /// </param>
        /// <param name="fieldManager">
        /// fieldManager is a name associated with the actor or entity that is making these
        /// changes. The value must be less than or 128 characters long, and only contain
        /// printable characters, as defined by https://golang.org/pkg/unicode/#IsPrint.
        /// </param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The updated object.</returns>
        public static async Task<T> ReplaceClusterCustomObjectAsync<T>(
            this ICustomObjectsOperations   k8s,
            T                               body,
            string                          name, 
            string                          dryRun            = null,
            string                          fieldManager      = null,
            CancellationToken               cancellationToken = default)

            where T : IKubernetesObject<V1ObjectMeta>, new()
        {
            await SyncContext.Clear;

            var typeMetadata = body.GetKubernetesTypeMetadata();
            var result       = await k8s.ReplaceClusterCustomObjectAsync(
                body:              body, 
                group:             typeMetadata.Group,
                version:           typeMetadata.ApiVersion,
                plural:            typeMetadata.PluralName,
                name:              name,
                dryRun:            dryRun, 
                fieldManager:      fieldManager,
                cancellationToken: cancellationToken);

            return ((JsonElement)result).Deserialize<T>(options: serializeOptions);
        }

        /// <summary>
        /// Creates or replaces a cluster scoped custom object of the specified generic 
        /// object type and name, depending on whether the object already exists in the cluster.
        /// </summary>
        /// <typeparam name="T">The custom object type.</typeparam>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="body">Specifies the new object data.</param>
        /// <param name="name">Specifies the object name.</param>
        /// <param name="dryRun">
        /// When present, indicates that modifications should not be persisted. An invalid
        /// or unrecognized dryRun directive will result in an error response and no further
        /// processing of the request. Valid values are: - All: all dry run stages will be
        /// processed
        /// </param>
        /// <param name="fieldManager">
        /// fieldManager is a name associated with the actor or entity that is making these
        /// changes. The value must be less than or 128 characters long, and only contain
        /// printable characters, as defined by https://golang.org/pkg/unicode/#IsPrint.
        /// </param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The updated object.</returns>
        public static async Task<T> UpsertClusterCustomObjectAsync<T>(
            this ICustomObjectsOperations   k8s,
            T                               body,
            string                          name, 
            string                          dryRun            = null,
            string                          fieldManager      = null,
            CancellationToken               cancellationToken = default)

            where T : IKubernetesObject<V1ObjectMeta>, new()
        {
            await SyncContext.Clear;

            // $todo(jefflill): Investigate fixing race condition:
            // 
            //      https://github.com/nforgeio/neonKUBE/issues/1578 

            // We're going to try fetching the resource first.  If it doesn't exist, we'll
            // create a new resource otherwise we'll replace the existing resource.

            T existing;

            try
            {
                existing = await k8s.ReadClusterCustomObjectAsync<T>(name, cancellationToken);
            }
            catch (HttpOperationException e)
            {
                if (e.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    return await k8s.CreateClusterCustomObjectAsync<T>(body, name, dryRun, fieldManager, cancellationToken);
                }
                else
                {
                    throw;
                }
            }

            body.Metadata.ResourceVersion   = existing.Metadata.ResourceVersion;
            body.Metadata.Generation        = existing.Metadata.Generation;
            body.Metadata.CreationTimestamp = existing.Metadata.CreationTimestamp;
            body.Metadata.Uid               = existing.Metadata.Uid;

            return await k8s.ReplaceClusterCustomObjectAsync<T>(
                body:              body, 
                name:              name, 
                dryRun:            dryRun,
                fieldManager:      fieldManager, 
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Updates the <b>status</b> of a cluster scoped custom object of the specified generic 
        /// object type and name.
        /// </summary>
        /// <typeparam name="T">The custom object type.</typeparam>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="patch">
        /// Specifies the patch to be applied to the object status.  This is typically a 
        /// <see cref="V1Patch"/> instance but additional patch types may be supported in 
        /// </param>
        /// <param name="name">Specifies the object name.</param>
        /// <param name="dryRun">
        /// When present, indicates that modifications should not be persisted. An invalid
        /// or unrecognized dryRun directive will result in an error response and no further
        /// processing of the request. Valid values are: - All: all dry run stages will be
        /// processed
        /// </param>
        /// <param name="fieldManager">
        /// fieldManager is a name associated with the actor or entity that is making these
        /// changes. The value must be less than or 128 characters long, and only contain
        /// printable characters, as defined by https://golang.org/pkg/unicode/#IsPrint.
        /// </param>
        /// <param name="force">
        /// Force is going to "force" Apply requests. It means user will re-acquire conflicting
        /// fields owned by other people. Force flag must be unset for non-apply patch requests.
        /// </param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The updated custom object.</returns>
        public static async Task<T> PatchClusterCustomObjectStatusAsync<T>(
            this ICustomObjectsOperations   k8s,
            V1Patch                         patch,
            string                          name,
            string                          dryRun            = null,
            string                          fieldManager      = null,
            bool?                           force             = null,
            CancellationToken               cancellationToken = default)

            where T : IKubernetesObject<V1ObjectMeta>, new()
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(patch != null, nameof(patch));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            var typeMetadata = typeof(T).GetKubernetesTypeMetadata();
            var result       = await k8s.PatchClusterCustomObjectStatusAsync(
                body:              patch,
                group:             typeMetadata.Group,
                version:           typeMetadata.ApiVersion,
                plural:            typeMetadata.PluralName,
                name:              name,
                dryRun:            dryRun,
                fieldManager:      fieldManager,
                force:             force,
                cancellationToken: cancellationToken);

            return ((JsonElement)result).Deserialize<T>(options: serializeOptions);
        }

        /// <summary>
        /// Updates the <b>spec</b> of a cluster scoped custom object of the specified generic 
        /// object type and name.
        /// </summary>
        /// <typeparam name="T">The custom object type.</typeparam>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="patch">
        /// Specifies the patch to be applied to the object spec.  This is typically a 
        /// <see cref="V1Patch"/> instance but additional patch types may be supported in 
        /// </param>
        /// <param name="name">Specifies the object name.</param>
        /// <param name="dryRun">
        /// When present, indicates that modifications should not be persisted. An invalid
        /// or unrecognized dryRun directive will result in an error response and no further
        /// processing of the request. Valid values are: - All: all dry run stages will be
        /// processed
        /// </param>
        /// <param name="fieldManager">
        /// fieldManager is a name associated with the actor or entity that is making these
        /// changes. The value must be less than or 128 characters long, and only contain
        /// printable characters, as defined by https://golang.org/pkg/unicode/#IsPrint.
        /// </param>
        /// <param name="force">
        /// Force is going to "force" Apply requests. It means user will re-acquire conflicting
        /// fields owned by other people. Force flag must be unset for non-apply patch requests.
        /// </param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The updated custom object.</returns>
        public static async Task<T> PatchClusterCustomObjectAsync<T>(
            this ICustomObjectsOperations   k8s,
            V1Patch                         patch,
            string                          name,
            string                          dryRun            = null,
            string                          fieldManager      = null,
            bool?                           force             = null,
            CancellationToken               cancellationToken = default)

            where T : IKubernetesObject<V1ObjectMeta>, new()
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(patch != null, nameof(patch));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            var typeMetadata = typeof(T).GetKubernetesTypeMetadata();
            var result       = await k8s.PatchClusterCustomObjectAsync(
                body:              patch,
                group:             typeMetadata.Group,
                version:           typeMetadata.ApiVersion,
                plural:            typeMetadata.PluralName,
                name:              name,
                dryRun:            dryRun,
                fieldManager:      fieldManager,
                force:             force,
                cancellationToken: cancellationToken);

            return ((JsonElement)result).Deserialize<T>(options: serializeOptions);
        }

        /// <summary>
        /// Deletes a namespace scoped custom object of the specified generic object type,
        /// and doesn't throw any exceptions if the object doesn't exist.
        /// </summary>
        /// <typeparam name="T">The custom object type.</typeparam>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="name">Specifies the object name.</param>
        /// <param name="body">Optionally specifies deletion options.</param>
        /// <param name="gracePeriodSeconds">
        /// Optionally specifies the duration in seconds before the object should be deleted. Value must be
        /// non-negative integer. The value zero indicates delete immediately. If this value
        /// is nil, the default grace period for the specified type will be used. Defaults
        /// to a per object value if not specified. zero means delete immediately.
        /// </param>
        /// <param name="orphanDependents">
        /// Deprecated: please use the PropagationPolicy, this field will be deprecated in
        /// 1.7. Should the dependent objects be orphaned. If true/false, the &quot;orphan&quot;
        /// finalizer will be added to/removed from the object&apos;s finalizers list. Either
        /// this field or PropagationPolicy may be set, but not both.
        /// </param>
        /// <param name="propagationPolicy">
        /// Optionally specifies ehether and how garbage collection will be performed. Either 
        /// this field or OrphanDependents may be set, but not both. The default policy is 
        /// decided by the existing finalizer set in the metadata.finalizers and the resource-specific
        /// default policy.
        /// </param>
        /// <param name="dryRun">
        /// Optionally specifies that modifications should not be persisted. An invalid
        /// or unrecognized dryRun directive will result in an error response and no further
        /// processing of the request. Valid values are: - All: all dry run stages will be
        /// processed
        /// </param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task DeleteClusterCustomObjectAsync<T>(
            this ICustomObjectsOperations   k8s,
            string                          name,
            V1DeleteOptions                 body               = null,
            int?                            gracePeriodSeconds = null,
            bool?                           orphanDependents   = null,
            string                          propagationPolicy  = null,
            string                          dryRun             = null,
            CancellationToken               cancellationToken  = default)

            where T : IKubernetesObject<V1ObjectMeta>, new()
        {
            await SyncContext.Clear;

            try
            {
                var typeMetadata = typeof(T).GetKubernetesTypeMetadata();

                await k8s.DeleteClusterCustomObjectAsync(
                    group:              typeMetadata.Group, 
                    version:            typeMetadata.ApiVersion, 
                    plural:             typeMetadata.PluralName, 
                    name:               name,
                    body:               body,
                    gracePeriodSeconds: gracePeriodSeconds,
                    orphanDependents:   orphanDependents,
                    propagationPolicy:  propagationPolicy,
                    dryRun:             dryRun,
                    cancellationToken:  cancellationToken);
            }
            catch (HttpOperationException e)
            {
                if (e.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    return;
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Deletes a namespace scoped custom object of the specified generic object type,
        /// and doesn't throw any exceptions if the object doesn't exist.
        /// </summary>
        /// <typeparam name="T">The custom object type.</typeparam>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="object">Specifies the object being deleted.</param>
        /// <param name="body">Optionally specifies deletion options.</param>
        /// <param name="gracePeriodSeconds">
        /// Optionally specifies the duration in seconds before the object should be deleted. Value must be
        /// non-negative integer. The value zero indicates delete immediately. If this value
        /// is nil, the default grace period for the specified type will be used. Defaults
        /// to a per object value if not specified. zero means delete immediately.
        /// </param>
        /// <param name="orphanDependents">
        /// Deprecated: please use the PropagationPolicy, this field will be deprecated in
        /// 1.7. Should the dependent objects be orphaned. If true/false, the &quot;orphan&quot;
        /// finalizer will be added to/removed from the object&apos;s finalizers list. Either
        /// this field or PropagationPolicy may be set, but not both.
        /// </param>
        /// <param name="propagationPolicy">
        /// Optionally specifies ehether and how garbage collection will be performed. Either 
        /// this field or OrphanDependents may be set, but not both. The default policy is 
        /// decided by the existing finalizer set in the metadata.finalizers and the resource-specific
        /// default policy.
        /// </param>
        /// <param name="dryRun">
        /// Optionally specifies that modifications should not be persisted. An invalid
        /// or unrecognized dryRun directive will result in an error response and no further
        /// processing of the request. Valid values are: - All: all dry run stages will be
        /// processed
        /// </param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task DeleteClusterCustomObjectAsync<T>(
            this ICustomObjectsOperations   k8s,
            T                               @object,
            V1DeleteOptions                 body               = null,
            int?                            gracePeriodSeconds = null,
            bool?                           orphanDependents   = null,
            string                          propagationPolicy  = null,
            string                          dryRun             = null,
            CancellationToken               cancellationToken  = default)

            where T : IKubernetesObject<V1ObjectMeta>, new()
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(@object != null, nameof(@object));

            try
            {
                var typeMetadata = typeof(T).GetKubernetesTypeMetadata();

                await k8s.DeleteClusterCustomObjectAsync(
                    group:              typeMetadata.Group, 
                    version:            typeMetadata.ApiVersion, 
                    plural:             typeMetadata.PluralName, 
                    name:               @object.Name(),
                    body:               body,
                    gracePeriodSeconds: gracePeriodSeconds,
                    orphanDependents:   orphanDependents,
                    propagationPolicy:  propagationPolicy,
                    dryRun:             dryRun,
                    cancellationToken:  cancellationToken);
            }
            catch (HttpOperationException e)
            {
                if (e.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    return;
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
