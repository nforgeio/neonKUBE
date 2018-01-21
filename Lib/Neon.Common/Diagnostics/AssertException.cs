//-----------------------------------------------------------------------------
// FILE:	    AssertException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Diagnostics
{
    /// <summary>
    /// Thrown by <see cref="Covenant.Assert(bool, string)"/> to signal logic failures.
    /// </summary>
    public class AssertException : Exception
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public AssertException()
            : base("Assertion Failed")
        {
        }

        /// <summary>
        /// Constructs an assertion with a specific message and optional inner exception.
        /// </summary>
        /// <param name="message">The custom message.</param>
        /// <param name="innerException">Optional inner exception.</param>
        public AssertException(string message, Exception innerException = null)
            : base("Assertion Failed: " + message, innerException)
        {
        }
    }
}
