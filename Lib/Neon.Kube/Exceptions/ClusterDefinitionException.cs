//-----------------------------------------------------------------------------
// FILE:	    ClusterDefinitionException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Signals cluster definition errors.
    /// </summary>
    public class ClusterDefinitionException : Exception
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterDefinitionException()
        {
        }

        /// <summary>
        /// Consstructs an instance with a message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">Optionally specifies an inner exception.</param>
        public ClusterDefinitionException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}
