//-----------------------------------------------------------------------------
// FILE:	    ServiceEndpoint.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;

namespace Neon.Service
{
    /// <summary>
    /// Describes a network endpoint for remote service.
    /// </summary>
    public class ServiceEndpoint
    {
        private string pathPrefix = string.Empty;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ServiceEndpoint()
        {
        }

        /// <summary>
        /// <para>
        /// The parent <see cref="ServiceDescription"/>.
        /// </para>
        /// <note>
        /// This must be initialized before attempting to reference the endpoint.
        /// </note>
        /// </summary>
        public ServiceDescription ServiceDescription { get; set; }

        /// <summary>
        /// The endpoint name.  This defaults to the empty string.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Specifies the network protocol implemented by this endpoint.
        /// This defaults to <see cref="ServiceEndpointProtocol.Http"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Protocol", Required = Required.Always)]
        [YamlMember(Alias = "protocol", ApplyNamingConventions = false)]
        public ServiceEndpointProtocol Protocol { get; set; } = ServiceEndpointProtocol.Http;

        /// <summary>
        /// This specifies the path prefix to prepended to URIs accessing this service. 
        /// This defaults to an empty string.  This has meaning only for the HTTP and 
        /// HTTPS protocols.
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
                else if (value[0] == '/')
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
        /// <para>
        /// This specifies the network port to be used for URIs accessing this service.  This defaults to <b>-1</b>
        /// which indicates that HTTP and HTTPS based endpoints will be initialized to their default ports <b>80</b>
        /// and <b>443</b> so you don't need to specify and explicit ports for these.  You will need to set this to
        /// a valid port for TCP and UDP protocols.
        /// </para>
        /// <note>
        /// <b>CAUTION:</b> It's best not to rely on this value when setting up your service network endpoints and
        /// reference the port from <see cref="Uri"/> instead because that will always be a valid TCP port number.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Port", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "port", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int Port { get; set; } = -1;

        // $todo(jeff.lill): 
        //
        // We're not doing anything with Swagger documentation yet because
        // we'd have to figure out where the generatedf code comment XML
        // files are and ensure that they're included in the generated
        // images by [core-layers] or whatever.

        /// <summary>
        /// ASP.NET services, this can be set to the metadata used for Swagger documentation
        /// generation related purposes.  This defaults to <c>null</c>.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public ServiceApiInfo ApiInfo { get; set; } = null;

