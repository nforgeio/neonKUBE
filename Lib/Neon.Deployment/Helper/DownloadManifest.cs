//-----------------------------------------------------------------------------
// FILE:	    DownloadManifest.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

using Newtonsoft.Json;

namespace Neon.Deployment
{
    /// <summary>
    /// Describes a download including its parts
    /// </summary>
    public class DownloadManifest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public DownloadManifest()
        {
        }

        /// <summary>
        /// Identifies the download.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        public string Name { get; set; }

        /// <summary>
        /// The download version (this may be <c>null</c>).
        /// </summary>
        [JsonProperty(PropertyName = "Version", Required = Required.Always)]
        public string Version { get; set; }

        /// <summary>
        /// The download file name.
        /// </summary>
        [JsonProperty(PropertyName = "Filename", Required = Required.AllowNull)]
        public string Filename { get; set; }

        /// <summary>
        /// The overall size of the download.
        /// </summary>
        [JsonProperty(PropertyName = "Size", Required = Required.Always)]
        public long Size { get; set; }

        /// <summary>
        /// The MD5 hash for the entire download.
        /// </summary>
        [JsonProperty(PropertyName = "Md5", Required = Required.Always)]
        public string Md5 { get; set; }

        /// <summary>
        /// The download parts.
        /// </summary>
        [JsonProperty(PropertyName = "Parts", Required = Required.Always)]
        public List<DownloadPart> Parts { get; set; } = new List<DownloadPart>();
    }
}
