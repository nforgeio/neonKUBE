//-----------------------------------------------------------------------------
// FILE:	    V1NeonClusterOperator.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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

namespace Neon.Kube.Resources.Cluster
{
    /// <summary>
    /// Specifies the <b>neon-cluster-operator</b> settings.
    /// </summary>
    [KubernetesEntity(Group = KubeGroup, ApiVersion = KubeApiVersion, Kind = KubeKind, PluralName = KubePlural)]
    [EntityScope(EntityScope.Cluster)]
    public class V1NeonClusterOperator : IKubernetesObject<V1ObjectMeta>, ISpec<V1NeonClusterOperator.OperatorSpec>
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
            ApiVersion = $"{KubeGroup}/{KubeApiVersion}";
            Kind       = KubeKind;
        }

        /// <summary>
        /// Gets or sets APIVersion defines the versioned schema of this
        /// representation of an object. Servers should convert recognized
        /// schemas to the latest internal value, and may reject unrecognized
        /// values. More info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#resources
        /// </summary>
        public string ApiVersion { get; set; }

        /// <summary>
        /// Gets or sets kind is a string value representing the REST resource
        /// this object represents. Servers may infer this from the endpoint
        /// the client submits requests to. Cannot be updated. In CamelCase.
        /// More info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#types-kinds
        /// </summary>
        public string Kind { get; set; }

        /// <summary>
        /// Gets or sets standard object metadata.
        /// </summary>
        public V1ObjectMeta Metadata { get; set; }

        /// <summary>
        /// The spec.
        /// </summary>
        public OperatorSpec Spec { get; set; }

        /// <summary>
        /// The status.
        /// </summary>
        public OperatorStatus Status { get; set; }

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
            public Updates Updates { get; set; }
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
        public class Updates
        {
            /// <summary>
            /// Control plane certificate update spec.
            /// </summary>
            public UpdateSpec ControlPlaneCertificates { get; set; } = new UpdateSpec();

            /// <summary>
            /// Node CA certificate update spec.
            /// </summary>
            public UpdateSpec NodeCaCertificates { get; set; } = new UpdateSpec();

            /// <summary>
            /// Update spec for security spec.
            /// </summary>
            public UpdateSpec SecurityPatches { get; set; } = new UpdateSpec();

            /// <summary>
            /// Update spec for container images.
            /// </summary>
            public UpdateSpec ContainerImages { get; set; } = new UpdateSpec();

            /// <summary>
            /// Update spec for telemetry.
            /// </summary>
            public UpdateSpec Telemetry { get; set; } = new UpdateSpec();

            /// <summary>
            /// When the Neon Desktop certificate should be updated.
            /// </summary>
            public UpdateSpec NeonDesktopCertificate { get; set; } = new UpdateSpec();
        }

        /// <summary>
        /// The certificate update schedules.
        /// </summary>
        public class UpdateSpec
        {
            /// <summary>
            /// Specifies whether this update is enabled.
            /// </summary>
            public bool Enabled { get; set; } = true;

            /// <summary>
            /// <para>
            /// The update schedule. This is a represented as a cron expression. Cron expressions are 
            /// made up of seven sub-expressions that describe the details of the schedule. The sub expressions
            /// are:
            /// </para>
            /// 
            /// <list type="bullet">
            ///     <item>Seconds</item>
            ///     <item>Minutes</item>
            ///     <item>Hours</item>
            ///     <item>Day-of-Month</item>
            ///     <item>Month</item>
            ///     <item>Day-of-Week</item>
            ///     <item>Year (optional)</item>
            /// </list>
            /// 
            /// <para>
            /// An example of a complete cron expression is <code>0 0 15 ? * MON</code> which means
            /// every monday at 3pm.
            /// </para>
            /// 
            /// </summary>
            /// <remarks>
            /// For the full documentation see: 
            /// https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/crontriggers.html#cron-expressions
            /// </remarks>
            public string Schedule { get; set; } = "0 0 0 ? * 1";
        }
    }
}
