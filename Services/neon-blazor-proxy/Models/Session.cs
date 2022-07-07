//-----------------------------------------------------------------------------
// FILE:	    Session.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using System.ComponentModel;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace NeonBlazorProxy
{
    /// <summary>
    /// <para>
    /// Represents a Blazor session.
    /// </para>
    /// </summary>
    public class Session
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Session()
        {

        }

        /// <summary>
        /// The Session ID.
        /// </summary>
        [JsonProperty(PropertyName = "Id", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Id { get; set; } = null;

        /// <summary>
        /// The Blazor Websocket connection ID.
        /// </summary>
        [JsonProperty(PropertyName = "ConnectionId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ConnectionId { get; set; } = null;

        /// <summary>
        /// The upstream Blazor Server host.
        /// </summary>
        [JsonProperty(PropertyName = "UpstreamHost", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string UpstreamHost { get; set; } = null;
    }
}
