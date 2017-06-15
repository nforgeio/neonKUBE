//-----------------------------------------------------------------------------
// FILE:	    DbNode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.Cluster
{
    /// <summary>
    /// Information about an individual NeonCluster database node.
    /// </summary>
    public class DbNode
    {
        /// <summary>
        /// The Docker node name.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// The node's IP address.
        /// </summary>
        [JsonProperty(PropertyName = "Address", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public string Address { get; set; }

        /// <summary>
        /// The node status.
        /// </summary>
        [JsonProperty(PropertyName = "Status", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(DbStatus.Unknown)]
        public DbStatus Status { get; set; }

        /// <summary>
        /// A status or error message for the node.
        /// </summary>
        [JsonProperty(PropertyName = "Message", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue("")]
        public string Message { get; set; }
    }
}
