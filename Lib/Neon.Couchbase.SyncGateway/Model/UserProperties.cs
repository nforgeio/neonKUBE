//-----------------------------------------------------------------------------
// FILE:	    UserProperties.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
        public string Name { get; set; }

        /// <summary>
        /// The user's password.
        /// </summary>
        [JsonProperty(PropertyName = "Password")]
        public string Password { get; set; }

        /// <summary>
        /// The user's email address.
        /// </summary>
        [JsonProperty(PropertyName = "Email")]
        public string Email { get; set; }

        /// <summary>
        /// Indicates whether the user is disabled.  Disabled users cannot login.
        /// </summary>
        [JsonProperty(PropertyName = "IsDisabled")]
        public bool IsDisabled { get; set; }

        /// <summary>
        /// The channels to be made accessable to this specific user.
        /// </summary>
        [JsonProperty(PropertyName = "AdminChannels")]
        public List<string> AdminChannels { get; set; } = new List<string>();

        /// <summary>
        /// The list of all channels the user may access.  This includes 
        /// those explictly assigned via <see cref="AdminChannels"/> as well as 
        /// channels accessable due to role assignments or due to special access
        /// granted by the Sync-Server's sync function.
        /// </summary>
        [JsonProperty(PropertyName = "AllChannels")]
        public List<String> AllChannels { get; set; } = new List<string>();

        /// <summary>
        /// Identifies the roles the user may assume.
        /// </summary>
        [JsonProperty(PropertyName = "Roles")]
        public List<string> Roles { get; set; } = new List<string>();
    }
}
