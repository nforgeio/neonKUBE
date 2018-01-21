//-----------------------------------------------------------------------------
// FILE:	    LineEnding.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Neon.IO
{
    /// <summary>
    /// Enumerates the possible line ending modes.
    /// </summary>
    public enum LineEnding
    {
        /// <summary>
        /// Use platform specific line endings.
        /// </summary>
        Platform = 0,

        /// <summary>
        /// Windows style line endings using carriage return and line feed characters.
        /// </summary>
        CRLF,

        /// <summary>
        /// Unix/Linux style line endings using just a line feed.
        /// </summary>
        LF
    }
}