        /// <summary>
        /// <para>
        /// Returns the URI for the endpoint.  For HTTP and HTTPS endpoints, this will
        /// include the service hostname returned by the parent <see cref="ServiceDescription"/>,
        /// along with the port and path prefix.  For TCP and UDP protocols, this will
        /// use the <b>tcp://</b> or <b>udp://</b> scheme along with the hostname and
        /// just the port.  The path prefix is ignored for TCP and UDP.
        /// </para>
        /// <para>
        /// When <see cref="Port"/> is <b>-1</b> for HTTP or HTTPS endpoints, the URL returned 
        /// will use the default port for thbe protocol (80/443).  For TCP and UDP protocols,
        /// the port must be a valid (non-negative) network port.
        /// </para>
        /// <note>
        /// For production, this property returns the partially qualified hostname for
        /// the host, omitting the cluster domain (e.g. <b>cluster.local</b>).  Use 
        /// <see cref="FullUri"/> if you need the fully qualified URI.
        /// </note>
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when <see cref="Port"/> is not valid for the endpoint protocol.</exception>
        [JsonIgnore]
        [YamlIgnore]
        public Uri Uri
        {
            get
            {
                if (Port != -1 && !NetHelper.IsValidPort(Port))
                {
                    throw new ArgumentException($"Invalid network port [{Port}].");
                }

                if (ServiceDescription == null)
                {
                    throw new InvalidOperationException($"The [{nameof(ServiceEndpoint)}.{nameof(ServiceDescription)}] property has not been set.");
                }

                switch (Protocol)
                {
                    case ServiceEndpointProtocol.Http:

                        if (Port == -1)
                        {
                            return new Uri($"http://{ServiceDescription.Hostname}/{PathPrefix}");
                        }
                        else
                        {
                            return new Uri($"http://{ServiceDescription.Hostname}:{Port}/{PathPrefix}");
                        }

                    case ServiceEndpointProtocol.Https:

                        if (Port == -1)
                        {
                            return new Uri($"https://{ServiceDescription.Hostname}/{PathPrefix}");
                        }
                        else
                        {
                            return new Uri($"https://{ServiceDescription.Hostname}:{Port}/{PathPrefix}");
                        }

                    case ServiceEndpointProtocol.Tcp:

                        if (Port == -1)
                        {
                            throw new ArgumentException("TCP endpoints require a non-zero port.");
                        }

                        return new Uri($"tcp://{ServiceDescription.Hostname}:{Port}");

                    case ServiceEndpointProtocol.Udp:

                        if (Port == -1)
                        {
                            throw new ArgumentException("UDP endpoints require a non-zero port.");
                        }

                        return new Uri($"udp://{ServiceDescription.Hostname}:{Port}");

                    default:

                        throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        /// <para>
        /// Returns the URI for the endpoint.  For HTTP and HTTPS endpoints, this will
        /// include the service hostname returned by the parent <see cref="ServiceDescription"/>,
        /// along with the port and path prefix.  For TCP and UDP protocols, this will
        /// use the <b>tcp://</b> or <b>udp://</b> scheme along with the hostname and
        /// just the port.  The path prefix is ignored for TCP and UDP.
        /// </para>
        /// <para>
        /// When <see cref="Port"/> is zero for HTTP or HTTPS endpoints, the URL returned 
        /// will use the default port for thbe protocol (80/443).  For TCP and UDP protocols,
        /// the port must be a valid non-zero network port.
        /// </para>
        /// <note>
        /// For production, this property returns the partially qualified hostname for
        /// the host, including the cluster domain (e.g. <b>cluster.local</b>.  Use 
        /// <see cref="Uri"/> if you need the relative qualified URI.
        /// </note>
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when <see cref="Port"/> is not valid for the endpoint protocol.</exception>
        [JsonIgnore]
        [YamlIgnore]
        public Uri FullUri
        {
            get
            {
                if (Port != -1 && !NetHelper.IsValidPort(Port))
                {
                    throw new ArgumentException($"Invalid network port [{Port}].");
                }

                if (ServiceDescription == null)
                {
                    throw new InvalidOperationException($"The [{nameof(ServiceEndpoint)}.{nameof(ServiceDescription)}] property has not been set.");
                }

                switch (Protocol)
                {
                    case ServiceEndpointProtocol.Http:

                        if (Port == -1)
                        {
                            return new Uri($"http://{ServiceDescription.Hostname}/{PathPrefix}");
                        }
                        else
                        {
                            return new Uri($"http://{ServiceDescription.Hostname}:{Port}/{PathPrefix}");
                        }

                    case ServiceEndpointProtocol.Https:

                        if (Port == -1)
                        {
                            return new Uri($"https://{ServiceDescription.Hostname}/{PathPrefix}");
                        }
                        else
                        {
                            return new Uri($"https://{ServiceDescription.Hostname}:{Port}/{PathPrefix}");
                        }

                    case ServiceEndpointProtocol.Tcp:

                        if (Port == -1)
                        {
                            throw new ArgumentException("TCP endpoints require a non-zero port.");
                        }

                        return new Uri($"tcp://{ServiceDescription.Hostname}:{Port}");

                    case ServiceEndpointProtocol.Udp:

                        if (Port == -1)
                        {
                            throw new ArgumentException("UDP endpoints require a non-zero port.");
                        }

                        return new Uri($"udp://{ServiceDescription.Hostname}:{Port}");

                    default:

                        throw new NotImplementedException();
                }
            }
        }
    }
}
