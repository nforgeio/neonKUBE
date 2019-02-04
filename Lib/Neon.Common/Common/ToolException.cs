//-----------------------------------------------------------------------------
// FILE:        ToolException.cs
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
using Neon.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Common
{
    /// <summary>
    /// Typically thrown when a tool or subprocess is executed an fails.
    /// </summary>
    public class ToolException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The optional exception message.</param>
        /// <param name="inner">The optional inner exception.</param>
        public ToolException(string message = null, Exception inner = null)
            : base(message, inner)
        {
        }
    }
}
