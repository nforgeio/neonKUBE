//-----------------------------------------------------------------------------
// FILE:	    KubeClusterspaceMode.cs
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

namespace Neon.Kube
{
    /// <summary>
    /// Used to enable/disable cluster clusterspace mode to allow multiple deployments
    /// to be managed in parallel on a client workstation without conflict.
    /// </summary>
    public enum KubeClusterspaceMode
    {
        /// <summary>
        /// Disables clusterspace mode such that cluster state will be persisted to the
        /// normal default user folders.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Enables clusterspaces such that cluster state will be persisted to a non
        /// standard folder to avoid conflicts.
        /// </summary>
        Enabled,

        /// <summary>
        /// Enables clusterspaces like <see cref="Enabled"/> while still using the standard
        /// image cache folder to improve performance by avoiding unncessary image downloads.
        /// </summary>
        EnabledWithSharedCache
    }
}
