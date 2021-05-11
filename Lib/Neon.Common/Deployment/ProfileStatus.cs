//-----------------------------------------------------------------------------
// FILE:	    ProfileStatus.cs
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
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Deployment
{
    /// <summary>
    /// Enumerates the profile error code strings.
    /// </summary>
    public static class ProfileStatus
    {
        /// <summary>
        /// The request completed successfully.
        /// </summary>
        public const string OK = "OK";

        /// <summary>
        /// Unable to establish a connection with to the profile server.
        /// </summary>
        public const string Connect = "CONNECT";

        /// <summary>
        /// The profile server is running but it's not ready to accept requests.
        /// </summary>
        public const string NotReady = "NOT-READY";

        /// <summary>
        /// The request is malformed.
        /// </summary>
        public const string BadRequest = "BAD-REQUEST";

        /// <summary>
        /// The request is missing one or more required arguments.
        /// </summary>
        public const string MissingArg = "MISSING-ARG";

        /// <summary>
        /// The request command is unknown.
        /// </summary>
        public const string BadCommand = "BAD-COMMAND";

        /// <summary>
        /// A secret or profile value could not be found.
        /// </summary>
        public const string NotFound = "NOT-FOUND";

        /// <summary>
        /// The user aborted the operation.
        /// </summary>
        public const string Aborted = "ABORTED";

        /// <summary>
        /// The operation timed-out.
        /// </summary>
        public const string Timeout = "TIMEOUT";

        /// <summary>
        /// The profile or secret reference is malformed.
        /// </summary>
        public const string BadReference = "BAD-REFERENCE";

        /// <summary>
        /// The 1Password backend service is not available.
        /// </summary>
        public const string OnePasswordUnavailable = "1PASSWORD-UNAVAILABLE";

        /// <summary>
        /// An arbitrary call to the profile server failed.
        /// </summary>
        public const string CallError = "CALL-ERROR";

        /// <summary>
        /// An onspecified error occurred.
        /// </summary>
        public const string Other = "OTHER";
    }
}
