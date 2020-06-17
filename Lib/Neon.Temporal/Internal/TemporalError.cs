//-----------------------------------------------------------------------------
// FILE:	    WorkflowIDReusePolicy.cs
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Text;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Temporal;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Describes a Temporal error.
    /// </summary>
    internal class TemporalError
    {
        //---------------------------------------------------------------------
        // Static members

        private static Dictionary<string, ConstructorInfo> goErrorToConstructor;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static TemporalError()
        {
            // Initialize a dictionary that maps GOLANG error strings to TemporalError
            // derived exception constructors.  These constructors must have signatures
            // like:
            //
            //      TemporalException(string message, Exception innerException)
            //
            // Note that we need to actually construct a temporary instance of each exception
            // type so that we can retrieve the corresponding GOLANG error string.

            goErrorToConstructor = new Dictionary<string, ConstructorInfo>();

            var temporalExceptionType = typeof(TemporalException);

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (!temporalExceptionType.IsAssignableFrom(type))
                {
                    // Ignore everything besides [TemporalException] derived types.

                    continue;
                }

                if (type.IsAbstract)
                {
                    // Ignore [TemporalException] itself.

                    continue;
                }

                var constructor = type.GetConstructor(new Type[] { typeof(string), typeof(Exception) });

                if (constructor == null)
                {
                    throw new Exception($"Type [{type.Name}:{temporalExceptionType.Name}] does not have a constructor like: [{type.Name}(string, Exception)].");
                }

                var exception = (TemporalException)constructor.Invoke(new object[] { string.Empty, null });

                if (exception.TemporalError == null)
                {
                    // The exception doesn't map to a GOLANG error.

                    continue;
                }

                goErrorToConstructor.Add(exception.TemporalError, constructor);
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public TemporalError()
        {
        }

        /// <summary>
        /// Constructs an error from parameters.
        /// </summary>
        /// <param name="error">The GOLANG error string.</param>
        /// <param name="type">Optionally specifies the error type.</param>
        public TemporalError(string error, TemporalErrorTypes type = TemporalErrorTypes.Custom)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(error), nameof(error));

            this.String = error;
            this.SetErrorType(type);
        }

        /// <summary>
        /// Constructs an error from a .NET exception.
        /// </summary>
        /// <param name="e">The exception.</param>
        public TemporalError(Exception e)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));

            this.String = $"{e.GetType().FullName}{{{e.Message}}}";
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
        internal TemporalErrorTypes GetErrorType()
        {
            switch (Type)
            {
                case "cancelled":   return TemporalErrorTypes.Cancelled;
                case "custom":      return TemporalErrorTypes.Custom;
                case "generic":     return TemporalErrorTypes.Generic;
                case "panic":       return TemporalErrorTypes.Panic;
                case "terminated":  return TemporalErrorTypes.Terminated;
                case "timeout":     return TemporalErrorTypes.Timeout;

                default:

                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Sets the error type.
        /// </summary>
        /// <param name="type">The new type.</param>
        internal void SetErrorType(TemporalErrorTypes type)
        {
            switch (type)
            {
                case TemporalErrorTypes.Cancelled:  Type = "cancelled";   break;
                case TemporalErrorTypes.Custom:     Type = "custom";      break;
                case TemporalErrorTypes.Generic:    Type = "generic";     break;
                case TemporalErrorTypes.Panic:      Type = "panic";       break;
                case TemporalErrorTypes.Terminated: Type = "terminated";  break;
                case TemporalErrorTypes.Timeout:    Type = "timeout";     break;

                default:

                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Converts the instance into an <see cref="TemporalException"/>.
        /// </summary>
        /// <returns>One of the exceptions derived from <see cref="TemporalException"/>.</returns>
        public TemporalException ToException()
        {
            // $note(jefflill):
            //
            // We're depending on Temporal error strings looking like this:
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

            // We're going to save the error in the exception [Message] property and
            // save the message to the [Reasons] property and then  we'll encode the
            // error as the exception message.  This seems a bit confusing but that's
            // the way we're doing it.

            var details = message;

            message = error;

            // First, we're going to try mapping the error identifier to one of the
            // predefined Temporal exceptions and if that doesn't work, we'll generate
            // a more generic exception.

            if (goErrorToConstructor.TryGetValue(error, out var constructor))
            {
                var e = (TemporalException)constructor.Invoke(new object[] { error, null });

                e.Details = details;

                return e;
            }

            var errorType = GetErrorType();

            switch (errorType)
            {
                case TemporalErrorTypes.Cancelled:

                    return new CancelledException(message) { Details = details };

                case TemporalErrorTypes.Custom:

                    return new TemporalCustomException(message) { Details = details };

                case TemporalErrorTypes.Generic:

                    return new TemporalGenericException(message) { Details = details };

                case TemporalErrorTypes.Panic:

                    return new TemporalPanicException(message) { Details = details };

                case TemporalErrorTypes.Terminated:

                    return new TerminatedException(message) { Details = details };

                case TemporalErrorTypes.Timeout:

                    // Special case some timeout exceptions.

                    switch (message)
                    {
                        case "TimeoutType: START_TO_CLOSE":

                            return new StartToCloseTimeoutException();

                        case "TimeoutType: HEARTBEAT":

                            return new ActivityHeartbeatTimeoutException();
                    }

                    return new TemporalTimeoutException(message);

                default:

                    throw new NotImplementedException($"Unexpected Temporal error type [{errorType}].");
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return String;
        }
    }
}
