//-----------------------------------------------------------------------------
// FILE:	    KubernetesExtensions.ClusterCustom.cs
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
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;

using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Rest;

using Neon.Common;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using k8s;
using k8s.Models;

namespace Neon.Kube
{
    public static partial class KubernetesExtensions
    {
        //---------------------------------------------------------------------
        // Namedspaced generic custom object extensions:

        /// <summary>
        /// List or watch a cluster scoped custom object, deserializing it into the specified
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
        public static async Task<T> ListClusterCustomObjectAsync<T>(
            this IKubernetes    k8s,
            bool?               allowWatchBookmarks  = null,
            string              continueParameter    = null,
            string              fieldSelector        = null,
            string              labelSelector        = null,
            int?                limit                = null,
            string              resourceVersion      = null,
            string              resourceVersionMatch = null,
            int?                timeoutSeconds       = null,
            bool?               watch                = null,
            CancellationToken   cancellationToken    = default(CancellationToken))

            where T : IKubernetesObject, new()
        {
            var typeMetadata = new T().GetKubernetesTypeMetadata();

            var result = await k8s.ListClusterCustomObjectAsync(
                typeMetadata.Group,
                typeMetadata.ApiVersion,
                typeMetadata.PluralName,
                allowWatchBookmarks,
                continueParameter,
                fieldSelector,
                labelSelector,
                limit,
                resourceVersion,
                resourceVersionMatch,
                timeoutSeconds,
                watch,
                pretty: false,
                cancellationToken);

            return NeonHelper.JsonDeserialize<T>(((JsonElement)result).GetRawText());
        }

        /// <summary>
        /// Create a cluster scoped custom object.
        /// </summary>
        /// <typeparam name="T">The custom object type.</typeparam>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="body">The object data.</param>
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
        /// <returns>The new object.</returns>
        public static async Task<T> CreateClusterCustomObjectAsync<T>(
            this IKubernetes    k8s,
            T                   body,
            string              dryRun       = null,
            string              fieldManager = null) 

            where T : IKubernetesObject, new()
        {
            var typeMetadata = body.GetKubernetesTypeMetadata();
            var result       = await k8s.CreateClusterCustomObjectAsync(body, typeMetadata.Group, typeMetadata.ApiVersion, typeMetadata.PluralName, dryRun, fieldManager, pretty: false);

            return NeonHelper.JsonDeserialize<T>(((JsonElement)result).GetRawText());
        }

        /// <summary>
        /// Returns a cluster scoped custom object, deserialized as the specified generic type.
        /// </summary>
        /// <typeparam name="T">The custom object type.</typeparam>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="name">Specifies the object name.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The deserialized object.</returns>
        public static async Task<T> GetClusterCustomObjectAsync<T>(
            this IKubernetes    k8s,
            string              name,
            CancellationToken   cancellationToken = default(CancellationToken)) 
            
            where T : IKubernetesObject, new()
        {
            var typeMetadata = new T().GetKubernetesTypeMetadata();
            var result       = await k8s.GetClusterCustomObjectAsync(typeMetadata.Group, typeMetadata.ApiVersion, typeMetadata.PluralName, name, cancellationToken);

            return NeonHelper.JsonDeserialize<T>(((JsonElement)result).GetRawText());
        }

        /// <summary>
        /// Replace a cluster scoped custom object of the specified generic type.
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
            this IKubernetes    k8s,
            T                   body,
            string              name, 
            string              dryRun            = null,
            string              fieldManager      = null,
            CancellationToken   cancellationToken = default(CancellationToken))

            where T : IKubernetesObject, new()
        {
            var typeMetadata = body.GetKubernetesTypeMetadata();
            var result       = await k8s.ReplaceClusterCustomObjectAsync(body, typeMetadata.Group, typeMetadata.ApiVersion, typeMetadata.PluralName, name, dryRun, fieldManager, cancellationToken);

            return NeonHelper.JsonDeserialize<T>(((JsonElement)result).GetRawText());
        }

        /// <summary>
        /// Creates or replaces a cluster scoped custom object of the specified generic type,
        /// depending on whether the object already exists in the cluster.
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
            this IKubernetes    k8s,
            T                   body,
            string              name, 
            string              dryRun            = null,
            string              fieldManager      = null,
            CancellationToken   cancellationToken = default(CancellationToken))

            where T : IKubernetesObject, new()
        {
            // We're going to try fetching the resource first.  If it doesn't exist, we'll
            // create it otherwise we'll replace it.

            try
            {
                await k8s.GetClusterCustomObjectAsync<T>(name, cancellationToken);
            }
            catch (HttpOperationException e)
            {
                if (e.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    return await k8s.CreateClusterCustomObjectAsync<T>(body, dryRun, fieldManager);
                }
                else
                {
                    throw;
                }
            }

            return await k8s.ReplaceClusterCustomObjectAsync<T>(body, name, dryRun, fieldManager);
        }
    }
}
