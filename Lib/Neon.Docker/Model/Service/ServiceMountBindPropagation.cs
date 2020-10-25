//-----------------------------------------------------------------------------
// FILE:	    ServiceMountBindPropagation.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Docker
{
    /// <summary>
    /// Enumerates the mount propagation options.
    /// </summary>
    public enum ServiceMountBindPropagation
    {
        /// <summary>
        /// RPrivate.
        /// </summary>
        [EnumMember(Value = "rprivate")]
        RPrivate = 0,

        /// <summary>
        /// Shared.
        /// </summary>
        [EnumMember(Value = "shared")]
        Shared,

        /// <summary>
        /// Slave.
        /// </summary>
        [EnumMember(Value = "slave")]
        Slave,

        /// <summary>
        /// Private.
        /// </summary>
        [EnumMember(Value = "private")]
        Private,

        /// <summary>
        /// RShared.
        /// </summary>
        [EnumMember(Value = "rshared")]
        RShared,

        /// <summary>
        /// RSlave.
        /// </summary>
        [EnumMember(Value = "rslave")]
        RSlave
    }
}
