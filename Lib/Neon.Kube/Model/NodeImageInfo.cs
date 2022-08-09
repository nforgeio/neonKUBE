//-----------------------------------------------------------------------------
// FILE:	    NodeImageInfo.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Kube;
using Neon.IO;
using Neon.Net;
using Neon.SSH;

namespace Neon.Kube
{
    /// <summary>
    /// Holds information about a setup container image.
    /// </summary>
    public class NodeImageInfo
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="imageFolder">The image folder path.</param>
        /// <param name="imageName">The image name.</param>
        /// <param name="targetTag">The target tag.</param>
        /// <param name="registry">The source registry.</param>
        public NodeImageInfo(string imageFolder, string imageName, string targetTag, string registry)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(imageFolder), nameof(imageFolder));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(imageName), nameof(imageName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(targetTag), nameof(targetTag));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(registry), nameof(registry));

            this.Folder      = imageFolder;
            this.Name        = imageName;
            this.TargetTag   = targetTag;
            this.SourceImage = $"{registry}/{imageName}:{targetTag}";
            this.TargetImage = $"{KubeConst.LocalClusterRegistry}/{imageName}:{targetTag}";
            this.Registry    = registry;
        }

        /// <summary>
        /// Returns the image folder path.
        /// </summary>
        public string Folder { get; private set; }

        /// <summary>
        /// Returns the image name without the hoster's prefix or image tag.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the fully qualified source image name from one of the 
        /// <see cref="NeonHelper.NeonSdkProdRegistry"/> or <see cref="NeonHelper.NeonSdkDevRegistry"/>
        /// container registries and will be tagged with the cluster version.
        /// </summary>
        public string SourceImage { get; private set; }

        /// <summary>
        /// Returns the fully qualified name of the image as it will be deployed to a cluster,
        /// with the registry being set to <see cref="KubeConst.LocalClusterRegistry"/> which maps
        /// to the cluster's internal container registry.
        /// </summary>
        public string TargetImage { get; private set; }

        /// <summary>
        /// Returns the tag used for the target image persisted to the internal cluster
        /// registry.  This will be set to the original source component tag as built 
        /// for the base images.
        /// </summary>
        public string TargetTag { get; private set; }

        /// <summary>
        /// Returns the registry used for the source image.
        /// </summary>
        public string Registry { get; private set; }
    }
}
