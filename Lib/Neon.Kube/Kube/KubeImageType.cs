//-----------------------------------------------------------------------------
// FILE:	    KubeImageType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
        /// Identifies a virgin images.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown = 0,

        /// <summary>
        /// Identifies a base image.
        /// </summary>
        [EnumMember(Value = "base")]
        Base,

        /// <summary>
        /// Identifies the general purpose image that can be used to deploy 
        /// single or multi-node clusters.
        /// </summary>
        [EnumMember(Value = "node")]
        Node,

        /// <summary>
        /// Identifies a prebuilt built-in neondesktop cluster image.
        /// </summary>
        [EnumMember(Value = "desktop")]
        Desktop,
    }
}
