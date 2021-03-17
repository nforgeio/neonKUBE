//-----------------------------------------------------------------------------
// FILE:	   ProfileHandlerResult.cs
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
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Deployment
{
    /// <summary>
    /// Describes the results returned by <see cref="ProfileServer"/> handlers.
    /// </summary>
    public class ProfileHandlerResult
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Constructs a handler value result.
        /// </summary>
        /// <param name="value"></param>
        /// <returns>The <see cref="ProfileHandlerResult"/>.</returns>
        public static ProfileHandlerResult Create(string value)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(value));

            return new ProfileHandlerResult()
            {
                Value = value
            };
        }

        /// <summary>
        /// Constructs an error result.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <returns>The <see cref="ProfileHandlerResult"/>.</returns>
        public static ProfileHandlerResult CreateError(string message)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(message));

            return new ProfileHandlerResult()
            {
                Error = message
            };
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Private constructor.
        /// </summary>
        private ProfileHandlerResult()
        {
        }

        /// <summary>
        /// Specifies the value returned by the handler.
        /// </summary>
        public string Value { get; private set; }

        /// <summary>
        /// Specifies an error message when not <c>null</c>.
        /// </summary>
        public string Error { get; private set; }
    }
}
