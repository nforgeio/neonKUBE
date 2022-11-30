//-----------------------------------------------------------------------------
// FILE:	    Port.cs
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
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube.Resources
{
    /// <summary>
    /// Describes the properties of a specific port of a service.
    /// </summary>
    public class Port : IValidate
    {
        /// <summary>
        /// Initializes a new instance of the Port class.
        /// </summary>
        public Port()
        {
        }

        /// <summary>
        /// A valid non-negative integer port number.
        /// </summary>
        [JsonProperty(PropertyName = "number", Required = Required.Always)]
        public int Number { get; set; }

        /// <summary>
        /// The protocol exposed on the port.
        /// </summary>
        [JsonProperty(PropertyName = "protocol", Required = Required.Always)]
        public PortProtocol Protocol { get; set; }

        /// <summary>
        /// Label assigned to the port.
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        public string Name { get; set; }

        /// <summary>
        /// The port number on the endpoint where the traffic will be received. Applicable only when used with ServiceEntries.
        /// </summary>
        [JsonProperty(PropertyName = "targetPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int? TargetPort { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
            Covenant.Assert(Number > -1, "Port number must be non-negative.");
        }
    }
}
