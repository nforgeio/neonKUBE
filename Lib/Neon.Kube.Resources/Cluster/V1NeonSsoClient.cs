//-----------------------------------------------------------------------------
// FILE:        V1NeonSsoClient.cs
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
using System.Text;

using k8s;
using k8s.Models;

using NJsonSchema;

namespace Neon.Kube.Resources.Cluster
{
    /// <summary>
    /// Specifies Neon SSO client settings.
    /// </summary>
    [KubernetesEntity(Group = KubeGroup, ApiVersion = KubeApiVersion, Kind = KubeKind, PluralName = KubePlural)]
    [EntityScope(EntityScope.Cluster)]
    public class V1NeonSsoClient : IKubernetesObject<V1ObjectMeta>, ISpec<V1SsoClientSpec>, IStatus<V1SsoClientStatus>
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
        public const string KubeKind = "NeonSsoClient";

        /// <summary>
        /// Object plural name.
        /// </summary>
        public const string KubePlural = "neonssoclients";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public V1NeonSsoClient()
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
        public V1SsoClientSpec Spec { get; set; }

        /// <summary>
        /// The status.
        /// </summary>
        public V1SsoClientStatus Status { get; set; }
    }

    /// <summary>
    /// The SSO client specification.
    /// </summary>
    public class V1SsoClientSpec
    {
        /// <summary>
        /// The client ID used to identify the client.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The client Secret used to identify the client.
        /// </summary>
        public string Secret { get; set; }

        /// <summary>
        /// A registered set of redirect URIs. When redirecting from dex to the client, the URI
        /// requested to redirect to MUST match one of these values, unless the client is "public".
        /// </summary>
        public List<string> RedirectUris { get; set; }

        /// <summary>
        /// TrustedPeers are a list of peers which can issue tokens on this client's behalf using
        /// the dynamic "oauth2:server:client_id:(client_id)" scope. If a peer makes such a request,
        /// this client's ID will appear as the ID Token's audience.
        ///
        /// Clients inherently trust themselves.
        /// </summary>
        public List<string> TrustedPeers { get; set; }

        /// <summary>
        /// Public clients must use either use a redirectURL 127.0.0.1:X or "urn:ietf:wg:oauth:2.0:oob"
        /// </summary>
        public bool Public { get; set; }

        /// <summary>
        /// Name used when displaying this client to the end user.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Logo used when displaying this client to the end user.
        /// </summary>
        public string LogoUrl { get; set; }
    }

    /// <summary>
    /// The status.
    /// </summary>
    public class V1SsoClientStatus
    {
        /// <summary>
        /// Conditions.
        /// </summary>
        public List<V1Condition> Conditions { get; set; } = new List<V1Condition>();

        /// <summary>
        /// The state.
        /// </summary>
        public string State { get; set; }
    }
}
