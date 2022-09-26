//-----------------------------------------------------------------------------
// FILE:	    V1NeonClusterOperator.cs
// CONTRIBUTOR: Jeff Lill
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
using System.Text;

using k8s;
using k8s.Models;

#if KUBEOPS
using DotnetKubernetesClient.Entities;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;
using Neon.Kube;
#endif

#if KUBEOPS
namespace Neon.Kube.ResourceDefinitions
#else
namespace Neon.Kube.Resources
#endif
{
    /// <summary>
    /// Specifies the <b>neon-cluster-operator</b> settings.
    /// </summary>
    [KubernetesEntity(Group = KubeGroup, ApiVersion = KubeApiVersion, Kind = KubeKind, PluralName = KubePlural)]
#if KUBEOPS
    [KubernetesEntityShortNames]
    [EntityScope(EntityScope.Cluster)]
    [Description("Used to specify settings for the [neon-cluster-operator] and for the operator to report status.")]
#endif
    public class V1NeonClusterOperator : CustomKubernetesEntity<V1NeonClusterOperator.OperatorSpec, V1NeonClusterOperator.OperatorStatus>
    {
        /// <summary>
        /// Object API group.
        /// </summary>
        public const string KubeGroup = ResourceHelper.NeonKubeResourceGroup;

        /// <summary>
        /// Object API version.
        /// </summary>
        public const string KubeApiVersion = "v1alpha1";

        /// <summary>
        /// Object API kind.
        /// </summary>
        public const string KubeKind = "NeonClusterOperator";

        /// <summary>
        /// Object plural name.
        /// </summary>
        public const string KubePlural = "neonclusteroperators";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public V1NeonClusterOperator()
        {
            this.SetMetadata();
        }

        /// <summary>
        /// The node execute task specification.
        /// </summary>
        public class OperatorSpec
        {
            /// <summary>
            /// A test string.
            /// </summary>
            public string Message { get; set; }

            /// <summary>
            /// The cron schedule for updating node certificates.
            /// </summary>
            public CertificateUpdateSchedule CertificateUpdateSchedule { get; set; } = "0 0 * * *";
        }

        /// <summary>
        /// The node execute task status.
        /// </summary>
        public class OperatorStatus
        {
            /// <summary>
            /// Testing <see cref="DateTime"/> .
            /// </summary>
            public DateTime? Timestamp { get; set; }
        }

        /// <summary>
        /// The certificate update schedules.
        /// </summary>
        public class CertificateUpdateSchedule
        {
            /// <summary>
            /// Control plane certificate update schedule.
            /// </summary>
            public string ControlPlane { get; set; }

            /// <summary>
            /// Node CA certificate update schedule.
            /// </summary>
            public string NodeCa { get; set; }
        }
    }
}
