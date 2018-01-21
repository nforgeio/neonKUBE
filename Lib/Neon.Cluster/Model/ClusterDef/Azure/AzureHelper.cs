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

namespace Neon.Cluster
{
    /// <summary>
    /// Microsoft Azure helpers.
    /// </summary>
    public static class AzureHelper
    {
        /// <summary>
        /// The maximum number of nodes currently allowed in a neonCLUSTER deployed to Azure.
        /// </summary>
        public const int MaxClusterNodes = 100;

        /// <summary>
        /// The maximum number of hosted load balanced TCP/UDP endpoints allowed  in a neonCLUSTER deployed to Azure.
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
                case AzureStorageTypes.StandardLRS:

                    // Azure currently standard disks sizes: 32GB, 64GB, 128GB, 512GB, and 1024GB (1TB)

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
                    else
                    {
                        return 1024;
                    }

                case AzureStorageTypes.PremiumLRS:

                    // Azure currently premium disks sizes: 128GB, 512GB, and 1024GB (1TB)

                    if (requestedSizeGB <= 128)
                    {
                        return 128;
                    }
                    else if (requestedSizeGB <= 512)
                    {
                        return 512;
                    }
                    else 
                    {
                        return 1024;
                    }

                default:

                    throw new NotImplementedException();
            }
        }
    }
}
