//-----------------------------------------------------------------------------
// FILE:	    JsonClientPayload.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Collections;
using Neon.Diagnostics;
using Neon.Retry;

namespace Neon.Net
{
    /// <summary>
    /// Passed as the <b>document</b> to be uploaded with a <see cref="JsonClient"/> <b>POST</b>
    /// or <b>PUT</b> request to customize the payload data and content-type.  This can be used
    /// in special situations where a REST API needs to push <b>application/x-www-form-urlencoded</b>
    /// data or other formats.
    /// </summary>
    public class JsonClientPayload
    {
        /// <summary>
        /// <para>
        /// Constructs an instance from the <b>Content-Type</b> header and text to be 
        /// included with the POST/PUT.
        /// </para>
        /// <note>
        /// The uploaded text will be <b>UTF-8</b> encoded.
        /// </note>
        /// </summary>
        /// <param name="contentType">The value to be passed as the request's <b>Content-Type</b> header.</param>
        /// <param name="text">The text payload.</param>
        public JsonClientPayload(string contentType, string text)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(contentType));
            Covenant.Requires<ArgumentNullException>(text != null);

            this.ContentType  = contentType;
            this.ContentBytes = Encoding.UTF8.GetBytes(text);
        }

        /// <summary>
        /// Constructs an instance from the <b>Content-Type</b> header and byte data to be 
        /// included with the POST/PUT.
        /// </summary>
        /// <param name="contentType">The value to be passed as the request's <b>Content-Type</b> header.</param>
        /// <param name="bytes">The bytes to be uploaded.</param>
        public JsonClientPayload(string contentType, byte[] bytes)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(contentType));
            Covenant.Requires<ArgumentNullException>(bytes != null);

            this.ContentType  = contentType;
            this.ContentBytes = bytes;
        }

        /// <summary>
        /// Returns the HTTP <b>Content-Type</b> header to be included in the POST/PUT request.
        /// </summary>
        public string ContentType { get; private set; }

        /// <summary>
        /// Returns the payload bytes to be included in the POST/PUT request.
        /// </summary>
        public byte[] ContentBytes { get; private set; }
    }
}
