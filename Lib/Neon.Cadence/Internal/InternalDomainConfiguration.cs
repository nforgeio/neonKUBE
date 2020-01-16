//-----------------------------------------------------------------------------
// FILE:	    InternalDomainConfiguration.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

using Neon.Cadence;
using Neon.Common;

using Newtonsoft.Json;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Describes a Cadence domain configuration.
    /// </summary>
    internal class InternalDomainConfiguration
    {
        /// <summary>
        /// Determines how long  workflow executions are retained.
        /// </summary>
        [JsonProperty(PropertyName = "workflowExecutionRetentionPeriodInDays", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int WorkflowExecutionRetentionPeriodInDays { get; set; }

        /// <summary>
        /// Enables metrics.
        /// </summary>
        [JsonProperty(PropertyName = "emitMetric", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public bool EmitMetric { get; set; }

        /// <summary>
        /// $todo(jefflill): Don't know what this is.
        /// </summary>
        [JsonProperty(PropertyName = "badBinaries", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalBadBinaries BadBinaries { get; set; }

        /// <summary>
        /// $todo(jefflill): Don't know what this is.
        /// </summary>
        [JsonProperty(PropertyName = "historyArchivalStatus", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public ArchivalStatus? HistoryArchivalStatus { get; set; }

        /// <summary>
        /// $todo(jefflill): Don't know what this is.
        /// </summary>
        [JsonProperty(PropertyName = "historyArchivalUri", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string HistoryArchivalUri { get; set; }

        /// <summary>
        /// $todo(jefflill): Don't know what this is.
        /// </summary>
        [JsonProperty(PropertyName = "visibilityArchivalStatus", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public ArchivalStatus? VisibilityArchivalStatus { get; set; }

        /// <summary>
        /// $todo(jefflill): Don't know what this is.
        /// </summary>
        [JsonProperty(PropertyName = "visibilityArchivalUri", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VisibilityArchivalUri { get; set; }

        /// <summary>
        /// Converts the internal instance into a public <see cref="DomainConfiguration"/>.
        /// </summary>
        /// <returns>The converted instance.</returns>
        public DomainConfiguration ToPublic()
        {
            return new DomainConfiguration()
            {
                RetentionDays = this.WorkflowExecutionRetentionPeriodInDays,
                EmitMetrics   = this.EmitMetric,
            };
        }
    }
}
