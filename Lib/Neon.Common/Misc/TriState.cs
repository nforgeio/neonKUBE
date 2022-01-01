//-----------------------------------------------------------------------------
// FILE:	    TriState.cs
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
using System.Runtime.Serialization;

namespace Neon.Common
{
    /// <summary>
    /// Used to specify a tristate boolean with values: <b>true</b>, <b>false</b>, and <b>default</b>.
    /// </summary>
    public enum TriState
    {
        /// <summary>
        /// Specifies the default behavior.
        /// </summary>
        [EnumMember(Value = "default")]
        Default = 0,

        /// <summary>
        /// Specifies <c>false</c>.
        /// </summary>
        [EnumMember(Value = "false")]
        False,

        /// <summary>
        /// Specifies <c>true</c>.
        /// </summary>
        [EnumMember(Value = "true")]
        True
    }
}
