//-----------------------------------------------------------------------------
// FILE:	    TemporalException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Base class for all Temporal related exceptions.
    /// </summary>
    public abstract class TemporalException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">Optionally specifies a message.</param>
        /// <param name="innerException">Optionally specifies the inner exception.</param>
        public TemporalException(string message = null, Exception innerException = null)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Returns Temporal GOLANG client's error string corresponding to the
        /// exception or <c>null</c> when the exception does not map to an
        /// error string.
        /// </summary>
        internal virtual string TemporalError => null;

        /// <summary>
        /// Returns the Temporal error type.
        /// </summary>
        internal abstract TemporalErrorTypes TemporalErrorType { get; }

        /// <summary>
        /// Returns the Temporal error reason used for specifying non-retryable errors
        /// for a <see cref="RetryOptions"/> instance.
        /// </summary>
        internal abstract string Reason { get; }

        /// <summary>
        /// Returns the additional details about the exception.
        /// </summary>
        public string Details { get; internal set; }

        /// <summary>
        /// Converts the exception into a <see cref="TemporalError"/>.
        /// </summary>
        /// <returns>The <see cref="TemporalError"/>.</returns>
        internal virtual TemporalError ToTemporalError()
        {
            return new TemporalError($"{TemporalError}{{{Message}}}", TemporalErrorType);
        }
    }
}
