//-----------------------------------------------------------------------------
// FILE:	    CatchAllException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Diagnostics.Contracts;

namespace Neon.Common
{
    /// <summary>
    /// <para>
    /// Thrown by <see cref="ExceptionResult.ThrowOnError"/> and <see cref="ExceptionResult{TResult}"/>
    /// when the type for the exception identified by <see cref="ExceptionResult.ExceptionType"/> doesn't
    /// exist in the current <see cref="AppDomain"/>.
    /// </para>
    /// <para>
    /// This can happen when a remote process throws an exception from an assembly it references but
    /// is not referenced by the calling process.  In these cases, you'll need to catch this exception
    /// and then examine the <see cref="ExceptionType"/> property to identify the exception and potentially
    /// the <see cref="Exception.Message"/> property as well.
    /// </para>
    /// </summary>
    public class CatchAllException : Exception
    {
        /// <summary>
        /// Constructs an instance from the exception passed.
        /// </summary>
        /// <param name="e">The exception being wrapped.</param>
        public CatchAllException(Exception e)
            : base(e.Message)
        {
            this.ExceptionType = e.GetType().FullName;
        }

        /// <summary>
        /// Constructs an instance from an exception type name and an optional message.
        /// </summary>
        /// <param name="exceptionType">The fully qualified exception type name.</param>
        /// <param name="message">The optional exception message.</param>
        public CatchAllException(string exceptionType, string message = null)
            : base(message)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(exceptionType), nameof(exceptionType));

            this.ExceptionType = exceptionType;
        }

        /// <summary>
        /// Returns the fully qualified name of the wrapped exception type.
        /// </summary>
        public string ExceptionType { get; internal set; }
    }
}
