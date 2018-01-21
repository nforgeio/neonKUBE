//-----------------------------------------------------------------------------
// FILE:	    NeonPropertyNames.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.DynamicData;

namespace Couchbase.Lite
{
    /// <summary>
    /// Defines the property names used by the Neon Couchbase Lite document
    /// extensions.
    /// </summary>
    /// <remarks>
    /// This class defines the property names used for <see cref="EntityDocument{TEntity}"/>
    /// related values.  By default, these names all have leading plus signs (<b>+</b>) and
    /// are abbreviated to help reduce the network, memory and disk footprint of documents.
    /// </remarks>
    public static class NeonPropertyNames
    {
        // WARNING:
        //
        // Ensure that the property name definitions below do not conflict with
        // the Entity property names

            /// <summary>
        /// <b>Content</b> property name.  Defaults to <b>"+c"</b>.
        /// </summary>
        public const string Content = "+c";

        /// <summary>
        /// <b>Channels</b> property name.  Defaults to <b>"+ch"</b>.
        /// </summary>
        public const string Channels = "+ch";

        /// <summary>
        /// <b>Type</b> property name.  Defaults to <b>"+t"</b>.
        /// </summary>
        public const string Type = "+t";

        /// <summary>
        /// <b>Timestamp</b> property name.  Defaults to <b>"+ts"</b>.
        /// </summary>
        public const string Timestamp = "+ts";
    }
}
