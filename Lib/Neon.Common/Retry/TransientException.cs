//-----------------------------------------------------------------------------
// FILE:	    TransientException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Retry
{
    /// <summary>
    /// Used to indicate an explicit transient error.
    /// </summary>
    public class TransientException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">Optional inner exception.</param>
        public TransientException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}
