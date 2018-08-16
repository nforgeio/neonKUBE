//-----------------------------------------------------------------------------
// FILE:	    HyperVException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;

namespace Neon.HyperV
{
    /// <summary>
    /// Thrown by <see cref="HyperVClient"/> when an error is detected.
    /// </summary>
    public class HyperVException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">Optionally specifies an inner exception.</param>
        public HyperVException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}
