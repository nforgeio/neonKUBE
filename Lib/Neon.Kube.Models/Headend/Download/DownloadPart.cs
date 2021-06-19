//-----------------------------------------------------------------------------
// FILE:	    DownloadPart.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Text;

using Newtonsoft.Json;

namespace Neon.Kube.Models.Headend
{
    /// <summary>
    /// Downloads may be split into one or more parts.  This class includes the
    /// zero-based part <see cref="Number"/> which specifies the order in which
    /// this part will be assembled back into the reconsitituted download.  The
    /// class also includes the <see cref="Uri"/> used to retrieve the part,
    /// the part <see cref="Size"/>, as well as the optional <see cref="Md5"/>
    /// has for the part.
    /// </summary>
    public class DownloadPart
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public DownloadPart()
        {
        }

        /// <summary>
        /// The zero-based index specifying where this part will be assembled
        /// back into the reconsitituted download.
        /// </summary>
        [JsonProperty(PropertyName = "Number")]
        public int Number { get; set; }

        /// <summary>
        /// The URI to the part data.
        /// </summary>
        [JsonProperty(PropertyName = "Uri")]
        public string Uri { get; set; }

        /// <summary>
        /// Actual size of the part in bytes after being downloaded and potentially decompressed. 
        /// </summary>
        [JsonProperty(PropertyName = "Size")]
        public long Size { get; set; }

        /// <summary>
        /// Optionally set to the MD5 hash of the part data (without any compression).
        /// </summary>
        [JsonProperty(PropertyName = "Md5")]
        public string Md5 { get; set; }
    }
}
