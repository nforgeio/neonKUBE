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
    /// Describes a collection of services deployed to Kubernetes or that run in
    /// a simulated unit test environment.  This is simply a dictionary mapping
    /// case sensitive service names to <see cref="ServiceDescription"/>
    /// records for each service.
    /// </summary>
    public class ServiceMap : Dictionary<string, ServiceDescription>
    {
        /// <summary>
        /// Adds a service description to the map.
        /// </summary>
        /// <param name="description">The service descrioption.</param>
        public void Add(ServiceDescription description)
        {
            this.Add(description.Name, description);
        }

        /// <summary>
        /// Adds the named service description.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="description">The service description.</param>
        public new void Add(string name, ServiceDescription description)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(description != null);

            description.Name = name;

            base.Add(name, description);

            // Ensure that all service endpoints have a reference to the
            // description so they'll be able to return fully qualified URIs.

            foreach (var endpoint in description.Endpoints.Values)
            {
                endpoint.ServiceDescription = description;
            }
        }

        /// <summary>
        /// Indexer mapping service names to their <see cref="ServiceDescription"/>.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <returns>The service description or <c>null</c> if the named service is not present.</returns>
        public new ServiceDescription this[string name]
        {
            get
            {
                if (base.TryGetValue(name, out var description))
                {
                    return description;
                }
                else
                {
                    return null;
                }
            }

            set
            {
                Covenant.Requires<ArgumentNullException>(value != null);

                base[name] = value;

                // Ensure that all service endpoints have a reference to the
                // description so they'll be able to return fully qualified URIs.

                foreach (var endpoint in value.Endpoints.Values)
                {
                    endpoint.ServiceDescription = value;
                }
            }
        }

        /// <summary>
        /// Returns the named endpoint for the specified service.
        /// </summary>
        /// <param name="serviceName">The target service name.</param>
        /// <param name="endpointName">Optionally specifies the target endpoint name (defaults to <see cref="string.Empty"/>).</param>
        /// <returns>The requested service endpoint.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the requested service or endpoint does not exist.</exception>
        public ServiceEndpoint GetServiceEndpoint(string serviceName, string endpointName = "")
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(serviceName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(endpointName));

            if (!TryGetValue(serviceName, out var service))
            {
                throw new KeyNotFoundException($"Service [{serviceName}] does not exist.");
            }

            if (!service.Endpoints.TryGetValue(endpointName, out var endpoint))
            {
                throw new KeyNotFoundException($"Service [{serviceName}] does not define the [{endpointName}] endpoint.");
            }

            return endpoint;
        }

        /// <summary>
        /// Returns the named endpoint <see cref="Uri"/> for the specified service.
        /// </summary>
        /// <param name="serviceName">The target service name.</param>
        /// <param name="endpointName">Optionally specifies the target endpoint name (defaults to <see cref="string.Empty"/>).</param>
        /// <returns>The requested service endpoint <see cref="Uri"/>.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the requested service or endpoint does not exist.</exception>
        public Uri GetServiceEndpointUri(string serviceName, string endpointName = "")
        {
            return GetServiceEndpoint(serviceName, endpointName).Uri;
        }
    }
}
