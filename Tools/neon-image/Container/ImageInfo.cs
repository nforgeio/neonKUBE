//-----------------------------------------------------------------------------
// FILE:	    ImageInfo.cs
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
using System.Diagnostics.Contracts;

namespace NeonImage
{
    /// <summary>
    /// Holds information about a Docker container image.
    /// </summary>
    public class ImageInfo
    {
        private LayerInfo           rootLayer      = null;
        private List<LayerInfo>     layers         = null;
        private long                compressedSize = -1;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The image name including the tag.</param>
        public ImageInfo(string name)
        {
            this.Name = name;
        }

        /// <summary>
        /// Returns the image name including the tag.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Maps a layer ID to the layer information.
        /// </summary>
        public Dictionary<string, LayerInfo> IdToLayer = new Dictionary<string, LayerInfo>();

        /// <summary>
        /// Returns the container layers in order from the first to last.
        /// </summary>
        public List<LayerInfo> Layers
        {
            get
            {
                if (layers == null)
                {
                    // This is not a terribly efficent way to order the layers but
                    // we're going to cache the result so I'm not going to worry
                    // too much about it.

                    layers = new List<LayerInfo>();
                    layers.Add(RootLayer);

                    while (layers.Count < IdToLayer.Count)
                    {
                        var nextLayer = IdToLayer.Values.FirstOrDefault(layer => layer.ParentId == layers.Last().Id);

                        Covenant.Assert(nextLayer != null);
                        layers.Add(nextLayer);
                    }
                }

                return layers;
            }
        }

        /// <summary>
        /// Returns the container images root layer.
        /// </summary>
        public LayerInfo RootLayer
        {
            get
            {
                if (this.rootLayer == null)
                {
                    this.rootLayer = IdToLayer.Values.FirstOrDefault(LayerInfo => LayerInfo.ParentId == null);
                }

                return this.rootLayer;
            }
        }

        /// <summary>
        /// Returns the approximate total size of the compressed container.
        /// </summary>
        public long CompressedSize
        {
            get
            {
                if (this.compressedSize < 0)
                {
                    this.compressedSize = IdToLayer.Values.Sum(layer => layer.CompressedSize);
                }

                return this.compressedSize;
            }
        }
    }
}
