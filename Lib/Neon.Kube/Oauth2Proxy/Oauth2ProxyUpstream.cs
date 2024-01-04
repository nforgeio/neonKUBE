//-----------------------------------------------------------------------------
// FILE:        Oauth2ProxyUpstream.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Buffers.Text;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube.Oauth2Proxy
{
    /// <summary>
    /// Oauth2Proxy header model.
    /// </summary>
    public class Oauth2ProxyUpstream
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Oauth2ProxyUpstream()
        {
        }

        /// <summary>
        /// Should be a unique identifier for the upstream.
        /// </summary>
        [JsonProperty(PropertyName = "Id", Required = Required.Always)]
        [YamlMember(Alias = "id", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Id { get; set; }

        /// <summary>
        /// Used to map requests to the upstream server.  
        /// 
        /// The closest match will take precedence and all Paths must be unique.
        /// Path can also take a pattern when used with RewriteTarget.
        /// Path segments can be captured and matched using regular experessions. 
        /// 
        /// Eg:
        /// - ^/foo$: Match only the explicit path /foo
        /// - ^/bar/$: Match any path prefixed with /bar/
        /// - ^/baz/(.*)$: Match any path prefixed with /baz and capture the remaining path for use with RewriteTarget
        /// </summary>
        [JsonProperty(PropertyName = "Path", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "path", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Path { get; set; }

        /// <summary>
        /// Allows users to rewrite the request path before it is sent to the upstream server.
        /// Use the Path to capture segments for reuse within the rewrite target.
        /// Eg: With a Path of ^/baz/(.*), a RewriteTarget of /foo/$1 would rewrite
        /// the request /baz/abc/123 to /foo/abc/123 before proxying to the
        /// upstream server.
        /// </summary>
        [JsonProperty(PropertyName = "RewriteTarget", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "rewriteTarget", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string RewriteTarget { get; set; }

        /// <summary>
        /// The URI of the upstream server. This may be an HTTP(S) server of a File
        /// based URL.It may include a path, in which case all requests will be served
        /// under that path.
        /// Eg:
        /// - http://localhost:8080
        /// - https://service.localhost
        /// - https://service.localhost/path
        /// - file://host/path
        /// If the URI's path is "/base" and the incoming request was for "/dir",
        /// the upstream request will be for "/base/dir".
        /// </summary>
        [JsonProperty(PropertyName = "Uri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "uri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Uri { get; set; }

        /// <summary>
        /// Will skip TLS verification of upstream HTTPS hosts. This option is insecure and will allow potential Man-In-The-Middle attacks
        /// betweem OAuth2 Proxy and the usptream server.
        /// </summary>
        [JsonProperty(PropertyName = "InsecureSkipTlsVerify", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "insecureSkipTLSVerify", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool InsecureSkipTlsVerify { get; set; } = false;

        /// <summary>
        /// Will make all requests to this upstream have a static response.
        /// The response will have a body of "Authenticated" and a response code
        /// matching StaticCode.
        /// If StaticCode is not set, the response will return a 200 response.
        /// </summary>
        [JsonProperty(PropertyName = "Static", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "static", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Static { get; set; } = false;

        /// <summary>
        /// Determines the response code for the Static response. This option can only be used with <see cref="Static"/> enabled.
        /// </summary>
        [JsonProperty(PropertyName = "StaticCode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "staticCode", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public int? StaticCode { get; set; }

        /// <summary>
        /// The period between flushing the response buffer when streaming response from the upstream.
        /// </summary>
        [JsonProperty(PropertyName = "FlushInterval", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "flushInterval", ApplyNamingConventions = false)]
        [DefaultValue("1s")]
        public string FlushInterval { get; set; } = "1s";

        /// <summary>
        /// Determines whether the request host header should be proxied to the upstream server.
        /// </summary>
        [JsonProperty(PropertyName = "PassHostHeader", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "passHostHeader", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public bool? PassHostHeader { get; set; }

        /// <summary>
        /// Enables proxying of websockets to upstream servers.
        /// </summary>
        [JsonProperty(PropertyName = "ProxyWebSockets", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "proxyWebSockets", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public bool? ProxyWebSockets { get; set; }

        /// <summary>
        /// The maximum duration the server will wait for a response from the upstream server.
        /// </summary>
        [JsonProperty(PropertyName = "Timeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "timeout", ApplyNamingConventions = false)]
        [DefaultValue("1s")]
        public string Timeout { get; set; } = "30s";
    }
}
