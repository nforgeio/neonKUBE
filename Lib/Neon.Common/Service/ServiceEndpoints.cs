//-----------------------------------------------------------------------------
// FILE:	    ServiceMap.cs
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
    /// Holds the endpoint definitions for a service.
    /// </summary>
    public class ServiceEndpoints : Dictionary<string, ServiceEndpoint>
    {
        /// <summary>
        /// Adds an endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint.</param>
        public void Add(ServiceEndpoint endpoint)
        {
            Covenant.Requires<ArgumentNullException>(endpoint != null);

            Add(endpoint.Name, endpoint);
        }

        /// <summary>
        /// Returns the default endpoint (the one with the empty name) if present.
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown if the default endpoint doesn't exist.</exception>
        public ServiceEndpoint Default
        {
            get
            {
                if (!TryGetValue(string.Empty, out var endpoint))
                {
                    throw new KeyNotFoundException($"Cannot locate the default endpoint (with the empty name).");
                }

                return endpoint;
            }
        }
    }
}
