//-----------------------------------------------------------------------------
// FILE:	    ProfileException.cs
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
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Deployment
{
    /// <summary>
    /// Thrown by <see cref="IProfileClient"/> instance when the profile server
    /// returned an error.
    /// </summary>
    public class ProfileException : Exception 
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="status">
        /// Pass as one of the <see cref="ProfileStatus"/> values indicating the
        /// reason for the failure.
        /// </param>
        /// <param name="inner">Optionally specifies an inner exception.</param>
        public ProfileException(string message, string status, Exception inner = null)
            : base(message, inner)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(status), nameof(status));

            this.Status = status;
        }

        /// <summary>
        /// Returns one of the <see cref="ProfileStatus"/> values indicating the
        /// reason for the failure.
        /// </summary>
        public string Status { get; private set; }
    }
}
