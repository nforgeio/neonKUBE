//-----------------------------------------------------------------------------
// FILE:	    ServiceEndpointProtocol.cs
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;

namespace Neon.Service
{
    /// <summary>
    /// Enumerats the network protocols that can be implemented by a <see cref="ServiceEndpoint"/>.
    /// </summary>
    public enum ServiceEndpointProtocol
    {
        /// <summary>
        /// HTTP.
        /// </summary>
        Http = 0,

        /// <summary>
        /// HTTPS.
        /// </summary>
        Https,

        /// <summary>
        /// TCP.
        /// </summary>
        Tcp,

        /// <summary>
        /// UDP.
        /// </summary>
        Udp
    }
}
