//-----------------------------------------------------------------------------
// FILE:	    AzureStorageTypes.cs
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
    /// Enumerates the possible Azure storage account types.
    /// </summary>
    public enum AzureStorageTypes
    {
        #pragma warning disable 1591 // Disable code comment warnings

        /// <summary>
        /// Standard managed spinning drives with local redundancy.
        /// </summary>
        StandardHDD_LRS,

        /// <summary>
        /// Standard managed SSD drives with local redundancy.
        /// </summary>
        StandardSSD_LRS,

        /// <summary>
        /// Premium managed SSD drives with local redundancy.
        /// </summary>
        PremiumSSD_LRS
    }
}
