//-----------------------------------------------------------------------------
// FILE:	    NamespaceConfiguration.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using Newtonsoft.Json;

using Neon.Common;
using Neon.Data;
using Neon.Temporal;

namespace Neon.Temporal
{
    /// <summary>
    /// Namespace configuration options.
    /// </summary>
    public class NamespaceConfig
    {
        /// <summary>
        /// The workflow history retention Ttl.
        /// </summary>
        [JsonConverter(typeof(GoDurationJsonConverter))]
        [JsonProperty(PropertyName = "workflow_execution_retention_ttl")]
        public TimeSpan? WorkflowExecutionRetentionTtl { get; set; }

        /// <summary>
        /// Set of Bad Binaries.
        /// </summary>
        [JsonProperty(PropertyName = "bad_binaries")]
        public BadBinaries BadBinaries { get; set; }

        /// <summary>
        /// Archival state of namespace history.  If unspecified then 
        /// default server configuration is used.
        /// </summary>
        [JsonConverter(typeof(IntegerEnumConverter<ArchivalState>))]
        [JsonProperty(PropertyName = "history_archival_state")]
        public ArchivalState HistoryArchivalState { get; set; }

        /// <summary>
        /// Uri of archived history.
        /// </summary>
        [JsonProperty(PropertyName = "history_archival_uri")]
        public string HistoryArchivalUri { get; set; }

        /// <summary>
        /// Archival state of namespace visibility.  If unspecified then 
        /// default server configuration is used.
        /// </summary>
        [JsonConverter(typeof(IntegerEnumConverter<ArchivalState>))]
        [JsonProperty(PropertyName = "visibility_archival_state")]
        public ArchivalState VisibilityArchivalState { get; set; }

        /// <summary>
        /// Uri of archived namespace visibility.
        /// </summary>
        [JsonProperty(PropertyName = "visibility_archival_uri")]
        public string VisibilityArchivalUri { get; set; }
    }
}
