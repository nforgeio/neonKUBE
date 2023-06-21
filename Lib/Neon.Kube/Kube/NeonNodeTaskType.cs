//-----------------------------------------------------------------------------
// FILE:        NeonNodeTaskType.cs
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

namespace Neon.Kube
{
    /// <summary>
    /// Defines node task types.
    /// </summary>
    public static class NeonNodeTaskType
    {
        /// <summary>
        /// Node task to check the expiration of the cluster control plane certificates.
        /// </summary>
        public const string ControlPlaneCertExpirationCheck = "control-plane-cert-expiration-check";

        /// <summary>
        /// Node task to update control plane certificates.
        /// </summary>
        public const string ControlPlaneCertUpdate = "control-plane-cert-update";

        /// <summary>
        /// Node task to update node CA certificates.
        /// </summary>
        public const string NodeCaCertUpdate = "node-ca-cert-update";

        /// <summary>
        /// Node task to sync container images to the cluster registry.
        /// </summary>
        public const string ContainerImageSync = "container-image-sync";
    }
}
