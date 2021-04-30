//-----------------------------------------------------------------------------
// FILE:	    NeonServiceMap.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

using Neon.Common;
using Neon.Service;

using Newtonsoft.Json;

namespace Neon.Kube
{
    /// <summary>
    /// Holds more detailed information describing the Neon cluster services.  This a
    /// <see cref="ServiceMap"/> keyed by the service names defined in <see cref="NeonServices"/>
    /// and the class also has properties named for each service you can use as a shortcut.
    /// </summary>
    /// <remarks>
    /// <note>
    /// The default constructor builds a <see cref="NeonServiceMap"/> for all environments, and clones
    /// them for each Dev's workstation in the Neon Office, setting the correct static IP.
    /// </note>
    /// </remarks>
    public class NeonServiceMap : ServiceMap
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The default service endpoint name.
        /// </summary>
        public const string DefaultEndpointName = "";

        /// <summary>
        /// Returns a dictionary holding all service maps.
        /// </summary>
        public static Dictionary<string, NeonServiceMap> serviceMaps;

        /// <summary>
        /// Returns the Production internal service map.
        /// </summary>
        public static NeonServiceMap Production => serviceMaps["production"];

        /// <summary>
        /// Returns the $NAME service map.
        /// </summary>
        public static NeonServiceMap GetMapName(string name)
        {
            return serviceMaps[name];
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        static NeonServiceMap()
        {
            serviceMaps = new Dictionary<string, NeonServiceMap>();

            BuildProduction();
        }

        /// <summary>
        /// Ensure that the service description properties have been initialized
        /// for all service endpoints.
        /// </summary>
        /// <param name="map">The service map.</param>
        private static void VerifyInit(NeonServiceMap map)
        {
            foreach (var serviceDescription in map.Values)
            {
                foreach (var serviceEndpoint in serviceDescription.Endpoints.Values)
                {
                    if (object.ReferenceEquals(serviceEndpoint, serviceDescription))
                    {
                        throw new InvalidOperationException($"Service map entry for [{serviceDescription.Name}] has endpoint [{serviceEndpoint.Name}] that does not reference the correct parent description.");
                    }
                }
            }
        }

        /// <summary>
        /// Builds the <see cref="NeonServiceMap"/> for internal production services.
        /// </summary>
        private static void BuildProduction()
        {
            serviceMaps["production"] = new NeonServiceMap();
            serviceMaps["production"].AddServiceDescription(NeonServices.ClusterOperator, new ServiceEndpoint());
            serviceMaps["production"].AddServiceDescription(NeonServices.TestCadence, new ServiceEndpoint());
            serviceMaps["production"].AddServiceDescription(NeonServices.TestTemporal, new ServiceEndpoint());

            VerifyInit(Production);
        }


        /// <summary>
        /// Clones a <see cref="NeonServiceMap"/> and changes the <see cref="ServiceDescription.Address"/> for each of the services.
        /// </summary>
        public static NeonServiceMap CloneWithNewAdress(NeonServiceMap map, string ip)
        {
            var newMap = NeonHelper.JsonClone<NeonServiceMap>(map);
            int port = 8300;
            foreach (var k in newMap.Keys)
            {
                newMap[k].Address = ip;

                if (!newMap[k].Endpoints.IsEmpty())
                {
                    newMap[k].Endpoints.Default.ServiceDescription = newMap[k];
                    if (newMap[k].Endpoints.Default.Port == 0)
                    {
                        newMap[k].Endpoints.Default.Port = port;
                        port++;
                    }
                }
            }

            VerifyInit(newMap);

            return newMap;
        }

        /// <summary>
        /// Adds a <see cref="ServiceDescription"/> for the named service using
        /// default details and specifying an optional network endpoint.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <param name="endpoint">Optionally specifies a single service endpoint.</param>
        /// <param name="namespace">The K8s namespace where the service is deployed.</param>
        /// <returns>
        /// The <see cref="ServiceDescription"/> so that it can be customized further if necessary.
        /// </returns>
        /// <remarks>
        /// This method also initializes any service endpoint <see cref="ServiceEndpoint.ServiceDescription"/>
        /// properties to the service description being created and initializes blank or <c>null</c> endpoint
        /// names to <see cref="DefaultEndpointName"/>.
        /// </remarks>
        private ServiceDescription AddServiceDescription(string serviceName, ServiceEndpoint endpoint = null, string @namespace = "default")
        {
            var serviceDescription = new ServiceDescription()
            {
                Name = serviceName,
                Namespace = @namespace
            };

            if (endpoint != null)
            {
                if (string.IsNullOrEmpty(endpoint.Name))
                {
                    endpoint.Name = DefaultEndpointName;
                }

                endpoint.ServiceDescription = serviceDescription;

                serviceDescription.Endpoints.Add(endpoint.Name, endpoint);
            }

            base.Add(serviceName, serviceDescription);

            return serviceDescription;
        }

        /// <summary>
        /// Adds a <see cref="ServiceDescription"/> for the named service using
        /// default details with a set of network endpoints.
        /// </summary>
        /// <param name="serviceName">The service name or <c>null</c>.</param>
        /// <param name="endpoints">The service's network endpoints.</param>
        /// <returns>
        /// The <see cref="ServiceDescription"/> so that it can be customized further if necessary.
        /// </returns>
        /// <remarks>
        /// This method also initializes any service endpoints <see cref="ServiceEndpoint.ServiceDescription"/>
        /// properties to the service description being created and initializes blank or <c>null</c> endpoint
        /// names to <see cref="DefaultEndpointName"/>.
        /// </remarks>
        private ServiceDescription AddServiceDescription(string serviceName, IEnumerable<ServiceEndpoint> endpoints)
        {
            var serviceDescription = new ServiceDescription()
            {
                Name = serviceName
            };

            if (endpoints != null)
            {
                foreach (var endpoint in endpoints)
                {
                    if (endpoint == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(endpoint.Name))
                    {
                        endpoint.Name = DefaultEndpointName;
                    }

                    endpoint.ServiceDescription = serviceDescription;

                    serviceDescription.Endpoints.Add(endpoint.Name, endpoint);
                }
            }

            base.Add(serviceName, serviceDescription);

            return serviceDescription;
        }

        /// <summary>
        /// Returns the <see cref="ServiceDescription"/> for the given service name.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <returns>The associated <see cref="ServiceDescription"/>.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if thre service map has no description for the service.</exception>
        private ServiceDescription Lookup(string serviceName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(serviceName), nameof(serviceName));

            if (!base.TryGetValue(serviceName, out var serviceDescription))
            {
                throw new KeyNotFoundException($"The service map does not have information for [{serviceName}].");
            }

            return serviceDescription;
        }
    }
}