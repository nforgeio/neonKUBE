//-----------------------------------------------------------------------------
// FILE:	    ContainerManifest.cs
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;

namespace NeonImage
{
    /// <summary>
    /// Models a container manifest as returned by <b>docker manifest inspect</b> (non-verbose).
    /// </summary>
    public class ContainerManifest
    {
        // These properties are parsed from [docker manifest inspect] command results.

        public int schemaVersion { get; set; }
        public string mediaType { get; set; }
        public ContainerLayer config { get; set; }
        public List<ContainerLayer> Layers { get; set; } = new List<ContainerLayer>();

        /// <summary>
        /// Returns the total size of the container image in bytes.
        /// </summary>
        [JsonIgnore]
        public long TotalSize => config.size + Layers.Sum(layer => layer.size);
    }
}
