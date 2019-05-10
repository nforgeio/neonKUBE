//-----------------------------------------------------------------------------
// FILE:	    WorkflowIDReusePolicy.cs
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Cadence;
using Neon.Common;
using Neon.Retry;
using Neon.Time;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Describes a Cadence error.
    /// </summary>
    internal class CadenceError
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public CadenceError()
        {
        }

        /// <summary>
        /// Constructs an error from parameters.
        /// </summary>
        /// <param name="error">The GOLANG error string.</param>
        /// <param name="type">Optionally specifies the error type.</param>
        /// <param name="details">Optionally specifies additional error details.</param>
        public CadenceError(string error, CadenceErrorTypes type = CadenceErrorTypes.Custom, string details = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(error));

            this.String = error;
            this.SetErrorType(type);
            this.Details = details;
        }

        /// <summary>
        /// Specifies the GOLANG error string.
        /// </summary>
        [JsonProperty(PropertyName = "String", Required = Required.Always)]
        public string String { get; set; }

        /// <summary>
        /// Optionally specifies the GOLANG error type.
        /// </summary>
        [JsonProperty(PropertyName = "Type", Required = Required.Always)]
        public string Type { get; set; }

        /// <summary>
        /// Returns the error type.
        /// </summary>
        internal CadenceErrorTypes GetErrorType()
        {
            switch (Type)
            {
                case "cancelled":   return CadenceErrorTypes.Cancelled;
                case "custom":      return CadenceErrorTypes.Custom;
                case "generic":     return CadenceErrorTypes.Generic;
                case "panic":       return CadenceErrorTypes.Panic;
                case "terminated":  return CadenceErrorTypes.Terminated;
                case "timeout":     return CadenceErrorTypes.Timeout;

                default:

                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Sets the error type.
        /// </summary>
        /// <param name="type">The new type.</param>
        internal void SetErrorType(CadenceErrorTypes type)
        {
            string typeString;

            switch (type)
            {
                case CadenceErrorTypes.Cancelled:   typeString = "cancelled";   break;
                case CadenceErrorTypes.Custom:      typeString = "custom";      break;
                case CadenceErrorTypes.Generic:     typeString = "generic";     break;
                case CadenceErrorTypes.Panic:       typeString = "panic";       break;
                case CadenceErrorTypes.Terminated:  typeString = "terminated";  break;
                case CadenceErrorTypes.Timeout:     typeString = "timeout";     break;

                default:

                    throw new NotImplementedException();
            }

            Type = typeString;
        }

        /// <summary>
        /// Optionally specifies any additional error details.
        /// </summary>
        [JsonProperty(PropertyName = "Details", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public string Details { get; set; }

        /// <summary>
        /// Converts the instance into a <see cref="CadenceException"/>.
        /// </summary>
        /// <returns>One of the exceptions derived from <see cref="CadenceException"/>.</returns>
        public CadenceException ToException()
        {
            // First, we're going to try mapping the error identifier to one of the
            // predefined Cadence exceptions and if that doesn't work, we'll generate
            // a more generic exception.

            switch (String)
            {
                case "BadRequestError":

                    return new CadenceBadRequestException(Details);

                case "DomainAlreadyExistsError":

                    return new CadenceDomainAlreadyExistsException(Details);

                case "EntityNotExistsError":

                    return new CadenceEntityNotExistsException(Details);

                case "InternalServiceError":

                    return new CadenceInternalServiceException(Details);

                case "ServiceBusyError":

                    return new CadenceServiceBusyException(Details);
            }

            // Create a more generic exception.

            var errorType = GetErrorType();

            switch (errorType)
            {
                case CadenceErrorTypes.Cancelled:

                    return new CadenceCancelledException(Details);

                case CadenceErrorTypes.Custom:

                    return new CadenceCustomException(Details);

                case CadenceErrorTypes.Generic:

                    return new CadenceGenericException(Details);

                case CadenceErrorTypes.Panic:

                    return new CadencePanicException(Details);

                case CadenceErrorTypes.Terminated:

                    return new CadenceTerminatedException(Details);

                case CadenceErrorTypes.Timeout:

                    return new CadenceTimeoutException(Details);

                default:

                    throw new NotImplementedException($"Unexpected Cadence error type [{errorType}].");
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return String;
        }
    }
}
