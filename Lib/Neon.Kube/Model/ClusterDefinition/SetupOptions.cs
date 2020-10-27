//-----------------------------------------------------------------------------
// FILE:	    SetupOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Kube
{
    /// <summary>
    /// Specifies setup related options.
    /// </summary>
    public class SetupOptions
    {
        private const int defaultStepStaggerSeconds = 5;

        /// <summary>
        /// <para>
        /// Indicates that cluster prepare and setup should be run in <b>debug mode</b>.
        /// This is intended to help debugging cluster setup issues by having scripts
        /// uploaded multiple times at different stages of setup so that setup can
        /// be restarted with new scripts without having to restart setup from the
        /// beginning.  This defaults to <c>false</c>.
        /// </para>
        /// <note>
        /// This is intended for use by cluster developers.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Debug", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        [Obsolete("Deprecated: Use the [neon-cli --debug] option instead.", error:true)]
        public bool Debug { get; set; } = false;

        /// <summary>
        /// <para>
        /// Specifies the maximum delay to be added between steps at strategic points 
        /// during cluster preparation and setup to help mitigate potential problems 
        /// when mutiple cluster nodes are trying to access the same Internet resources,
        /// potentially getting throttled by the remote endpoint.
        /// </para>
        /// <para>
        /// This defaults to <b>5 seconds</b> between these steps  Set this to 0 to disable
        /// the delay.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "StepStaggerSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultStepStaggerSeconds)]
        public int StepStaggerSeconds { get; set; } = defaultStepStaggerSeconds;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            if (StepStaggerSeconds < 0)
            {
                throw new ClusterDefinitionException($"[{nameof(SetupOptions)}.{nameof(StepStaggerSeconds)}={StepStaggerSeconds}] cannot be negative.");
            }
        }
    }
}
