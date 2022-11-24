//-----------------------------------------------------------------------------
// FILE:	    V1MinioBucket.cs
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
using System.Runtime.Serialization;

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
    /// Used for unit testing Kubernetes clients.
    /// </summary>
    [KubernetesEntity(Group = KubeGroup, ApiVersion = KubeApiVersion, Kind = KubeKind, PluralName = KubePlural)]
#if KUBEOPS
    [KubernetesEntityShortNames]
    [EntityScope(EntityScope.Cluster)]
    [Description("Defines a Minio bucket.")]
#endif
    public class V1MinioBucket : CustomKubernetesEntity<V1MinioBucket.V1MinioBucketSpec, V1MinioBucket.V1MinioBucketStatus>
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
        public const string KubeKind = "MinioBucket";

        /// <summary>
        /// Object plural name.
        /// </summary>
        public const string KubePlural = "miniobuckets";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public V1MinioBucket()
        {
            this.SetMetadata();
        }

        /// <summary>
        /// The node execute task specification.
        /// </summary>
        public class V1MinioBucketSpec
        {
            /// <summary>
            /// The Minio tenant where the bucket should be created.
            /// </summary>
            public string Tenant { get; set; }

            /// <summary>
            /// The bucket Region.
            /// </summary>
            public string Region { get; set; }

            /// <summary>
            /// Allows to keep multiple versions of the same object under the same key.
            /// </summary>
            public VersioningMode Versioning { get; set; } = VersioningMode.Off;

            /// <summary>
            /// Prevents objects from being deleted. Required to support retention and legal hold. 
            /// Can only be enabled at bucket creation.
            /// </summary>
            public bool ObjectLocking { get; set; } = false;

            /// <summary>
            /// Optionally limits the amount of data in the bucket.
            /// </summary>
            public ResourceQuantity Quota { get; set; }
        }

        /// <summary>
        /// The minio bucket status.
        /// </summary>
        public class V1MinioBucketStatus
        {
            /// <summary>
            /// <see cref="DateTime"/>.
            /// </summary>
            public DateTime? Timestamp { get; set; }
        }
    }

    /// <summary>
    /// Imposes rules to prevent object deletion for a period of time.
    /// </summary>
    public class RetentionSpec
    {
        /// <summary>
        /// The <see cref="RetentionMode"/>.
        /// </summary>
        public RetentionMode Mode { get; set; }

        /// <summary>
        /// The retention period in days.
        /// </summary>
        public long Validity { get; set; }
    }

    /// <summary>
    /// The bucket retention mode.
    /// </summary>
    public enum RetentionMode
    {
        [EnumMember(Value = "COMPLIANCE")]
        Compliance,

        [EnumMember(Value = "GOVERNANCE")]
        Governance
    }

    /// <summary>
    /// The bucket versioning mode.
    /// </summary>
    public enum VersioningMode
    {

        [EnumMember(Value = "Off")]
        Off,

        [EnumMember(Value = "Enabled")]
        Enabled,

        [EnumMember(Value = "Suspended")]
        Suspended
    }
}
