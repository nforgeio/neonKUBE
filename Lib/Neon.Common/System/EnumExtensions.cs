//-----------------------------------------------------------------------------
// FILE:	    EnumExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
