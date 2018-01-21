//-----------------------------------------------------------------------------
// FILE:	    ConflictException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.DynamicData;

namespace Couchbase.Lite
{
    /// <summary>
    /// Reports a document conflict error from <see cref="EntityDocument{TEntity}.Save(ConflictPolicy)"/>.
    /// </summary>
    public class ConflictException : Exception
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ConflictException()
            : base("Conflict")
        {
        }

        /// <summary>
        /// Constructs an exception with a message.
        /// </summary>
        /// <param name="message">The message.</param>
        public ConflictException(string message)
            : base(message)
        {
        }
    }
}
