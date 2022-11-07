//-----------------------------------------------------------------------------
// FILE:	    ClusterPurpose.cs
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
using System.Runtime.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Enumerates the cluster purposes.
    /// </summary>
    public enum ClusterPurpose
    {
        /// <summary>
        /// Unspecified.
        /// </summary>
        [EnumMember(Value = "unspecified")]
        Unspecified = 0,

        /// <summary>
        /// Development environment.
        /// </summary>
        [EnumMember(Value = "development")]
        Development,

        /// <summary>
        /// Production environment.
        /// </summary>
        [EnumMember(Value = "production")]
        Production,

        /// <summary>
        /// Staging environment.
        /// </summary>
        [EnumMember(Value = "stage")]
        Stage,

        /// <summary>
        /// Test environment.
        /// </summary>
        [EnumMember(Value = "test")]
        Test
    }
}
