//-----------------------------------------------------------------------------
// FILE:	    KubeServiceEndpoint.cs
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

using Microsoft.OpenApi.Models;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using System.ComponentModel;

namespace Neon.Kube.Service
{
    /// <summary>
    /// Describes a network endpoint for a <see cref="KubeService"/> or <see cref="AspNetKubeService"/>.
    /// </summary>
    public class KubeServiceEndpoint
    {
        private KubeServiceDescription  serviceDescription;
        private string                  pathPrefix = string.Empty;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serviceDescription">The parent service description.</param>
        public KubeServiceEndpoint(KubeServiceDescription serviceDescription)
        {
            Covenant.Requires<ArgumentNullException>(serviceDescription != null);

            this.serviceDescription = serviceDescription;
        }

        /// <summary>
        /// The endpoint name.  This defaults to the empty string.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Specifies the network protocol implemented by this endpoint.
        /// This defaults to <see cref="KubeServiceEndpointProtocol.Http"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Protocol", Required = Required.Always)]
        [YamlMember(Alias = "protocol", ApplyNamingConventions = false)]
        public KubeServiceEndpointProtocol Protocol { get; set; } = KubeServiceEndpointProtocol.Http;

        /// <summary>
        /// For <see cref="AspNetKubeService"/> services, this specifies the path
        /// prefix to prepended to URIs accessing this service.  This defaults to
        /// an empty string.  This has meaning only for the HTTP and HTTPS protocols.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Any leading forward slash character will be stripped when setting this.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "PathPrefix", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "pathPrefix", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string PathPrefix
        {
            get => this.pathPrefix;

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    this.pathPrefix = string.Empty;
                }
                else if (pathPrefix[0] == '/')
                {
                    this.pathPrefix = value.Substring(1);
                }
                else
                {
                    this.pathPrefix = value;
                }
            }
        }

        /// <summary>
        /// For <see cref="AspNetKubeService"/> services, this specifies the network
        /// port to be used for URIs accessing this service.  This defaults to <b>80</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Port", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "port", ApplyNamingConventions = false)]
        [DefaultValue(80)]
        public int Port { get; set; } = 80;

        /// <summary>
        /// <para>
        /// For <see cref="AspNetKubeService"/> services, this is set to the 
        /// metadata used for Swagger related purposes.  This defaults to 
        /// <c>null</c>.
        /// </para>
        /// <note>
        /// This property is not read from JSON or YAML. 
        /// </note>
        /// </summary>
        public OpenApiInfo ApiInfo { get; set; } = null;

        /// <summary>
        /// Returns the URI for the endpoint.  For HTTP and HTTPS endpoints, this will
        /// include the service hostname returned by the parent <see cref="KubeServiceDescription"/>,
        /// along with the port and path prefix.  For TCP and UDP protocols, this will
        /// use the <b>TCP://</b> or <b>udp://</b> scheme along with the hostname and
        /// port.  The path prefix is ignored for these.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public string Uri
        {
            get
            {
                switch (Protocol)
                {
                    case KubeServiceEndpointProtocol.Http:

                        return $"http://{serviceDescription.Hostname}:{Port}/{PathPrefix}";

                    case KubeServiceEndpointProtocol.Https:

                        return $"https://{serviceDescription.Hostname}:{Port}/{PathPrefix}";

                    case KubeServiceEndpointProtocol.Tcp:

                        return $"tcp://{serviceDescription.Hostname}:{Port}";

                    case KubeServiceEndpointProtocol.Udp:

                        return $"udp://{serviceDescription.Hostname}:{Port}";

                    default:

                        throw new NotImplementedException();
                }
            }
        }
    }
}
