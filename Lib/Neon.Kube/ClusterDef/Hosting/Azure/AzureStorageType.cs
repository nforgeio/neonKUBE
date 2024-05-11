//-----------------------------------------------------------------------------
// FILE:        AzureStorageType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Runtime.Serialization;
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
    /// <para>
    /// Enumerates the possible Azure storage account types.  Microsoft explains
    /// their disk types here:
    /// </para>
    /// <para>
    /// <a href="https://docs.microsoft.com/en-us/azure/virtual-machines/windows/disks-types">https://docs.microsoft.com/en-us/azure/virtual-machines/windows/disks-types</a>
    /// </para>
    /// </summary>
    public enum AzureStorageType
    {
        /// <summary>
        /// Indicates that the default Azure storage type will be provisioned.
        /// When <see cref="AzureNodeOptions.StorageType"/>=<see cref="Default"/>
        /// then <see cref="AzureHostingOptions.DefaultStorageType"/> will be provisioned.
        /// If <see cref="AzureHostingOptions.DefaultStorageType"/>=<see cref="Default"/>
        /// then <see cref="StandardSSD"/> will be provisioned.
        /// </summary>
        [EnumMember(Value = "default")]
        Default = 0,

        /// <summary>
        /// Standard managed spinning drives are quite slow but are
        /// also very inexpensive.  These may be suited for test or latency
        /// insensitive clusters.  These are available in sizes up to 32TiB.
        /// </summary>
        [EnumMember(Value = "standard-hdd")]
        StandardHDD,

        /// <summary>
        /// Managed SSD based drives  are a cost effect option that offers
        /// better latancy and reliability than <see cref="StandardHDD"/>.
        /// These are available in sizes up to 32TiB.
        /// </summary>
        [EnumMember(Value = "standard-ssd")]
        StandardSSD,

        /// <summary>
        /// Premium managed SSD drives deliver high througput and low latency and
        /// are suitable for I/O intensive workloads.  These are available in sizes
        /// up to 32TiB.
        /// </summary>
        [EnumMember(Value = "premium-ssd")]
        PremiumSSD,

        /// <summary>
        /// <para>
        /// Premium v2 managed SSD drives are intended for the very demanding I/O
        /// workloads.  These have substantially more I/O throughput than <see cref="PremiumSSD"/>
        /// disks and also range in size up to 64TiB (double <see cref="PremiumSSD"/>)..
        /// </para>
        /// <note>
        /// Azure doesn't support using <see cref="PremiumSSDv2"/> disks as virtual machine OS
        /// disks.  NeonKUBE will quietly substitude a <see cref="PremiumSSD"/> in this case.
        /// </note>
        /// </summary>
        [EnumMember(Value = "premium-ssd-v2")]
        PremiumSSDv2,

        /// <summary>
        /// <para>
        /// Ultra managed SSD drives are intended for the most demanding I/O
        /// workloads.  These range in size up to 64TiB.
        /// </para>
        /// <note>
        /// Azure doesn't support using <see cref="UltraSSD"/> disks as virtual machine OS
        /// disks.  NeonKUBE will quietly substitude a <see cref="PremiumSSD"/> in this case.
        /// </note>
        /// </summary>
        [EnumMember(Value = "ultra-ssd")]
        UltraSSD
    }
}
