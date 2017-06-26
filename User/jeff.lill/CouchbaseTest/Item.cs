using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Data;

namespace CouchbaseTest
{
    public class Item
    {
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "Age", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int Age { get; set; }
    }
}
