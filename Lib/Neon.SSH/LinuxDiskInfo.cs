//-----------------------------------------------------------------------------
// FILE:	    LinuxDiskInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

using Renci.SshNet;
using Renci.SshNet.Common;

// $todo(jefflill):
//
// The download methods don't seem to be working for paths like [/proc/meminfo].
// They return an empty stream.

namespace Neon.SSH
{
    /// <summary>
    /// Holds information about a Linux disk and its partitions.
    /// </summary>
    public class LinuxDiskInfo
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="diskName">The disk name.</param>
        /// <param name="size">The disk size in bytes.</param>
        /// <param name="isReadonly">Indicates whether the disk is removable.</param>
        /// <param name="isRemovable">Indicates whether the disk is read-only.</param>
        /// <param name="partitions">The disk partitions or <c>null</c>.</param>
        public LinuxDiskInfo(string diskName, long size, bool isRemovable, bool isReadonly, List<LinuxDiskPartition> partitions)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(diskName), nameof(diskName));
            Covenant.Requires<ArgumentException>(diskName.StartsWith("/dev/"), nameof(diskName));

            partitions = partitions ?? new List<LinuxDiskPartition>();

            this.DiskName    = diskName;
            this.Size        = size;
            this.IsRemovable = IsRemovable;
            this.IsReadOnly  = isReadonly;
            this.Partitions  = partitions.AsReadOnly();
        }

        /// <summary>
        /// Returns the disk name.
        /// </summary>
        public string DiskName { get; private set; }

        /// <summary>
        /// Disk size in bytes.
        /// </summary>
        public long Size { get; private set; }

        /// <summary>
        /// Indicates whether the disk is removable.
        /// </summary>
        public bool IsRemovable { get; private set; }

        /// <summary>
        /// Indicates whether the disk is read-only.
        /// </summary>
        public bool IsReadOnly { get; private set; }

        /// <summary>
        /// Returns the the disk partitions.
        /// </summary>
        public IList<LinuxDiskPartition> Partitions { get; private set; }
    }

    /// <summary>
    /// Holds information about a Linux disk partition.
    /// </summary>
    public class LinuxDiskPartition
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="partition">The partition number.</param>
        /// <param name="partitionName">The partition name.</param>
        /// <param name="partitionSize">The partition size in bytes.</param>
        /// <param name="mountPoint">The partition mount point or <c>null</c>.</param>
        public LinuxDiskPartition(int partition, string partitionName, long partitionSize, string mountPoint)
        {
            Covenant.Requires<ArgumentException>(partition > 0, nameof(partition));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(partitionName), nameof(partitionName));
            Covenant.Requires<ArgumentException>(partitionName.StartsWith("/dev/"), nameof(partitionName));

            this.Partition     = partition;
            this.PartitionName = partitionName;
            this.PartitionSize = partitionSize;
        }

        /// <summary>
        /// Returns the partition number (1..4).
        /// </summary>
        public int Partition { get; private set; }

        /// <summary>
        /// Returns the partition name.
        /// </summary>
        public string PartitionName { get; private set; }

        /// <summary>
        /// Returns the partition size in bytes.
        /// </summary>
        public long PartitionSize { get; private set; }

        /// <summary>
        /// Returns the current partition mount point or <c>null</c>.
        /// </summary>
        public string MountPoint { get; private set; }
    }
}
