//-----------------------------------------------------------------------------
// FILE:	    KubeClientInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// Describes client related information such as help, GitHub repo links as well
    /// as available update.
    /// </summary>
    public class KubeClientInfo
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeClientInfo()
        {
        }

        /// <summary>
        /// Returns the neonKUBE help URL.
        /// </summary>
        [JsonProperty(PropertyName = "HelpUrl", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "helpUrl", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string HelpUrl { get; set; }

        /// <summary>
        /// Returns the neonKUBE GitHub repository URL.
        /// </summary>
        [JsonProperty(PropertyName = "GitHubUrl", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "githubUrl", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string GitHubUrl { get; set; }

        /// <summary>
        /// Returns the URL for the installed release notes.
        /// </summary>
        [JsonProperty(PropertyName = "ReleaseNotesUrl", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "releaseNotesUrl", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ReleaseNotesUrl { get; set; }

        /// <summary>
        /// Returns the version for the latest available neonKUBE update.
        /// This will be <c>null</c> when there are no updates.
        /// </summary>
        [JsonProperty(PropertyName = "UpdateVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "updateVersion", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string UpdateVersion { get; set; }

        /// <summary>
        /// Returns the URL for the latest available neonKUBE update.
        /// This will be <c>null</c> when there are no updates.
        /// </summary>
        [JsonProperty(PropertyName = "UpdateUrl", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "updateUrl", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string UpdateUrl { get; set; }

        /// <summary>
        /// Returns the URL for the latest available neonKUBE update
        /// release notes.  This will be <c>null</c> when there are 
        /// no updates.
        /// </summary>
        [JsonProperty(PropertyName = "UpdateReleaseNotesUrl", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "updateReleaseNotesUrl", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string UpdateReleaseNotesUrl { get; set; }
    }
}
