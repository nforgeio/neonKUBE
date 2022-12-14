//-----------------------------------------------------------------------------
// FILE:	    DexOidcConnector.cs
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
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Neon.Kube.Resources;
using Newtonsoft.Json;
using NJsonSchema;
using NJsonSchema.Annotations;
using YamlDotNet.Serialization;
using static Org.BouncyCastle.Bcpg.Attr.ImageAttrib;

namespace Neon.Kube
{
    /// <summary>
    /// Configuration for OIDC connectors.
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(DexConnectorJsonConverter))]
    public class DexConnector : IDexConnector
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexConnector()
        {
        }

        /// <inheritdoc/>
        public string Id { get; set; }


        /// <inheritdoc/>
        public string Name { get; set; }

        /// <inheritdoc/>
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
        public DexConnectorType Type { get; set; }
        /// <summary>
        /// Placeholder.
        /// </summary>
        public object Config { get; set; }
    }
}