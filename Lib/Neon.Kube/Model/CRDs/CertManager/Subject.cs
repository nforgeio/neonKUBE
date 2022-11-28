//-----------------------------------------------------------------------------
// FILE:	    Subject.cs
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

namespace Neon.Kube
{
    /// <summary>
    /// Full X509 name specification (https://golang.org/pkg/crypto/x509/pkix/#Name).
    /// </summary>
    public class Subject
    {
        /// <summary>
        /// Initializes a new instance of the Subject class.
        /// </summary>
        public Subject()
        {
        }

        /// <summary>
        /// Countries to be used on the Certificate.
        /// </summary>
        [JsonProperty(PropertyName = "countries", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> Countries { get; set; }

        /// <summary>
        /// Cities to be used on the Certificate.
        /// </summary>
        [JsonProperty(PropertyName = "localities", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> Localities { get; set; }

        /// <summary>
        /// Organizational Units to be used on the Certificate.
        /// </summary>
        [JsonProperty(PropertyName = "organizationalUnits", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> OrganizationalUnits { get; set; }

        /// <summary>
        /// Postal codes to be used on the Certificate.
        /// </summary>
        [JsonProperty(PropertyName = "postalCodes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> PostalCodes { get; set; }

        /// <summary>
        /// State/Provinces to be used on the Certificate.
        /// </summary>
        [JsonProperty(PropertyName = "provinces", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> Provinces { get; set; }

        /// <summary>
        /// Serial number to be used on the Certificate.
        /// </summary>
        [JsonProperty(PropertyName = "serialNumber", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string SerialNumber { get; set; }

        /// <summary>
        /// Street addresses to be used on the Certificate.
        /// </summary>
        [JsonProperty(PropertyName = "streetAddresses", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> StreetAddresses { get; set; }
    }
}
