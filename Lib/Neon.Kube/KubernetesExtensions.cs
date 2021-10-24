//-----------------------------------------------------------------------------
// FILE:	    KubernetesExtensions.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.JsonPatch;

using Neon.Common;

using k8s;
using k8s.Models;

namespace Neon.Kube
{
    /// <summary>
    /// Kubernetes related extension methods.
    /// </summary>
    public static class KubernetesExtensions
    {
        /// <summary>
        /// Restarts a <see cref="V1Deployment"/>.
        /// </summary>
        /// <param name="deployment"></param>
        /// <param name="kubernetes"></param>
        /// <returns></returns>
        public static async Task RestartAsync(this V1Deployment deployment, IKubernetes kubernetes)
        {
            // $todo(jefflill):
            // fish out the k8s client from the deployment so we don't have to pass it in as a parameter.

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

            await kubernetes.PatchNamespacedDeploymentAsync(new V1Patch(patchStr, V1Patch.PatchType.MergePatch), deployment.Name(), deployment.Namespace());

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    try
                    {
                        var newDeployment = await kubernetes.ReadNamespacedDeploymentAsync(deployment.Name(), deployment.Namespace());

                        return newDeployment.Status.ObservedGeneration > generation;
                    }
                    catch
                    {
                        return false;
                    }
                },
                timeout: TimeSpan.FromSeconds(30),
                pollInterval: TimeSpan.FromMilliseconds(500));

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    try
                    {
                        deployment = await kubernetes.ReadNamespacedDeploymentAsync(deployment.Name(), deployment.Namespace());

                        return (deployment.Status.Replicas == deployment.Status.AvailableReplicas) && deployment.Status.UnavailableReplicas == null;
                    }
                    catch
                    {
                        return false;
                    }
                },
                timeout: TimeSpan.FromSeconds(30),
                pollInterval: TimeSpan.FromMilliseconds(500));
        }
    }
}
