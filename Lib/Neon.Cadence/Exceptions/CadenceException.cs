//-----------------------------------------------------------------------------
// FILE:	    CadenceException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

namespace Neon.Cadence
{
    /// <summary>
    /// Base class for all Cadence related exceptions.
    /// </summary>
    public class CadenceException : Exception
    {
        //-------------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Constructs a <see cref="CadenceException"/> corresponding to the Cadence
        /// error type and message passed.
        /// </summary>
        /// <param name="errorType">Identifies the error type.</param>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">Optional inner exception.</param>
        /// <returns>One of the exceptions derived from <see cref="CadenceException"/>.</returns>
        internal static CadenceException Create(CadenceErrorTypes errorType, string message, Exception innerException = null)
        {
            Covenant.Requires<ArgumentNullException>(message != null);

            // First, we're going to try mapping the message string to predefined
            // Cadence exceptions and if that doesn't work, we'll generate a more
            // generic exception.

            switch (message)
            {
                case "BadRequestError":

                    return new CadenceBadRequestException(message);

                case "DomainAlreadyExistsError":

                    return new CadenceDomainAlreadyExistsException(message);

                case "EntityNotExistsError":

                    return new CadenceEntityNotExistsException(message);

                case "InternalServiceError":

                    return new CadenceInternalServiceException(message);
            }

            // Create a more generic exception.

            switch (errorType)
            {
                case CadenceErrorTypes.Cancelled:

                    return new CadenceCancelledException(message, innerException);

                case CadenceErrorTypes.Custom:

                    return new CadenceCustomException(message, innerException);

                case CadenceErrorTypes.Generic:

                    return new CadenceGenericException(message, innerException);

                case CadenceErrorTypes.Panic:

                    return new CadencePanicException(message, innerException);

                case CadenceErrorTypes.Terminated:

                    return new CadenceTerminatedException(message, innerException);

                case CadenceErrorTypes.Timeout:

                    return new CadenceTimeoutException(message, innerException);

                case CadenceErrorTypes.None:
                default:

                    throw new NotImplementedException($"Unexpected Cadence error type [{errorType}].");
            }
        }

        //-------------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">Optionally specifies the inner exception.</param>
        public CadenceException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Returns the Cadence GOLANG client's error string corresponding to the
        /// exception or <c>null</c> when the exception does not map to an
        /// error string.
        /// </summary>
        internal virtual string CadenceError => null;
    }
}
