//-----------------------------------------------------------------------------
// FILE:	    StringBuilderExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Text
{
    /// <summary>
    /// System class extensions.
    /// </summary>
    public static class StringBuilderExtensions
    {
        //---------------------------------------------------------------------
        // StringBuilder extensions

        /// <summary>
        /// Appends a line of text using a Linux-style (LF) line ending.
        /// </summary>
        /// <param name="sb">The <see cref="StringBuilder"/>.</param>
        /// <param name="line">The line.</param>
        public static void AppendLineLinux(this StringBuilder sb, string line = null)
        {
            Covenant.Requires<ArgumentNullException>(sb != null);

            if (line != null)
            {
                sb.Append(line);
            }

            sb.Append('\n');
        }

        /// <summary>
        /// Appends non-<c>null</c> and non-empty text, separating it from any existing text with a string.
        /// </summary>
        /// <param name="sb">The <see cref="StringBuilder"/>.</param>
        /// <param name="text">The text to be appended.</param>
        /// <param name="separator">The separator string, this defaults to a single space.</param>
        /// <remarks>
        /// <note>
        /// The separator string will not be appended if <paramref name="text"/> is <c>null</c>
        /// or empty.
        /// </note>
        /// </remarks>
        public static void AppendWithSeparator(this StringBuilder sb, string text, string separator = " ")
        {
            Covenant.Requires<ArgumentNullException>(separator != null);

            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (sb.Length > 0)
            {
                sb.Append(separator);
            }

            sb.Append(text);
        }
    }
}