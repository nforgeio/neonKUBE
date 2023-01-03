//-----------------------------------------------------------------------------
// FILE:	    AwsHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// AWS helpers.
    /// </summary>
    public static class AwsHelper
    {
        /// <summary>
        /// The maximum number of nodes currently allowed in a cluster deployed to AWS.
        /// </summary>
        public const int MaxClusterNodes = 100;

        /// <summary>
        /// The maximum number of hosted load balanced TCP/UDP endpoints allowed in a 
        /// cluster deployed to AWS.
        /// </summary>
        public const int MaxHostedEndpoints = 150;

        /// <summary>
        /// Converts the requested disk size in bytes to the actual required size of the AWS
        /// managed disk in GiB.
        /// </summary>
        /// <param name="volumeType">Specifies the disk storage type.</param>
        /// <param name="driveSizeBytes">The requested size in bytes.</param>
        /// <returns>The actual AWS volume size in GiB.</returns>
        /// <remarks>
        /// We're not going to allow volumes smaller than 32 GiB or larger than 16 TiB,
        /// the current AWS limit for volumes with 4 KiB blocks.  We're also going to
        /// round up to the nearest GiB.
        /// </remarks>
        public static decimal GetVolumeSizeGiB(AwsVolumeType volumeType, decimal driveSizeBytes)
        {
            var driveSizeGiB = driveSizeBytes / ByteUnits.GibiBytes;

            if (driveSizeGiB < 32)
            {
                driveSizeGiB = 32;  // Minimum supported volume size
            }
            else if (driveSizeGiB > 16 * 1024)
            {
                driveSizeGiB = 16 * 1024;
            }

            if (driveSizeGiB < KubeConst.MinNodeDiskSizeGiB)
            {
                driveSizeGiB = KubeConst.MinNodeDiskSizeGiB;
            }

            return Math.Ceiling(driveSizeGiB);
        }
    }
}
