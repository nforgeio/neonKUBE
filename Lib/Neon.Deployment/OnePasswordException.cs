//-----------------------------------------------------------------------------
// FILE:	    OnePasswordException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
//
// The contents of this repository are for private use by neonFORGE, LLC. and may not be
// divulged or used for any purpose by other organizations or individuals without a
// formal written and signed agreement with neonFORGE, LLC.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Deployment
{
    /// <summary>
    /// Thrown by the <see cref="OnePassword"/> for errors.
    /// </summary>
    public class OnePasswordException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">Optionally specifies the exception message.</param>
        /// <param name="innerException">Optionally specifies an inner exception.</param>
        public OnePasswordException(string message = null, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}
