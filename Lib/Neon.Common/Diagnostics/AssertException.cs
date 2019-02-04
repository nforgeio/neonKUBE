//-----------------------------------------------------------------------------
// FILE:	    AssertException.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Diagnostics
{
    /// <summary>
    /// Thrown by <see cref="Covenant.Assert(bool, string)"/> to signal logic failures.
    /// </summary>
    public class AssertException : Exception
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public AssertException()
            : base("Assertion Failed")
        {
        }

        /// <summary>
        /// Constructs an assertion with a specific message and optional inner exception.
        /// </summary>
        /// <param name="message">The custom message.</param>
        /// <param name="innerException">Optional inner exception.</param>
        public AssertException(string message, Exception innerException = null)
            : base("Assertion Failed: " + message, innerException)
        {
        }
    }
}
