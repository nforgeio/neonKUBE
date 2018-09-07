//-----------------------------------------------------------------------------
// FILE:	    NetworkException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Net
{
    /// <summary>
    /// Indicates network related problems.
    /// </summary>
    public class NetworkException : Exception
    {
        /// <summary>
        /// Constuctor.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="inner">The optional inner exception.</param>
        public NetworkException(string message, Exception inner = null)
            : base(message, inner)
        {
        }
    }
}
