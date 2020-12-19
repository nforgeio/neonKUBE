//-----------------------------------------------------------------------------
// FILE:	    LayerInfo.cs
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
    /// Describes a layer from a container manifest.
    /// </summary>
    public class LayerInfo
    {
        /// <summary>
        /// The layer ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The parent layer ID or <c>null</c> for the root layer.
        /// </summary>
        public string ParentId { get; set; }

        /// <summary>
        /// The compressed layer size in bytes.
        /// </summary>
        public long CompressedSize { get; set; }

        /// <summary>
        /// Returns <c>true</c> for root layers.
        /// </summary>
        public bool IsRoot => ParentId == null;

        /// <summary>
        /// Returns <c>true</c> if the layer is shared by multiple required
        /// container images.
        /// </summary>
        public bool IsShared { get; set; }
    }
}
