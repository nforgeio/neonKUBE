//-----------------------------------------------------------------------------
// FILE:        StringMatch.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// Describes how to match a given string in HTTP headers. Match is case-sensitive.
    /// </summary>
    public class StringMatch : IValidate
    {
        /// <summary>
        /// Initializes a new instance of the StringMatch class.
        /// </summary>
        public StringMatch()
        {
        }

        /// <summary>
        /// <para>
        /// exact string match
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "exact", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Exact { get; set; }

        /// <summary>
        /// prefix-based match
        /// </summary>
        [JsonProperty(PropertyName = "prefix", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Prefix { get; set; }

        /// <summary>
        /// RE2 style regex-based match (https://github.com/google/re2/wiki/Syntax).
        /// </summary>
        [JsonProperty(PropertyName = "regex", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Regex { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
            var selected = 0;
            if (string.IsNullOrEmpty(Exact))
            {
                selected += 1;
            }
            if (string.IsNullOrEmpty(Prefix))
            {
                selected += 1;
            }
            if (string.IsNullOrEmpty(Regex))
            {
                selected += 1;
            }

            if (selected != 1)
            {
                throw new ArgumentException($"Only 1 of ({nameof(Exact)}, {nameof(Prefix)}, {nameof(Regex)} may be selected.");
            }
        }
    }
}
