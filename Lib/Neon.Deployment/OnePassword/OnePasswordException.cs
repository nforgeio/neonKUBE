//-----------------------------------------------------------------------------
// FILE:	    OnePasswordException.cs
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Deployment
{
    /// <summary>
    /// Thrown by the <see cref="OnePassword"/> for errors.
    /// </summary>
    public class OnePasswordException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">Optionally specifies the exception message.</param>
        /// <param name="innerException">Optionally specifies an inner exception.</param>
        public OnePasswordException(string message = null, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}
