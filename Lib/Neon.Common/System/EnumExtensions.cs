//-----------------------------------------------------------------------------
// FILE:	    EnumExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace System
{
    /// <summary>
    /// <see cref="Enum"/> extensions.
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// Converts an enumeration value into a string, using the <see cref="EnumMemberAttribute"/>
        /// value if one was specified for the value in the enumeration type definition otherwise
        /// the default enumeration value name will be returned.
        /// </summary>
        /// <param name="value">The enumeration value to be converted.</param>
        /// <returns>The member or default string value.</returns>
        /// <remarks>
        /// This is useful because <see cref="Enum.ToString()"/> ignores any <see cref="EnumMemberAttribute"/>
        /// attributes.
        /// </remarks>
        public static string ToMemberString(this Enum value)
        {
            var type       = value.GetType();
            var info       = type.GetField(value.ToString());
            var attributes = (EnumMemberAttribute[])(info.GetCustomAttributes(typeof(EnumMemberAttribute), false));

            if (attributes.Length > 0)
                return attributes.First().Value;
            else
                return value.ToString();
        }
    }
}
