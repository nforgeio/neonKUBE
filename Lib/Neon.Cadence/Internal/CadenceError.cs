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
using System.Reflection;
using System.Text;

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Describes a Cadence error.
    /// </summary>
    internal class CadenceError
    {
        //---------------------------------------------------------------------
        // Static members

        private static Dictionary<string, ConstructorInfo> goErrorToConstructor;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static CadenceError()
        {
            // Initialize a dictionary that maps GOLANG error strings to CadenceError
            // derived exception constructors.  These constructors must have signatures
            // like:
            //
            //      CadenceException(string message, Exception innerException)
            //
            // Note that we need to actually construct an instance of each exception
            // type so that we can retrieve the corresponding GOLANG error string.

            goErrorToConstructor = new Dictionary<string, ConstructorInfo>();

            var cadenceExceptionType = typeof(CadenceException);

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (!cadenceExceptionType.IsAssignableFrom(type))
                {
                    // Ignore everything besides [CadenceException] derived types.

                    continue;
                }

                if (type.IsAbstract)
                {
                    // Ignore [CadenceException] itself.

                    continue;
                }

                var constructor = type.GetConstructor(new Type[] { typeof(string), typeof(Exception) });

                if (constructor == null)
                {
                    throw new Exception($"Type [{type.Name}:{cadenceExceptionType.Name}] does not have a constructor like: [{type.Name}(string, Exception)].");
                }

                var exception = (CadenceException)constructor.Invoke(new object[] { string.Empty, null });

                if (exception.CadenceError == null)
                {
                    // The exception doesn't map to a GOLANG error.

                    continue;
                }

                goErrorToConstructor.Add(exception.CadenceError, constructor);
            }
        }

        //---------------------------------------------------------------------
        // Instance members

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
        public CadenceError(string error, CadenceErrorTypes type = CadenceErrorTypes.Custom)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(error));

            this.String = error;
            this.SetErrorType(type);
        }

        /// <summary>
        /// Constructs an error from a .NET exception.
        /// </summary>
        /// <param name="e">The exception.</param>
        public CadenceError(Exception e)
        {
            Covenant.Requires<ArgumentNullException>(e != null);

            this.String = $"{e.GetType().Name}{{{e.Message}}}";
            this.Type   = "custom";
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
        /// Converts the instance into an <see cref="CadenceException"/>.
        /// </summary>
        /// <returns>One of the exceptions derived from <see cref="CadenceException"/>.</returns>
        public CadenceException ToException()
        {
            // $note(jeff.lill):
            //
            // We're depending on cadence error strings looking like this:
            //
            //      ERROR{MESSAGE}
            //
            // where:
            //
            //      ERROR       - identifies the error
            //      MESSAGE     - describes the error in more detail
            //
            // For robustness, we'll also handle the situation where there
            // is no {MESSAGE} part.

            string error;
            string message;

            var startingBracePos = String.IndexOf('{');
            var endingBracePos   = String.LastIndexOf('}');

            if (startingBracePos != -1 && endingBracePos != 1)
            {
                error   = String.Substring(0, startingBracePos);
                message = String.Substring(startingBracePos + 1, endingBracePos - (startingBracePos + 1));
            }
            else
            {
                error   = String;
                message = string.Empty;
            }

            // First, we're going to try mapping the error identifier to one of the
            // predefined Cadence exceptions and if that doesn't work, we'll generate
            // a more generic exception.

            if (goErrorToConstructor.TryGetValue(error, out var constructor))
            {
                return (CadenceException)constructor.Invoke(new object[] { error, null });
            }

            // Create a more generic exception.

            if (!string.IsNullOrEmpty(message))
            {
                message = $"{error}: {message}";
            }
            else
            {
                message = error;
            }

            var errorType = GetErrorType();

            switch (errorType)
            {
                case CadenceErrorTypes.Cancelled:

                    return new CadenceCancelledException(message);

                case CadenceErrorTypes.Custom:

                    return new CadenceCustomException(message);

                case CadenceErrorTypes.Generic:

                    return new CadenceGenericException(message);

                case CadenceErrorTypes.Panic:

                    return new CadencePanicException(message);

                case CadenceErrorTypes.Terminated:

                    return new CadenceTerminatedException(message);

                case CadenceErrorTypes.Timeout:

                    return new CadenceTimeoutException(message);

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
