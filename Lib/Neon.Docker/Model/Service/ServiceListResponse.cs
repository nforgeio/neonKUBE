//-----------------------------------------------------------------------------
// FILE:	    ServiceListResponse.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Net;

namespace Neon.Docker
{
    /// <summary>
    /// The response from a <see cref="DockerClient.ServiceListAsync"/> command.
    /// </summary>
    public class ServiceListResponse : DockerResponse
    {
        /// <summary>
        /// Constructs the response from a lower-level <see cref="JsonResponse"/>.
        /// </summary>
        /// <param name="response"></param>
        internal ServiceListResponse(JsonResponse response)
            : base(response)
        {
            this.Inner = response.AsDynamic();

            foreach (var service in this.Inner)
            {
                this.Services.Add(DockerClient.ParseObject<ServiceDetails>((JObject)service));
            }
        }

        /// <summary>
        /// Returns the raw <v>dynamic</v> object actually returned by Docker.
        /// You may use this to access newer Docker properties that have not
        /// yet been wrapped by this class.
        /// </summary>
        public dynamic Inner { get; private set; }

        /// <summary>
        /// Returns the list of service details returned by the Docker engine.
        /// </summary>
        public List<ServiceDetails> Services { get; private set; } = new List<ServiceDetails>();
    }
}
