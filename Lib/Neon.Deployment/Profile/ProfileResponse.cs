//-----------------------------------------------------------------------------
// FILE:	    ProfileResponse.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Deployment
{
    /// <summary>
    /// Abstracts Neon Profile Service named pipe command responses.
    /// </summary>
    public class ProfileResponse : IProfileResponse
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Creates a successful command response with with a simple string value.
        /// </summary>
        /// <param name="value">The optional command arguments.</param>
        /// <returns>The <see cref="ProfileResponse"/>.</returns>
        public static ProfileResponse Create(string value)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(value), nameof(value));

            return new ProfileResponse()
            {
                Value = value
            };
        }

        /// <summary>
        /// Creates a successful command response with with a JSON value.
        /// </summary>
        /// <param name="jObject">The JSON value.</param>
        /// <returns>The <see cref="ProfileResponse"/>.</returns>
        public static ProfileResponse Create(JObject jObject)
        {
            Covenant.Requires<ArgumentNullException>(jObject != null, nameof(jObject));

            return new ProfileResponse()
            {
                JObject = jObject
            };
        }

        /// <summary>
        /// Creates a failed command response with an error message.
        /// </summary>
        /// <param name="status">The status code (one of the <see cref="ProfileStatus"/> values).</param>
        /// <param name="message">The error message.</param>
        /// <returns>The <see cref="ProfileResponse"/>.</returns>
        public static ProfileResponse CreateError(string status, string message)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(status), nameof(status));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(message), nameof(message));

            return new ProfileResponse()
            {
                Status = status,
                Error  = message
            };
        }

        /// <summary>
        /// Parses a request from a line of text read from the named pipe.
        /// </summary>
        /// <param name="responseLine">The response line.</param>
        /// <returns>The <see cref="ProfileResponse"/>.</returns>
        /// <exception cref="FormatException">Thrown for invalid response lines.</exception>
        public static ProfileResponse Parse(string responseLine)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(responseLine), nameof(responseLine));

            var colonPos = responseLine.IndexOf(':');

            if (colonPos == -1)
            {
                throw new FormatException("Invalid profile response line: Response colon is missing.");
            }

            var status     = responseLine.Substring(0, colonPos).Trim();
            var statusCode = ProfileStatus.OK;

            if (status == string.Empty)
            {
                throw new FormatException("Invalid profile response line: Missing status.");
            }

            if (status.StartsWith("ERROR"))
            {
                var bracketPos = status.IndexOf('[');

                if (bracketPos == -1)
                {
                    throw new FormatException("Invalid profile response line: Malformed status code.");
                }

                if (status.Last() != ']')
                {
                    throw new FormatException("Invalid profile response line: Malformed status code.");
                }

                statusCode = status.Substring(bracketPos + 1, status.Length - bracketPos - 2);

                if (statusCode == string.Empty)
                {
                    throw new FormatException("Invalid profile response line: Malformed status code.");
                }

                status = "ERROR";
            }

            if (status == string.Empty)
            {
                throw new FormatException("Invalid profile response line: Empty status code.");
            }

            var result = responseLine.Substring(colonPos + 1).Trim();

            switch (status)
            {
                case "OK":

                    return new ProfileResponse()
                    {
                        Value   = result,
                        JObject = null,
                        Error   = null
                    };

                case "OK-JSON":

                    try
                    {
                        return new ProfileResponse()
                        {
                            Value   = null,
                            JObject = JObject.Parse(responseLine.Substring(colonPos + 1)),
                            Error   = null
                        };
                    }
                    catch (JsonReaderException e)
                    {
                        throw new FormatException(e.Message, e);
                    }

                case "ERROR":

                    return new ProfileResponse()
                    {
                        Status  = statusCode,
                        Value   = null,
                        JObject = null,
                        Error   = responseLine.Substring(colonPos + 1).Trim()
                    };

                default:

                    throw new FormatException($"Invalid response [status={status}].");
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Private constructor.
        /// </summary>
        private ProfileResponse()
        {
        }

        /// <summary>
        /// Returns <c>true</c> for successful requests, <c>false</c> for failed ones.
        /// </summary>
        public bool Success => Status == ProfileStatus.OK;

        /// <summary>
        /// One of the <see cref="ProfileStatus"/> values.  This
        /// defaults to <see cref="ProfileStatus.OK"/>.
        /// </summary>
        public string Status { get; private set; } = ProfileStatus.OK;

        /// <summary>
        /// Returns the simply response string (for non-JSON responses).
        /// </summary>
        public string Value { get; private set; }

        /// <summary>
        /// Returns the <see cref="JObject"/> for JSON responses.
        /// </summary>
        public JObject JObject { get; private set; }

        /// <summary>
        /// Returns the error message for failed requests.
        /// </summary>
        public string Error { get; private set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (!Success)
            {
                return $"ERROR[{Status}]: {Error}";
            }

            if (Value != null)
            {
                return $"OK: {Value}";
            }
            else
            {
                return $"OK-JSON: {JObject.ToString(Formatting.None)}";
            }
        }
    }
}
