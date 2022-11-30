//-----------------------------------------------------------------------------
// FILE:	    PasswordSecretRef.cs
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

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube.Resources
{
    /// <summary>
    /// Configures additional keystore output formats stored in the `secretName` Secret resource.
    /// </summary>
    public class PasswordSecretRef
    {
        /// <summary>
        /// Initializes a new instance of the PasswordSecretRef class.
        /// </summary>
        public PasswordSecretRef()
        {
        }

        /// <summary>
        /// The key of the entry in the Secret resource's `data` field to be used. Some instances of this field may be defaulted, in others 
        /// it may be required.
        /// </summary>
        [JsonProperty(PropertyName = "key", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Key { get; set; }

        /// <summary>
        /// Name of the resource being referred to. More info: https://kubernetes.io/docs/concepts/overview/working-with-objects/names/#names
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [DefaultValue(null)]
        public string Name { get; set; }
    }
}
