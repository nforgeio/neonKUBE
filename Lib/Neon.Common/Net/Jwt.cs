//-----------------------------------------------------------------------------
// FILE:	    Jwt.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Retry;

namespace Neon.Net
{
    /// <summary>
    /// A lightweight implementation of Json Web Token (JWT) suitable for use by
    /// client applications.  The JWT structure is described <a href="https://jwt.io/introduction/">here</a>.
    /// </summary>
    public class Jwt
    {
        // JWT Specification:  https://openid.net/specs/draft-jones-json-web-token-07.html
        // Base64URL Encoding: https://tools.ietf.org/html/rfc4648#section-5

        //---------------------------------------------------------------------
        // Static methods

        /// <summary>
        /// <para>
        /// Parses a <see cref="Jwt"/> from an encoded string.
        /// </para>
        /// <note>
        /// <b>WARNING:</b> This method <b>does not verify the signature</b> so it is <b>not suitable for
        /// verifying a JWT's authenticity</b>.
        /// </note>
        /// </summary>
        /// <param name="jwtString">The encoded JWT string.</param>
        /// <returns>The parsed <see cref="Jwt"/> instance.</returns>
        /// <exception cref="FormatException">Thrown if the JWT format is invalid.</exception>
        public static Jwt Parse(string jwtString)
        {
            return new Jwt(jwtString);
        }

        //---------------------------------------------------------------------
        // Instance methods

        private string jwtString;

        /// <summary>
        /// Private constructor.
        /// </summary>
        /// <param name="jwtString">The encoded JWT string.</param>
        /// <exception cref="FormatException">Thrown if the JWT format is invalid.</exception>
        private Jwt(string jwtString)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(jwtString), nameof(jwtString));

            this.jwtString = jwtString;

            // Encoded JWTs are formatted as three URL encoded Base64URL encoded strings
            // separated by periods.  The first two strings are actually JSON objects 
            // and the last holds the signature bytes:
            //
            //      1. JWT header
            //      2. JWT payload (with the claims)
            //      3. JWT signature
            //
            // We're going to verify that these JSON objects exist and then parse
            // them as Newtonsoft [JObject] instances. 

            var parts = jwtString.Split('.');

            if (parts.Length != 3)
            {
                throw new FormatException("JWT must have three parts separated by periods.");
            }

            try
            {
                Header    = JObject.Parse(Encoding.UTF8.GetString(NeonHelper.Base64UrlDecode(parts[0])));
                Payload   = JObject.Parse(Encoding.UTF8.GetString(NeonHelper.Base64UrlDecode(parts[1])));
                Signature = NeonHelper.Base64UrlDecode(parts[2]);
            }
            catch (Exception e)
            {
                throw new FormatException("Cannot parse JWT JSON parts.", e);
            }
        }

        /// <summary>
        /// Returns a Newtonsoft <see cref="JObject"/> with the JWT <b>header</b> properties.
        /// </summary>
        public JObject Header { get; private set; }

        /// <summary>
        /// Returns a Newtonsoft <see cref="JObject"/> with the JWT <b>payload</b> properties.
        /// </summary>
        public JObject Payload { get; private set; }

        /// <summary>
        /// Returns the JWT <b>signature</b> as a byte array.
        /// </summary>
        public byte[] Signature { get; private set; }

        /// <summary>
        /// <para>
        /// Renders the JWT back into its encoded string format.
        /// </para>
        /// <note>
        /// This method currently returns the original string used to parse the JWT.  It does
        /// not actually perform any encoding so any changes made to the properties will not
        /// be included in the output.
        /// </note>
        /// </summary>
        /// <returns>The encode JWT.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the JWT wasn't parsed from a string.</exception>
        public override string ToString()
        {
            if (string.IsNullOrEmpty(jwtString))
            {
                throw new InvalidOperationException("This JWT was not parsed from a string.");
            }

            return jwtString;
        }
    }
}
