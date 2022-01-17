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
using System.Reflection;
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
    public static partial class KubernetesExtensions
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
                timeout:      TimeSpan.FromSeconds(300),
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
                timeout:      TimeSpan.FromSeconds(300),
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
                timeout:      TimeSpan.FromSeconds(300),
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
                timeout:      TimeSpan.FromSeconds(300),
                pollInterval: TimeSpan.FromMilliseconds(500));
        }

        //---------------------------------------------------------------------
        // IKubernetesObject extensions

        // Used to cache [KubernetesEntityAttribute] values for custom resource types
        // for better performance (avoiding unnecessary reflection).

        private class CustomResourceMetadata
        {
            public CustomResourceMetadata(KubernetesEntityAttribute attr)
            {
                this.Group           = attr.Group;
                this.ApiVersion      = attr.ApiVersion;
                this.Kind            = attr.Kind;
                this.GroupApiVersion = $"{attr.Group}/{attr.ApiVersion}";
            }

            public string Group { get; private set; }
            public string ApiVersion { get; private set; }
            public string Kind { get; private set; }
            public string GroupApiVersion { get; private set; }
        }

        private static Dictionary<Type, CustomResourceMetadata> typeToKubernetesEntity = new ();

        /// <summary>
        /// Initializes a custom Kubernetes object's metadata <b>Group</b>, <b>ApiVersion</b>, and
        /// <b>Kind</b> properties from the <see cref="KubernetesEntityAttribute"/> attached to the
        /// object's type.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <exception cref="InvalidDataException">Thrown when the object's type does not have a <see cref="KubernetesEntityAttribute"/>.</exception>
        /// <remarks>
        /// <para>
        /// This should be called in all custom object constructors to ensure that the object's
        /// metadata is configured and matches what was specified in the attribute.  Here's
        /// what this will look like:
        /// </para>
        /// <code language="C#">
        /// [KubernetesEntity(Group = "mygroup.io", ApiVersion = "v1", Kind = "my-resource", PluralName = "my-resources")]
        /// [KubernetesEntityShortNames]
        /// [EntityScope(EntityScope.Cluster)]
        /// [Description("My custom resource.")]
        /// public class V1MyCustomResource : CustomKubernetesEntity&lt;V1ContainerRegistry.V1ContainerRegistryEntitySpec&gt;
        /// {
        ///     public V1ContainerRegistry()
        ///     {
        ///         ((IKubernetesObject)this).InitializeMetadata();
        ///     }
        ///
        ///     ...
        /// </code>
        /// </remarks>
        public static void SetMetadata(this IKubernetesObject obj)
        {
            var objType = obj.GetType();

            CustomResourceMetadata customMetadata;

            lock (typeToKubernetesEntity)
            {
                if (!typeToKubernetesEntity.TryGetValue(objType, out customMetadata))
                {
                    var entityAttr = objType.GetCustomAttribute<KubernetesEntityAttribute>();

                    if (entityAttr == null)
                    {
                        throw new InvalidDataException($"Custom Kubernetes resource type [{objType.FullName}] does not have a [{nameof(KubernetesEntityAttribute)}].");
                    }

                    customMetadata = new CustomResourceMetadata(entityAttr);

                    typeToKubernetesEntity.Add(objType, customMetadata);
                }
            }

            obj.ApiVersion = customMetadata.GroupApiVersion;
            obj.Kind       = customMetadata.Kind;
        }

        //---------------------------------------------------------------------
        // IKubernetes client extensions.

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
    }
}
