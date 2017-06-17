//-----------------------------------------------------------------------------
// FILE:	    Entity.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Data
{
    /// <inheritdoc/>
    public class Entity : IEntity
    {
        /// <inheritdoc/>
        [JsonProperty(PropertyName = "_type", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Type { get; set; }

        /// <inheritdoc/>
        [JsonProperty(PropertyName = "_table", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Table { get; set; }

        /// <inheritdoc/>
        public string GetTableProperty()
        {
            return "_table";
        }

        /// <inheritdoc/>
        public string GetTypeProperty()
        {
            return "_type";
        }
    }
}
