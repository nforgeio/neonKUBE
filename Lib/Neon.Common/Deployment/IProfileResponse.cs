//-----------------------------------------------------------------------------
// FILE:	    IProfileResponse.cs
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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Deployment
{
    /// <summary>
    /// Abstracts Neon Profile Service named pipe command responses.
    /// </summary>
    public interface IProfileResponse
    {
        /// <summary>
        /// Retrurns <c>true</c> for successful requests, <c>false</c> for failed ones.
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// Returns the simply response string (for non-JSON responses).
        /// </summary>
        string Value { get; }

        /// <summary>
        /// Returns the <see cref="JObject"/> for JSON responses.
        /// </summary>
        JObject JObject { get; }

        /// <summary>
        /// Returns the error message for failed requests.
        /// </summary>
        string Error { get; }
    }
}
