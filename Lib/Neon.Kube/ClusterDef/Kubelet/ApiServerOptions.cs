// -----------------------------------------------------------------------------
// FILE:	    ApiServerOptions.cs
// CONTRIBUTOR: NEONFORGE Team
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Defines Kubernetes API Server options.
    /// </summary>
    public class ApiServerOptions
    {
        /// <summary>
        /// Public default constructor.
        /// </summary>
        public ApiServerOptions()
        {
        }

        /// <summary>
        /// Specifies the API server log verbosity.  This defaults to: <b>2</b>
        /// </summary>
        /// <remarks>
        /// <para>
        /// Here are the supported verbosity values and what they do:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>1</b></term>
        ///     <description>
        ///     Log minimal details
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>2</b></term>
        ///     <description>
        ///     <b>default</b>: Log useful steady state service status and significant changes to the system
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>3</b></term>
        ///     <description>
        ///     Log extended information about changes
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>4</b></term>
        ///     <description>
        ///     Log debug information
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>5</b></term>
        ///     <description>
        ///     Undefined
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>6</b></term>
        ///     <description>
        ///     Log requested resources
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>7</b></term>
        ///     <description>
        ///     Log HTTP request headers
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>8</b></term>
        ///     <description>
        ///     Log HTTP request contents
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>9</b></term>
        ///     <description>
        ///     Log HTTP request responses
        ///     </description>
        /// </item>
        /// </list>
        /// </remarks>
        [JsonProperty(PropertyName = "Verbosity", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "verbosity", ApplyNamingConventions = false)]
        [DefaultValue(2)]
        public int Verbosity { get; set; } = 2;
    }
}
