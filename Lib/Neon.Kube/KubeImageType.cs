//-----------------------------------------------------------------------------
// FILE:	    KubeImageType.cs
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
using System.Runtime.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Enumerates the neonKUBE image types.
    /// </summary>
    public enum KubeImageType
    {
        /// <summary>
        /// Indicates an unknown image types like base or virgin images.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown = 0,

        /// <summary>
        /// Indicates a node image that can be used to deploy a multi-node
        /// cluster.
        /// </summary>
        [EnumMember(Value = "node")]
        Node,

        /// <summary>
        /// Indicates a node image that has completed nearly all setup for
        /// a single node cluster.  This is used for neonDESKTOP built-in
        /// clusters.
        /// </summary>
        [EnumMember(Value = "ready-to-go")]
        ReadToGo
    }
}
