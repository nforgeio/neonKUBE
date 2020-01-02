//-----------------------------------------------------------------------------
// FILE:	    AzureStorageTypes.cs
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
