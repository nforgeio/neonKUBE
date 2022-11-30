//-----------------------------------------------------------------------------
// FILE:	    AcmeExternalAccountBinding.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Net;
using Neon.Time;
using System.Text.Json.Serialization;

namespace Neon.Kube.Resources
{
/// <summary>
/// Describes CertManager External Account Binding options.
/// </summary>
    public class AcmeExternalAccountBinding
    {
        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Default constructor.
        /// </summary>
        public AcmeExternalAccountBinding()
        {
        }

        /// <summary>
        /// The ID of the CA key that the External Account is bound to.
        /// </summary>
        [JsonPropertyName("keyID")]
        [JsonProperty(PropertyName = "keyID", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "keyID", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KeyId { get; set; } = null;

        /// <summary>
        /// Specifies a Secret Key as a string. This is only used when setting the secret via the ClusterDefinition/>.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonProperty(PropertyName = "key", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "key", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Key { get; set; } = null;

        /// <summary>
        /// Specifies a Secret Key Selector referencing a data item in a Kubernetes Secret which holds the symmetric MAC key of the External 
        /// Account Binding. The key is the index string that is paired with the key data in the Secret and should not be confused with the 
        /// key data itself, or indeed with the External Account Binding keyID above. The secret key stored in the Secret must be un-padded, 
        /// base64 URL encoded data.
        /// </summary>
        [JsonProperty(PropertyName = "keySecretRef", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public AcmeSecretKeySelector KeySecretRef { get; set; } = null;

        /// <inheritdoc/>
        public void Validate() { }
    }
}
