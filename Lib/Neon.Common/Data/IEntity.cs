//-----------------------------------------------------------------------------
// FILE:	    Entity.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.ComponentModel;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Data
{
    /// <summary>
    /// Interface describing an entity.
    /// </summary>
    public interface IEntity<T>
        where T : class, new()
    {
        /// <summary>
        /// Can be overridden by derived entities to return the Couchbase key to be used to
        /// persist the entity.  The base implementation throws a <see cref="NotSupportedException"/>.
        /// </summary>
        /// <returns>The Couchbase key for the entity.</returns>
        string GetKey();

        /// <summary>
        /// Identifies the entity type.
        /// </summary>
        string Type { get; set; }

        /// <summary>
        /// Tests this instance against another for equality.
        /// </summary>
        /// <param name="other">The other instance.</param>
        /// <returns><c>true</c> if the instances are equal.</returns>
        bool Equals(T other);
    }
}
