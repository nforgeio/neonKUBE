//-----------------------------------------------------------------------------
// FILE:	    KubeSetupProperty.cs
// CONTRIBUTOR: Jeff Lill
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

using k8s;

namespace Neon.Kube
{
    /// <summary>
    /// Identifies the cluster setup state available in an <see cref="ISetupController"/>.
    /// </summary>
    public static class KubeSetupProperty
    {
        /// <summary>
        /// <para>
        /// Property name for accessing a <c>bool</c> that indicates that we're running cluster prepare/setup in <b>debug mode</b>.
        /// In debug mode, setup works like it did in the past, where we deployed the base node image first and then 
        /// configured the node from that, rather than starting with the node image with assets already prepositioned.
        /// </para>
        /// <para>
        /// This mode is useful when debugging cluster setup or adding new features.
        /// </para>
        /// </summary>
        public const string DebugMode = "debug-setup";

        /// <summary>
        /// <para>
        /// Property name for accessing a <c>bool</c> that indicates that we're running cluster prepare/setup in <b>release mode</b>.
        /// </para>
        /// </summary>
        public const string ReleaseMode = "release-setup";

        /// <summary>
        /// <para>
        /// Property name for accessing a <c>bool</c> that indicates that we're running cluster prepare/setup in <b>maintainer mode</b>.
        /// </para>
        /// </summary>
        public const string MaintainerMode = "maintainer-setup";

        /// <summary>
        /// Property name for a <c>bool</c> that identifies the base image name to be used for preparing
        /// a cluster in <b>debug mode</b>.  This is the name of the base image file as persisted to our
        /// public S3 bucket.  This will not be set for cluster setup.
        /// </summary>
        public const string BaseImageName = "base-image-name";

        /// <summary>
        /// Property name for determining the current hosting environment: <see cref="Kube.HostingEnvironment"/>,
        /// </summary>
        public const string HostingEnvironment = "hosting-environment";

        /// <summary>
        /// Property name for accessing the <see cref="SetupController{NodeMetadata}"/>'s <see cref="Kube.ClusterProxy"/> property.
        /// </summary>
        public const string ClusterProxy = "cluster-proxy";

        /// <summary>
        /// Property name for accessing the <see cref="SetupController{NodeMetadata}"/>'s <see cref="Kube.ClusterLogin"/> property.
        /// </summary>
        public const string ClusterLogin = "cluster-login";

        /// <summary>
        /// Property name for accessing the <see cref="SetupController{NodeMetadata}"/>'s <see cref="IHostingManager"/> property.
        /// </summary>
        public const string HostingManager = "hosting-manager";

        /// <summary>
        /// Property name for accessing the <see cref="SetupController{NodeMetadata}"/>'s <see cref="Kubernetes"/> client property.
        /// </summary>
        public const string K8sClient = "k8sclient";

        /// <summary>
        /// Property name for accessing the <see cref="SetupController{NodeMetadata}"/>'s <see cref="KubeClusterAdvice"/> client property.
        /// </summary>
        public const string ClusterAdvice = "setup-advice";
    }
}
