//-----------------------------------------------------------------------------
// FILE:	    AzureHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

namespace Neon.Hive
{
    /// <summary>
    /// Microsoft Azure helpers.
    /// </summary>
    public static class AzureHelper
    {
        /// <summary>
        /// The maximum number of nodes currently allowed in a neonHIVE deployed to Azure.
        /// </summary>
        public const int MaxHiveNodes = 100;

        /// <summary>
        /// The maximum number of hosted load balanced TCP/UDP endpoints allowed  in a neonHIVE deployed to Azure.
        /// This is an Azure limit.
        /// </summary>
        public const int MaxHostedEndpoints = 150;

        /// <summary>
        /// Converts the requested disk size in GB to the actual required size of the Azure
        /// managed disk in GB.
        /// </summary>
        /// <param name="storageType">Specifies the disk storage type.</param>
        /// <param name="requestedSizeGB">The requested size in GB.</param>
        /// <returns>The actual Azure disk size in GB.</returns>
        public static int GetDiskSizeGB(AzureStorageTypes storageType, int requestedSizeGB)
        {
            switch (storageType)
            {
                case AzureStorageTypes.StandardHDD_LRS:

                    // Azure currently standard HDD sizes: 32GB, 64GB, 128GB, 512GB, 1TB, 2TB, 4TB, 8TB, 16TB or 32TB.

                    if (requestedSizeGB <= 32)
                    {
                        return 32;
                    }
                    else if (requestedSizeGB <= 64)
                    {
                        return 64;
                    }
                    else if (requestedSizeGB <= 128)
                    {
                        return 128;
                    }
                    else if (requestedSizeGB <= 512)
                    {
                        return 512;
                    }
                    else if (requestedSizeGB <= 1024)
                    {
                        return 1024;
                    }
                    else if (requestedSizeGB <= 2048)
                    {
                        return 2048;
                    }
                    else if (requestedSizeGB <= 8192)
                    {
                        return 8192;
                    }
                    else if (requestedSizeGB <= 16314)
                    {
                        return 16314;
                    }
                    else
                    {
                        return 32628;
                    }

                case AzureStorageTypes.StandardSSD_LRS:

                    // Azure currently standard SSD sizes: 128GB, 512GB, 1TB, 2TB, 4TB, 8TB, 16TB or 32TB.

                    if (requestedSizeGB <= 128)
                    {
                        return 128;
                    }
                    else if (requestedSizeGB <= 512)
                    {
                        return 512;
                    }
                    else if (requestedSizeGB <= 1024)
                    {
                        return 1024;
                    }
                    else if (requestedSizeGB <= 2048)
                    {
                        return 2048;
                    }
                    else if (requestedSizeGB <= 8192)
                    {
                        return 8192;
                    }
                    else if (requestedSizeGB <= 16314)
                    {
                        return 16314;
                    }
                    else
                    {
                        return 32628;
                    }

                case AzureStorageTypes.PremiumSSD_LRS:

                    // Azure currently premium disks sizes: 32GB, 64GB, 128GB, 512GB, 1TB, 2TB, 4TB, 8TB, 16TB or 32TB.

                    if (requestedSizeGB <= 32)
                    {
                        return 32;
                    }
                    else if (requestedSizeGB <= 64)
                    {
                        return 64;
                    }
                    else if (requestedSizeGB <= 128)
                    {
                        return 128;
                    }
                    else if (requestedSizeGB <= 512)
                    {
                        return 512;
                    }
                    else if (requestedSizeGB <= 1024)
                    {
                        return 1024;
                    }
                    else if (requestedSizeGB <= 2048)
                    {
                        return 2048;
                    }
                    else if (requestedSizeGB <= 8192)
                    {
                        return 8192;
                    }
                    else if (requestedSizeGB <= 16314)
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
