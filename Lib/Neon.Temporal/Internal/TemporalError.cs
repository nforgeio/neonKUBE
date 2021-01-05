//-----------------------------------------------------------------------------
// FILE:	    WorkflowIDReusePolicy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using YamlDotNet.Serialization;

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
            // Initialize a dictionary that maps GOLANG error strings to TemporalException
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

        /// <summary>
        /// Converts an error type string into an <see cref="TemporalErrorType"/>.
        /// </summary>
        /// <param name="typeString">The error string to be converted.</param>
        /// <returns>The converted error type.</returns>
        internal static TemporalErrorType StringToErrorType(string typeString)
        {
            switch (typeString)
            {
                case "cancelled":   return TemporalErrorType.Cancelled;
                case "custom":      return TemporalErrorType.Custom;
                case "generic":     return TemporalErrorType.Generic;
                case "panic":       return TemporalErrorType.Panic;
                case "terminated":  return TemporalErrorType.Terminated;
                case "timeout":     return TemporalErrorType.Timeout;

                // $todo(jefflill): 
                //    
                // Temporal has refactored how errors work and we're now seeing other
                // strings.  I believe this is from the [temporal-proxy] but perhaps not.
                // We'll treat these as "custom" until we have a chance to refactor
                // error handling.

                default:

                    return TemporalErrorType.Custom;
                    //throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Converts an <see cref="TemporalErrorType"/> into a error string.
        /// </summary>
        /// <param name="type">the error type.</param>
        /// <returns>The error string.</returns>
        internal static string ErrorTypeToString(TemporalErrorType type)
        {
            switch (type)
            {
                case TemporalErrorType.Cancelled:   return "cancelled";
                case TemporalErrorType.Custom:      return "custom";
                case TemporalErrorType.Generic:     return "generic";
                case TemporalErrorType.Panic:       return "panic";
                case TemporalErrorType.Terminated:  return "terminated";
                case TemporalErrorType.Timeout:     return "timeout";

                default:

                    throw new NotImplementedException();
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
        /// <param name="type">Optionally specifies the error type. This defaults to <see cref="TemporalErrorType.Generic"/>.</param>
        public TemporalError(string error, TemporalErrorType type = TemporalErrorType.Generic)
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

            var temporalException = e as TemporalException;

            if (temporalException != null)
            {
                this.Type = ErrorTypeToString(temporalException.TemporalErrorType);
            }
            else
            {
                this.Type = "custom";
            }
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
        internal TemporalErrorType GetErrorType()
        {
            return StringToErrorType(Type);
        }

        /// <summary>
        /// Sets the error type.
        /// </summary>
        /// <param name="type">The new type.</param>
        internal void SetErrorType(TemporalErrorType type)
        {
            Type = ErrorTypeToString(type);
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
            //      REASON{MESSAGE}
            //
            // where:
            //
            //      REASON      - identifies the error
            //      MESSAGE     - describes the error in more detail
            //
            // For robustness, we'll also handle the situation where there
            // is no {MESSAGE} part.

            string reason;
            string message;

            var startingBracePos = String.IndexOf('{');
            var endingBracePos   = String.LastIndexOf('}');

            if (startingBracePos != -1 && endingBracePos != 1)
            {
                reason  = String.Substring(0, startingBracePos);
                message = String.Substring(startingBracePos + 1, endingBracePos - (startingBracePos + 1));
            }
            else
            {
                reason  = String;
                message = string.Empty;
            }

            // We're going to save the details as the exception [Message] property and
            // save the error to the [Reason] property.
            //
            // First, we're going to try mapping the error reason to one of the
            // predefined Cadence exceptions and if that doesn't work, we'll generate
            // a more generic exception.

            if (goErrorToConstructor.TryGetValue(reason, out var constructor))
            {
                var e = (TemporalException)constructor.Invoke(new object[] { reason, null });

                e.Reason = reason;

                return e;
            }

            var errorType = GetErrorType();

            switch (errorType)
            {
                case TemporalErrorType.Cancelled:

                    return new CancelledException(message) { Reason = reason };

                case TemporalErrorType.Custom:

                    return new TemporalCustomException(message) { Reason = reason };

                case TemporalErrorType.Generic:

                    return new TemporalGenericException(message) { Reason = reason };

                case TemporalErrorType.Panic:

                    return new TemporalPanicException(message) { Reason = reason };

                case TemporalErrorType.Terminated:

                    return new TerminatedException(message) { Reason = reason };

                case TemporalErrorType.Timeout:

                    // Special case timeout exceptions.

                    switch (reason)
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
