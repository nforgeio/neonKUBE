//-----------------------------------------------------------------------------
// FILE:        ToolException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using Neon.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Common
{
    /// <summary>
    /// Typically thrown when a tool or subprocess is executed an fails.
    /// </summary>
    public class ToolException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The optional exception message.</param>
        /// <param name="inner">The optional inner exception.</param>
        public ToolException(string message = null, Exception inner = null)
            : base(message, inner)
        {
        }
    }
}
