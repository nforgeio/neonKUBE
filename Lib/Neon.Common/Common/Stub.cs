//-----------------------------------------------------------------------------
// FILE:	    Stub.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;

namespace Neon.Common
{
    /// <summary>
    /// Used in situations where an innocous parameter is required to disambiguate
    /// constructor or method overloads.
    /// </summary>
    public static class Stub
    {
        /// <summary>
        /// An empty value structure.
        /// </summary>
        public struct Value
        {
        }

        /// <summary>
        /// Returns the stub value.
        /// </summary>
        public static readonly Value Param = new Value();
    }
}
