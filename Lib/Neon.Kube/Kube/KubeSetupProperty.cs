//-----------------------------------------------------------------------------
// FILE:        KubeSetupProperty.cs
// CONTRIBUTOR: Jeff Lill
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

using Neon.Common;
using Neon.Kube.ClusterDef;
using Neon.Kube.Hosting;
using Neon.Kube.Proxy;
using Neon.Kube.Setup;

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
        /// Property name for accessing a <c>bool</c> that indicates that we're preparing a cluster vs.
        /// setting one up.
        /// </para>
        /// </summary>
        public const string Preparing = "preparing";

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
        public const string DebugMode = "debug-mode";

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
        /// Property name for determining the current hosting environment: <see cref="HostingEnvironment"/>,
        /// </summary>
        public const string HostingEnvironment = "hosting-environment";

        /// <summary>
        /// Property name for accessing the <see cref="SetupController{NodeMetadata}"/>'s <see cref="ClusterProxy"/> property.
        /// </summary>
        public const string ClusterProxy = "cluster-proxy";

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

        /// <summary>
        /// Property name for accessing the NEONCLOUD headend service client.
        /// </summary>
        public const string NeonCloudHeadendClient = "neoncloud-headend-client";

        /// <summary>
        /// Property name for a boolean indicating that the node image has already been downloaded
        /// (e.g. by NEONDESKTOP) and does not need to be downloaded hosting managers during cluster
        /// provisioning.  Image downloading should be considered to be enabled when this property
        /// is not present.
        /// </summary>
        public const string DisableImageDownload = "image-download-disabled";

        /// <summary>
        /// Property name for a <c>bool</c> indicating whether secrets should be redacted when logging
        /// during cluster setup.  This should be generally set to <c>true</c> for production
        /// deployments.
        /// </summary>
        public const string Redact = "redact";

        /// <summary>
        /// <para>
        /// Property name for a <see cref="Credentials"/> object holding the username and password
        /// to be used to authenticate <b>podman</b> on the cluster node with the local Harbor
        /// registry.
        /// </para>
        /// <note>
        /// Token based credentials are not supported.
        /// </note>
        /// </summary>
        public const string HarborCredentials = "harbor-credentials";

        /// <summary>
        /// Property name for a <c>bool</c> value indicating that we're <b>building</b> a
        /// ready-to-go desktop image.
        /// </summary>
        public const string BuildDesktopImage = "build-desktop-image";

        /// <summary>
        /// Property name for a <c>bool</c> value indicating whether we're <b>deploying</b>
        /// a cluster using a ready-to-go desktop image.
        /// </summary>
        public const string DesktopReadyToGo = "desktop-readytogo";

        /// <summary>
        /// Property name for a <see cref="DesktopServiceProxy"/> instance that can be used to perform
        /// specific privileged operations from a non-privileged process.
        /// </summary>
        public const string DesktopServiceProxy = "desktop-service-proxy";

        /// <summary>
        /// <para>
        /// Property name for a <c>bool</c> value indicating we should not secure the cluster
        /// nodes with a generated secure password and also that we should allow SSH password
        /// authentication, for cluster debugging purposes.
        /// </para>
        /// <note>
        /// <b>WARNING!</b> This should never be used for production clusters.
        /// </note>
        /// </summary>
        public const string Insecure = "insecure";
    }
}
