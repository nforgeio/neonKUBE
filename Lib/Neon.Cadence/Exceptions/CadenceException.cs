//-----------------------------------------------------------------------------
// FILE:	    CadenceException.cs
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

using Neon.Cadence;
using Neon.Cadence.Internal;

namespace Neon.Cadence
{
    /// <summary>
    /// Base class for all Cadence related exceptions.
    /// </summary>
    public abstract class CadenceException : Exception
    {
        private string reason;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">Optionally specifies a message.</param>
        /// <param name="innerException">Optionally specifies the inner exception.</param>
        public CadenceException(string message = null, Exception innerException = null)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Returns the Cadence GOLANG client's error string corresponding to the
        /// exception or <c>null</c> when the exception does not map directly to a
        /// Cadence error string.
        /// </summary>
        internal virtual string CadenceError => null;

        /// <summary>
        /// Returns the Cadence error type.
        /// </summary>
        internal abstract CadenceErrorType CadenceErrorType { get; }

        /// <summary>
        /// The Cadence error reason used for specifying non-retryable errors
        /// within a <see cref="RetryOptions"/> instance.
        /// </summary>
        internal string Reason
        {
            get => reason ?? CadenceError;
            set => reason = value;
        }

        /// <summary>
        /// Converts the exception into a <see cref="CadenceError"/>.
        /// </summary>
        /// <returns>The <see cref="CadenceError"/>.</returns>
        internal virtual CadenceError ToCadenceError()
        {
            return new CadenceError($"{CadenceError}{{{Message}}}", CadenceErrorType);
        }
    }
}
