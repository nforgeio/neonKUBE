// -----------------------------------------------------------------------------
// FILE:        KubeConfigPreferences.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using k8s.KubeConfigModels;

using Newtonsoft.Json;

using YamlDotNet.Serialization;
using Neon.Kube.K8s;

namespace Neon.Kube.Config
{
    /// <summary>
    /// Holds peferences values for a <see cref="KubeConfig"/>.
    /// </summary>
    public class KubeConfigPreferences
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeConfigPreferences()
        {
        }

        /// <summary>
        /// <i>No description provided.</i>
        /// </summary>
        [JsonProperty(PropertyName = "colors", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "colors", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)]
        [DefaultValue(false)]
        public bool Colors { get; set; } = false;

        /// <summary>
        /// Lists any custom extension properties.  Extensions are name/value pairs added
        /// by vendors to hold arbitrary information.  Take care to choose property names
        /// that are unlikely to conflict with properties created by other vendors by adding
        /// a custom prefix like <b>neonkube.io.MY-PROPERTY</b>, where <b>MY-PROPERTY</b> 
        /// identifies the property and <b>neonkibe.io</b> helps avoid conflicts.
        /// </summary>
        [JsonProperty(PropertyName = "extensions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "extensions", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)]
        [DefaultValue(null)]
        public List<NamedExtension> Extensions { get; set; }

        /// <summary>
        /// Returns an extension value.
        /// </summary>
        /// <typeparam name="T">Specifies the value type.</typeparam>
        /// <param name="name">Specifies the extension name.</param>
        /// <param name="default">Specifies the value to be returned when the extension is not found.</param>
        /// <returns>The extension value.</returns>
        public T GetExtensionValue<T>(string name, T @default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            if (Extensions == null)
            {
                return @default;
            }

            try
            {
                return Extensions.Get<T>(name, @default);
            }
            catch
            {
                return @default;
            }
        }

        /// <summary>
        /// Sets an extension value.
        /// </summary>
        /// <typeparam name="T">Specifies the value type.</typeparam>
        /// <param name="name">Specifies the extension name.</param>
        /// <param name="value">Specifies the value being set.</param>
        public void SetExtensionValue<T>(string name, T value)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            if (Extensions == null)
            {
                Extensions = new List<NamedExtension>();
            }

            Extensions.Set<T>(name, value);
        }
    }
}
