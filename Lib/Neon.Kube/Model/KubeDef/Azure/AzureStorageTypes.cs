//-----------------------------------------------------------------------------
// FILE:	    AzureStorageTypes.cs
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
    /// <para>
    /// Enumerates the possible Azure storage account types.  Microsoft explains
    /// their disk types here:
    /// </para>
    /// <para>
    /// <a href="https://docs.microsoft.com/en-us/azure/virtual-machines/windows/disks-types">https://docs.microsoft.com/en-us/azure/virtual-machines/windows/disks-types</a>
    /// </para>
    /// </summary>
    public enum AzureStorageTypes
    {
        /// <summary>
        /// Standard managed spinning drives are quite slow but are
        /// also very inexpensive.  These may be suited for test or latency
        /// insensitive clusters.  These are available in sizes up to 2GiB
        /// and are limited to 500 IOPS/sec.
        /// </summary>
        StandardHDD,

        /// <summary>
        /// Managed SSD based drives  are a cost effect option that offers
        /// better latancy and reliability than <see cref="StandardHDD"/>.
        /// These are available in sizes up to 256GiB and have the same low
        /// IOPS limit as <see cref="StandardHDD"/> at 500/sec.
        /// </summary>
        StandardSSD,

        /// <summary>
        /// Premium managed SSD drives deliver high througput and low latency and
        /// are suitable for I/O intensive workloads.  These are available up to
        /// 256GiB and the largest drives are provisioned with higher IOPS and
        /// throughput.  IOPS range is 120-1100/sec and throughput range is
        /// 25MiB-125MiB/sec guaranteed.  Premium drives can also temporarily
        /// burst to 3500 IOPS/sec and 170MiB/sec for up to 30 minutes.
        /// </summary>
        PremiumSSD,

        /// <summary>
        /// <para>
        /// Ultra managed SSD drives are intended for the most demanding I/O
        /// workloads.  These range in size up to 64TiB and can support thoughput
        /// up to 2GiB/sec and IPOS up to 160000/sec.
        /// </para>
        /// <note>
        /// These are still relatively new and your region may not be able to
        /// attach ultra drives to all VM instance types.
        /// </note>
        /// </summary>
        UltraSSD,
    }
}
