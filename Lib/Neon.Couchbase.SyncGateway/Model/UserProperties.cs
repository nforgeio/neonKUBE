//-----------------------------------------------------------------------------
// FILE:	    UserProperties.cs
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

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

using Neon.Common;

namespace Neon.Couchbase.SyncGateway
{
    /// <summary>
    /// Describes a Couchbase Sync Gateway user.
    /// </summary>
    public class UserProperties
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public UserProperties()
        {
        }

        /// <summary>
        /// The user's unique name within the database.
        /// </summary>
        [JsonProperty(PropertyName = "Name")]
        [YamlMember(Alias = "Name", ApplyNamingConventions = false)]
        public string Name { get; set; }

        /// <summary>
        /// The user's password.
        /// </summary>
        [JsonProperty(PropertyName = "Password")]
        [YamlMember(Alias = "Password", ApplyNamingConventions = false)]
        public string Password { get; set; }

        /// <summary>
        /// The user's email address.
        /// </summary>
        [JsonProperty(PropertyName = "Email")]
        [YamlMember(Alias = "Email", ApplyNamingConventions = false)]
        public string Email { get; set; }

        /// <summary>
        /// Indicates whether the user is disabled.  Disabled users cannot login.
        /// </summary>
        [JsonProperty(PropertyName = "IsDisabled")]
        [YamlMember(Alias = "IsDisabled", ApplyNamingConventions = false)]
        public bool IsDisabled { get; set; }

        /// <summary>
        /// The channels to be made accessable to this specific user.
        /// </summary>
        [JsonProperty(PropertyName = "AdminChannels")]
        [YamlMember(Alias = "AdminChannels", ApplyNamingConventions = false)]
        public List<string> AdminChannels { get; set; } = new List<string>();

        /// <summary>
        /// The list of all channels the user may access.  This includes 
        /// those explictly assigned via <see cref="AdminChannels"/> as well as 
        /// channels accessable due to role assignments or due to special access
        /// granted by the Sync-Server's sync function.
        /// </summary>
        [JsonProperty(PropertyName = "AllChannels")]
        [YamlMember(Alias = "AllChannels", ApplyNamingConventions = false)]
        public List<String> AllChannels { get; set; } = new List<string>();

        /// <summary>
        /// Identifies the roles the user may assume.
        /// </summary>
        [JsonProperty(PropertyName = "Roles")]
        [YamlMember(Alias = "Roles", ApplyNamingConventions = false)]
        public List<string> Roles { get; set; } = new List<string>();
    }
}
