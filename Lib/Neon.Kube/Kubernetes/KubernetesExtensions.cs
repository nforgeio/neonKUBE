//-----------------------------------------------------------------------------
// FILE:	    KubernetesExtensions.cs
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
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using Microsoft.AspNetCore.JsonPatch;

using Neon.Common;

using Newtonsoft.Json;

using k8s;
using k8s.Models;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace Neon.Kube
{
    /// <summary>
    /// Kubernetes related extension methods.
    /// </summary>
    public static class KubernetesExtensions
    {
        //---------------------------------------------------------------------
        // Deployment extensions

        /// <summary>
        /// Restarts a <see cref="V1Deployment"/>.
        /// </summary>
        /// <param name="deployment">The target deployment.</param>
        /// <param name="k8s">The <see cref="IKubernetes"/> client to be used for the operation.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task RestartAsync(this V1Deployment deployment, IKubernetes k8s)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            // $todo(jefflill):
            //
            // Fish out the k8s client from the deployment so we don't have to pass it in as a parameter.

            var generation = deployment.Status.ObservedGeneration;

            var patchStr = $@"
{{
    ""spec"": {{
        ""template"": {{
            ""metadata"": {{
                ""annotations"": {{
                    ""kubectl.kubernetes.io/restartedAt"": ""{DateTime.UtcNow.ToString("s")}""
                }}
            }}
        }}
    }}
}}";

            await k8s.PatchNamespacedDeploymentAsync(new V1Patch(patchStr, V1Patch.PatchType.MergePatch), deployment.Name(), deployment.Namespace());

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    try
                    {
                        var newDeployment = await k8s.ReadNamespacedDeploymentAsync(deployment.Name(), deployment.Namespace());

                        return newDeployment.Status.ObservedGeneration > generation;
                    }
                    catch
                    {
                        return false;
                    }
                },
                timeout:      TimeSpan.FromSeconds(30),
                pollInterval: TimeSpan.FromMilliseconds(500));

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    try
                    {
                        deployment = await k8s.ReadNamespacedDeploymentAsync(deployment.Name(), deployment.Namespace());

                        return (deployment.Status.Replicas == deployment.Status.AvailableReplicas) && deployment.Status.UnavailableReplicas == null;
                    }
                    catch
                    {
                        return false;
                    }
                },
                timeout:      TimeSpan.FromSeconds(120),
                pollInterval: TimeSpan.FromMilliseconds(500));
        }

        /// <summary>
        /// Restarts a <see cref="V1StatefulSet"/>.
        /// </summary>
        /// <param name="statefulset">The deployment being restarted.</param>
        /// <param name="k8s">The <see cref="IKubernetes"/> client to be used for the operation.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task RestartAsync(this V1StatefulSet statefulset, IKubernetes k8s)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            // $todo(jefflill):
            //
            // Fish out the k8s client from the statefulset so we don't have to pass it in as a parameter.

            var generation = statefulset.Status.ObservedGeneration;

            var patchStr = $@"
{{
    ""spec"": {{
        ""template"": {{
            ""metadata"": {{
                ""annotations"": {{
                    ""kubectl.kubernetes.io/restartedAt"": ""{DateTime.UtcNow.ToString("s")}""
                }}
            }}
        }}
    }}
}}";

            await k8s.PatchNamespacedStatefulSetAsync(new V1Patch(patchStr, V1Patch.PatchType.MergePatch), statefulset.Name(), statefulset.Namespace());

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    try
                    {
                        var newDeployment = await k8s.ReadNamespacedStatefulSetAsync(statefulset.Name(), statefulset.Namespace());

                        return newDeployment.Status.ObservedGeneration > generation;
                    }
                    catch
                    {
                        return false;
                    }
                },
                timeout: TimeSpan.FromSeconds(90),
                pollInterval: TimeSpan.FromMilliseconds(500));

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    try
                    {
                        statefulset = await k8s.ReadNamespacedStatefulSetAsync(statefulset.Name(), statefulset.Namespace());

                        return (statefulset.Status.Replicas == statefulset.Status.ReadyReplicas) && statefulset.Status.UpdatedReplicas == null;
                    }
                    catch
                    {
                        return false;
                    }
                },
                timeout:      TimeSpan.FromSeconds(120),
                pollInterval: TimeSpan.FromMilliseconds(500));
        }

        //---------------------------------------------------------------------
        // Kubernetes client extensions.

        // $note(jefflill):
        //
        // These methods are not currently added automatically to the generated [KubernetesWithRetry]
        // class.  We need to add these manually in the [KubernetesWithRetry.manual.cs] file.

        /// <summary>
        /// Adds a new Kubernetes secret or updates an existing secret.
        /// </summary>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="secret">The secret.</param>
        /// <param name="namespace">Optionally overrides the default namespace.</param>
        /// <returns>The updated secret.</returns>
        public static async Task<V1Secret> UpsertSecretAsync(this IKubernetes k8s, V1Secret secret, string @namespace = null)
        {
            Covenant.Requires<ArgumentNullException>(secret != null, nameof(secret));

            if ((await k8s.ListNamespacedSecretAsync(@namespace)).Items.Any(s => s.Metadata.Name == secret.Name()))
            {
                return await k8s.ReplaceNamespacedSecretAsync(secret, secret.Name(), @namespace);
            }
            else
            {
                return await k8s.CreateNamespacedSecretAsync(secret, @namespace);
            }
        }

        /// <summary>
        /// Waits for a service deployment to complete.
        /// </summary>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="namespaceParameter">The namespace.</param>
        /// <param name="name">The deployment name.</param>
        /// <param name="labelSelector">The optional label selector.</param>
        /// <param name="fieldSelector">The optional field selector.</param>
        /// <param name="pollInterval">Optionally specifies the polling interval.  This defaults to 1 second.</param>
        /// <param name="timeout">Optopnally specifies the operation timeout.  This defaults to 30 seconds.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>x
        /// <remarks>
        /// One of <paramref name="name"/>, <paramref name="labelSelector"/>, or <paramref name="fieldSelector"/>
        /// must be specified.
        /// </remarks>
        public static async Task WaitForDeploymentAsync(
            this IKubernetes    k8s, 
            string              namespaceParameter, 
            string              name          = null, 
            string              labelSelector = null,
            string              fieldSelector = null,
            TimeSpan            pollInterval  = default,
            TimeSpan            timeout       = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(namespaceParameter), nameof(namespaceParameter));
            Covenant.Requires<ArgumentException>(name != null || labelSelector != null || fieldSelector != null, "One of name, labelSelector or fieldSelector must be set,");

            if (pollInterval <= TimeSpan.Zero)
            {
                pollInterval = TimeSpan.FromSeconds(1);
            }

            if (timeout <= TimeSpan.Zero)
            {
                timeout = TimeSpan.FromSeconds(30);
            }

            if (!string.IsNullOrEmpty(name))
            {
                if (!string.IsNullOrEmpty(fieldSelector))
                {
                    fieldSelector += $",metadata.name={name}";
                }
                else
                {
                    fieldSelector = $"metadata.name={name}";
                }
            }

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    try
                    {
                        var deployments = await k8s.ListNamespacedDeploymentAsync(namespaceParameter, fieldSelector: fieldSelector, labelSelector: labelSelector);

                        if (deployments == null || deployments.Items.Count == 0)
                        {
                            return false;
                        }

                        return deployments.Items.All(deployment => deployment.Status.AvailableReplicas == deployment.Spec.Replicas);
                    }
                    catch
                    {
                        return false;
                    }
                            
                },
                timeout:      timeout,
                pollInterval: pollInterval);
        }

        /// <summary>
        /// Waits for a stateful set deployment to complete.
        /// </summary>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="namespaceParameter">The namespace.</param>
        /// <param name="name">The deployment name.</param>
        /// <param name="labelSelector">The optional label selector.</param>
        /// <param name="fieldSelector">The optional field selector.</param>
        /// <param name="pollInterval">Optionally specifies the polling interval.  This defaults to 1 second.</param>
        /// <param name="timeout">Optopnally specifies the operation timeout.  This defaults to 30 seconds.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// One of <paramref name="name"/>, <paramref name="labelSelector"/>, or <paramref name="fieldSelector"/>
        /// must be specified.
        /// </remarks>
        public static async Task WaitForStatefulSetAsync(
            this IKubernetes    k8s,
            string              namespaceParameter,
            string              name          = null,
            string              labelSelector = null,
            string              fieldSelector = null,
            TimeSpan            pollInterval  = default,
            TimeSpan            timeout       = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(namespaceParameter), nameof(namespaceParameter));
            Covenant.Requires<ArgumentException>(name != null || labelSelector != null || fieldSelector != null, "One of [name], [labelSelector] or [fieldSelector] must be passed.");

            if (pollInterval <= TimeSpan.Zero)
            {
                pollInterval = TimeSpan.FromSeconds(1);
            }

            if (timeout <= TimeSpan.Zero)
            {
                timeout = TimeSpan.FromSeconds(30);
            }

            if (!string.IsNullOrEmpty(name))
            {
                if (!string.IsNullOrEmpty(fieldSelector))
                {
                    fieldSelector += $",metadata.name={name}";
                }
                else
                {
                    fieldSelector = $"metadata.name={name}";
                }
            }

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    try
                    {
                        var statefulsets = await k8s.ListNamespacedStatefulSetAsync(namespaceParameter, fieldSelector: fieldSelector, labelSelector: labelSelector);

                        if (statefulsets == null || statefulsets.Items.Count == 0)
                        {
                            return false;
                        }

                        return statefulsets.Items.All(@set => @set.Status.ReadyReplicas == @set.Spec.Replicas);
                    }
                    catch
                    {
                        return false;
                    }
                },
                timeout:      timeout,
                pollInterval: pollInterval);
        }

        /// <summary>
        /// Waits for a daemon set deployment to complete.
        /// </summary>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="namespaceParameter">The namespace.</param>
        /// <param name="name">The deployment name.</param>
        /// <param name="labelSelector">The optional label selector.</param>
        /// <param name="fieldSelector">The optional field selector.</param>
        /// <param name="pollInterval">Optionally specifies the polling interval.  This defaults to 1 second.</param>
        /// <param name="timeout">Optopnally specifies the operation timeout.  This defaults to 30 seconds.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// One of <paramref name="name"/>, <paramref name="labelSelector"/>, or <paramref name="fieldSelector"/>
        /// must be specified.
        /// </remarks>
        public static async Task WaitForDaemonsetAsync(

            this IKubernetes    k8s,
            string              namespaceParameter,
            string              name          = null,
            string              labelSelector = null,
            string              fieldSelector = null,
            TimeSpan            pollInterval  = default,
            TimeSpan            timeout       = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(namespaceParameter), nameof(namespaceParameter));
            Covenant.Requires<ArgumentException>(name != null || labelSelector != null || fieldSelector != null, "One of [name], [labelSelector] or [fieldSelector] must be passed.");

            if (pollInterval <= TimeSpan.Zero)
            {
                pollInterval = TimeSpan.FromSeconds(1);
            }

            if (timeout <= TimeSpan.Zero)
            {
                timeout = TimeSpan.FromSeconds(30);
            }

            if (!string.IsNullOrEmpty(name))
            {
                if (!string.IsNullOrEmpty(fieldSelector))
                {
                    fieldSelector += $",metadata.name={name}";
                }
                else
                {
                    fieldSelector = $"metadata.name={name}";
                }
            }
            await NeonHelper.WaitForAsync(
                async () =>
                {
                    try
                    {
                        var daemonsets = await k8s.ListNamespacedDaemonSetAsync(namespaceParameter, fieldSelector: fieldSelector, labelSelector: labelSelector);

                        if (daemonsets == null || daemonsets.Items.Count == 0)
                        {
                            return false;
                        }

                        return daemonsets.Items.All(@set => @set.Status.NumberAvailable == @set.Status.DesiredNumberScheduled);
                    }
                    catch
                    {
                        return false;
                    }
                },
                timeout:      timeout,
                pollInterval: pollInterval);
        }

        /// <summary>
        /// Executes a program within a pod container.
        /// </summary>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="namespaceParameter">Specifies the namespace hosting the pod.</param>
        /// <param name="name">Specifies the target pod name.</param>
        /// <param name="container">Identifies the target container within the pod.</param>
        /// <param name="command">Specifies the program and arguments to be executed.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <param name="noSuccessCheck">Optionally disables the <see cref="ExecuteResponse.EnsureSuccess"/> check.</param>
        /// <returns>An <see cref="ExecuteResponse"/> with the command exit code and output and error text.</returns>
        /// <exception cref="ExecuteException">Thrown if the exit code isn't zero and <paramref name="noSuccessCheck"/><c>=false</c>.</exception>
        public static async Task<ExecuteResponse> NamespacedPodExecAsync(
            this IKubernetes    k8s,
            string              namespaceParameter,
            string              name,
            string              container,
            string[]            command,
            CancellationToken   cancellationToken = default,
            bool                noSuccessCheck    = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(namespaceParameter), nameof(namespaceParameter));
            Covenant.Requires<ArgumentNullException>(command != null, nameof(command));
            Covenant.Requires<ArgumentException>(command.Length > 0, nameof(command));
            Covenant.Requires<ArgumentException>(!string.IsNullOrEmpty(command[0]), nameof(command));
            Covenant.Requires<ArgumentNullException>(name != null, nameof(name));
            Covenant.Requires<ArgumentNullException>(container != null, nameof(container));

            var stdOut = "";
            var stdErr = "";

            var handler = new ExecAsyncCallback(async (_stdIn, _stdOut, _stdError) =>
            {
                stdOut = Encoding.UTF8.GetString(await _stdOut.ReadToEndAsync());
                stdErr = Encoding.UTF8.GetString(await _stdError.ReadToEndAsync());
            });

            var exitCode = await k8s.NamespacedPodExecAsync(
                name:              name,
                @namespace:        namespaceParameter,
                container:         container,
                command:           command,
                tty:               false,
                action:            handler,
                cancellationToken: cancellationToken);

            var response = new ExecuteResponse(exitCode, stdOut, stdErr);

            if (!noSuccessCheck)
            {
                response.EnsureSuccess();
            }

            return response;
        }

        /// <summary>
        /// List or watch a namespaced custom object.
        /// </summary>
        /// <typeparam name="T">The custom object list type.</typeparam>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="namespaceParameter">Specifies the namespace hosting the pod.</param>
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
        /// <param name="pretty">Optionally pretty print the output.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The object list.</returns>
        public static async Task<T> ListNamespacedCustomObjectAsync<T>(
            this IKubernetes    k8s,
            string              namespaceParameter,
            bool?               allowWatchBookmarks  = null,
            string              continueParameter    = null,
            string              fieldSelector        = null,
            string              labelSelector        = null,
            int?                limit                = null,
            string              resourceVersion      = null,
            string              resourceVersionMatch = null,
            int?                timeoutSeconds       = null,
            bool?               watch                = null,
            bool?               pretty               = null,
            CancellationToken   cancellationToken    = default(CancellationToken))
            where T : IKubernetesObject, new()
        {
            var customObject = new T();
            var typeMetadata = customObject.GetKubernetesTypeMetadata();

            var result = await k8s.ListNamespacedCustomObjectAsync(
                typeMetadata.Group,
                typeMetadata.ApiVersion,
                namespaceParameter,
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
                pretty,
                cancellationToken);

            return NeonHelper.JsonDeserialize<T>(((JsonElement)result).GetRawText());
        }

        /// <summary>
        /// Create a namespaced custom object.
        /// </summary>
        /// <typeparam name="T">The custom object type.</typeparam>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="body">The object data.</param>
        /// <param name="namespaceParameter">That target Kubernetes namespace.</param>
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
        /// <param name="pretty">Optionally pretty print the output.</param>
        /// <returns></returns>
        public static async Task<T> CreateNamespacedCustomObjectAsync<T>(
            this        IKubernetes k8s,
            T           body,
            string      namespaceParameter,
            string      dryRun       = null,
            string      fieldManager = null,
            bool?       pretty       = null) 
            where T : IKubernetesObject
        {
            var typeMetadata = body.GetKubernetesTypeMetadata();
            var result       = await k8s.CreateNamespacedCustomObjectAsync(body, typeMetadata.Group, typeMetadata.ApiVersion, namespaceParameter, typeMetadata.PluralName, dryRun, fieldManager, pretty);

            return NeonHelper.JsonDeserialize<T>(((JsonElement)result).GetRawText());
        }

        /// <summary>
        /// Returns a namespaced custom object.
        /// </summary>
        /// <typeparam name="T">The custom object type.</typeparam>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="namespaceParameter">That target Kubernetes namespace.</param>
        /// <param name="name">Specifies the object name.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>The object.</returns>
        public static async Task<T> GetNamespacedCustomObjectAsync<T>(
            this IKubernetes    k8s,
            string              namespaceParameter,
            string              name,
            CancellationToken   cancellationToken = default(CancellationToken)) where T : IKubernetesObject, new()
        {
            var customObject = new T();
            var typeMetadata = customObject.GetKubernetesTypeMetadata();
            var result       = await k8s.GetNamespacedCustomObjectAsync(typeMetadata.Group, typeMetadata.ApiVersion, namespaceParameter, typeMetadata.PluralName, name, cancellationToken);

            return NeonHelper.JsonDeserialize<T>(((JsonElement)result).GetRawText());
        }

        /// <summary>
        /// Replace a namespaced custom object.
        /// </summary>
        /// <typeparam name="T">The custom object type.</typeparam>
        /// <param name="k8s">The <see cref="Kubernetes"/> client.</param>
        /// <param name="body">Specifies the new object data.</param>
        /// <param name="namespaceParameter">That target Kubernetes namespace.</param>
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
        public static async Task<T> ReplaceNamespacedCustomObjectAsync<T>(
            this IKubernetes k8s,
            T body, 
            string              namespaceParameter, 
            string              name, 
            string              dryRun            = null,
            string              fieldManager      = null,
            CancellationToken   cancellationToken = default(CancellationToken))
            where T : IKubernetesObject
        {
            var typeMetadata = body.GetKubernetesTypeMetadata();
            var result       = await k8s.ReplaceNamespacedCustomObjectAsync(body, typeMetadata.Group, typeMetadata.ApiVersion, namespaceParameter, typeMetadata.PluralName, name, dryRun, fieldManager, cancellationToken);

            return NeonHelper.JsonDeserialize<T>(((JsonElement)result).GetRawText());
        }
    }
}
