//-----------------------------------------------------------------------------
// FILE:	    AzureHelper.cs
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
        /// Converts the requested disk size in GiB to the actual required size of the Azure
        /// managed disk in GiB.
        /// </summary>
        /// <param name="storageType">Specifies the disk storage type.</param>
        /// <param name="requestedSizeGiB">The requested size in GB.</param>
        /// <returns>The actual Azure disk size in GiB.</returns>
        public static int GetDiskSizeGiB(AzureStorageTypes storageType, int requestedSizeGiB)
        {
            switch (storageType)
            {
                case AzureStorageTypes.StandardHDD_LRS:

                    // Azure currently standard HDD sizes: 32GiB, 64GiB, 128GiB, 512GiB, 1TiB, 2TiB, 4TiB, 8TiB, 16TiB or 32TiB.

                    if (requestedSizeGiB <= 32)
                    {
                        return 32;
                    }
                    else if (requestedSizeGiB <= 64)
                    {
                        return 64;
                    }
                    else if (requestedSizeGiB <= 128)
                    {
                        return 128;
                    }
                    else if (requestedSizeGiB <= 512)
                    {
                        return 512;
                    }
                    else if (requestedSizeGiB <= 1024)
                    {
                        return 1024;
                    }
                    else if (requestedSizeGiB <= 2048)
                    {
                        return 2048;
                    }
                    else if (requestedSizeGiB <= 8192)
                    {
                        return 8192;
                    }
                    else if (requestedSizeGiB <= 16314)
                    {
                        return 16314;
                    }
                    else
                    {
                        return 32628;
                    }

                case AzureStorageTypes.StandardSSD_LRS:

                    // Azure currently standard SSD sizes: 128GB, 512GB, 1TB, 2TB, 4TB, 8TB, 16TB or 32TB.

                    if (requestedSizeGiB <= 128)
                    {
                        return 128;
                    }
                    else if (requestedSizeGiB <= 512)
                    {
                        return 512;
                    }
                    else if (requestedSizeGiB <= 1024)
                    {
                        return 1024;
                    }
                    else if (requestedSizeGiB <= 2048)
                    {
                        return 2048;
                    }
                    else if (requestedSizeGiB <= 8192)
                    {
                        return 8192;
                    }
                    else if (requestedSizeGiB <= 16314)
                    {
                        return 16314;
                    }
                    else
                    {
                        return 32628;
                    }

                case AzureStorageTypes.PremiumSSD_LRS:

                    // Azure currently premium disks sizes: 32GB, 64GB, 128GB, 512GB, 1TB, 2TB, 4TB, 8TB, 16TB or 32TB.

                    if (requestedSizeGiB <= 32)
                    {
                        return 32;
                    }
                    else if (requestedSizeGiB <= 64)
                    {
                        return 64;
                    }
                    else if (requestedSizeGiB <= 128)
                    {
                        return 128;
                    }
                    else if (requestedSizeGiB <= 512)
                    {
                        return 512;
                    }
                    else if (requestedSizeGiB <= 1024)
                    {
                        return 1024;
                    }
                    else if (requestedSizeGiB <= 2048)
                    {
                        return 2048;
                    }
                    else if (requestedSizeGiB <= 8192)
                    {
                        return 8192;
                    }
                    else if (requestedSizeGiB <= 16314)
                    {
                        return 16314;
                    }
                    else
                    {
                        return 32628;
                    }

                default:

                    throw new NotImplementedException();
            }
        }
    }
}
