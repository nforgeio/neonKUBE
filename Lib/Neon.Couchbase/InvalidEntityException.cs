//-----------------------------------------------------------------------------
// FILE:	    InvalidEntityException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.ComponentModel;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Data
{
    /// <summary>
    /// Thrown by <see cref="IEntity.Normalize()"/> implementations when the entity
    /// has invalid property values.
    /// </summary>
    public class InvalidEntityException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="inner">The optional inner exception.</param>
        public InvalidEntityException(string message, Exception inner = null)
            : base(message, inner)
        {
        }
    }
}
