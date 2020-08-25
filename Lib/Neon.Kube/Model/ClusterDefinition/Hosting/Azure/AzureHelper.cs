//-----------------------------------------------------------------------------
// FILE:	    AzureHelper.cs
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

namespace Neon.Kube
{
    /// <summary>
    /// Microsoft Azure helpers.
    /// </summary>
    public static class AzureHelper
    {
        /// <summary>
        /// The maximum number of nodes currently allowed in a cluster deployed to Azure.
        /// </summary>
        public const int MaxClusterNodes = 100;

        /// <summary>
        /// The maximum number of hosted load balanced TCP/UDP endpoints allowed  in a cluster deployed to Azure.
        /// This is an Azure limit.
        /// </summary>
        public const int MaxHostedEndpoints = 150;

        /// <summary>
        /// Converts the requested disk size in bytes to the actual required size of the Azure
        /// managed disk in GiB.
        /// </summary>
        /// <param name="storageType">Specifies the disk storage type.</param>
        /// <param name="driveSizeBytes">The requested size in bytes.</param>
        /// <returns>The actual Azure disk size in GiB.</returns>
        public static decimal GetDiskSizeGiB(AzureStorageTypes storageType, decimal driveSizeBytes)
        {
            var driveSizeGiB = driveSizeBytes / ByteUnits.GibiBytes;

            switch (storageType)
            {
                case AzureStorageTypes.StandardHDD:
                case AzureStorageTypes.StandardSSD:
                case AzureStorageTypes.PremiumSSD:

                    // Azure premium disks sizes: 32GiB, 64GiB, 128GiB, 512GiB, 1TiB, 2TiB, 4TiB, 8TiB, 16TiB or 32TiB.

                    if (driveSizeGiB <= 32)
                    {
                        return 32;
                    }
                    else if (driveSizeGiB <= 64)
                    {
                        return 64;
                    }
                    else if (driveSizeGiB <= 128)
                    {
                        return 128;
                    }
                    else if (driveSizeGiB <= 256)
                    {
                        return 256;
                    }
                    else if (driveSizeGiB <= 512)
                    {
                        return 512;
                    }
                    else if (driveSizeGiB <= 1024)
                    {
                        return 1024;
                    }
                    else if (driveSizeGiB <= 2048)
                    {
                        return 2048;
                    }
                    else if (driveSizeGiB <= 4096)
                    {
                        return 4096;
                    }
                    else if (driveSizeGiB <= 8192)
                    {
                        return 8192;
                    }
                    else if (driveSizeGiB <= 16314)
                    {
                        return 16314;
                    }
                    else
                    {
                        return 32768;
                    }

                case AzureStorageTypes.UltraSSD:

                    // Azure ultra disks sizes: 4GiB, 8GiB, 16GiB, 32GiB, 64GiB, 128GiB, 256GiB, 512GiB
                    //                          and 1TiB - 64TiB in 1TiB increments

                    if (driveSizeGiB < 4)
                    {
                        return 4;
                    }
                    else if (driveSizeGiB < 8)
                    {
                        return 8;
                    }
                    else if (driveSizeGiB < 16)
                    {
                        return 16;
                    }
                    else if (driveSizeGiB < 32)
                    {
                        return 32;
                    }
                    else if (driveSizeGiB < 64)
                    {
                        return 64;
                    }
                    else if (driveSizeGiB < 128)
                    {
                        return 128;
                    }
                    else if (driveSizeGiB < 256)
                    {
                        return 256;
                    }
                    else if (driveSizeGiB < 512)
                    {
                        return 512;
                    }
                    else if (driveSizeGiB < 65536)
                    {
                        // Round up to the nearest 1TiB.

                        var driveSizeTiB = driveSizeGiB / 1024;

                        if (driveSizeGiB % 1024 != 0)
                        {
                            driveSizeTiB++;
                        }

                        return driveSizeTiB * 1024;
                    }
                    else
                    {
                        return 65536;
                    }

                default:

                    throw new NotImplementedException();
            }
        }
    }
}
