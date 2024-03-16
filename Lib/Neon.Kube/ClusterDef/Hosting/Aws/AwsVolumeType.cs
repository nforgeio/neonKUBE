//-----------------------------------------------------------------------------
// FILE:        AwsVolumeType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
    /// Enumerates the AWS EBS volume types.
    /// </summary>
    public enum AwsVolumeType
    {
        /// <summary>
        /// Indicates that the default Azure storage type will be provisioned.
        /// When <see cref="AzureNodeOptions.StorageType"/>=<see cref="Default"/>
        /// then <see cref="AzureHostingOptions.DefaultStorageType"/> will be provisioned.
        /// If <see cref="AzureHostingOptions.DefaultStorageType"/>=<see cref="Default"/>
        /// then <see cref="AwsVolumeType.Gp2"/> will be provisioned.
        /// </summary>
        [EnumMember(Value = "default")]
        Default = 0,

        /// <summary>
        /// <b>HDD:</b> Useful for workloads need to process large amounts of data cost effectively.
        /// </summary>
        /// <remarks>
        /// These volumes are backed by hard disk drives (HDDs) and is ideal for frequently accessed, throughput-intensive workloads
        /// with large datasets and large I/O sizes, such as MapReduce, Kafka, log processing, data warehouse, and ETL workloads. 
        /// These volumes deliver performance, measured in MB/s of throughput, and include the ability to burst up to 250 MB/s per TB,
        /// with a baseline throughput of 40 MB/s per TB and a maximum throughput of 500 MB/s per volume. st1 is designed to deliver 
        /// the expected throughput performance 99% of the time and has enough I/O credits* to support a full-volume scan at the burst 
        /// rate. To maximize the performance of st1, we recommend using EBS-optimized EC2 instances.
        /// </remarks>
        [EnumMember(Value = "st1")]
        St1,

        /// <summary>
        /// <b>HDD:</b> The lowest cost storage option.
        /// </summary>
        /// <remarks>
        /// These volumes are backed by hard disk drives (HDDs) and provides the lowest cost per GB of all EBS volume types. It is ideal
        /// for less frequently accessed workloads with large, cold datasets. Similar to st1, sc1 provides a burst model. These volumes
        /// can burst up to 80 MB/s per TB, with a baseline throughput of 12 MB/s per TB and a maximum throughput of 250 MB/s per volume. 
        /// For infrequently accessed data, sc1 provides extremely inexpensive storage. sc1 is designed to deliver the expected throughput 
        /// performance 99% of the time and has enough I/O credits* to support a full-volume scan at the burst rate. To maximize the
        /// performance of sc1, we recommend using EBS-optimized EC2 instances. 
        /// </remarks>
        [EnumMember(Value = "sc1")]
        Sc1,

        /// <summary>
        /// <b>SSD:</b> This is the default EBS volume type and is suitable for a wide range of workloads.
        /// </summary>
        /// <remarks>
        /// These volumes are backed by solid-state drives (SSDs) and are suitable for a broad range of transactional workloads, 
        /// including dev/test environments, low-latency interactive applications, and boot volumes. gp2 is designed to offer
        /// single-digit millisecond latency, deliver a consistent baseline performance of 3 IOPS/GB (minimum 100 IOPS) to a 
        /// maximum of 16,000 IOPS, and provide up to 250 MB/s of throughput per volume. gp2 volumes smaller than 1 TB 
        /// can also burst up to 3,000 IOPS. I/O operations are included in the price of gp2, so you only pay for each GB of 
        /// storage you provision. gp2 is designed to deliver the provisioned performance 99% of the time. If you need a greater
        /// number of IOPS than gp2 can provide, such as a workload where low latency is critical or you need better performance
        /// consistency, we recommend using io1. To maximize the performance of gp2, we recommend using EBS-optimized EC2 instances.
        /// </remarks>
        [EnumMember(Value = "gp2")]
        Gp2,

        /// <summary>
        /// <b>SSD:</b> Useful for critical database and application workloads.
        /// </summary>
        /// <remarks>
        /// <para>
        /// io1 is backed by solid-state drives (SSDs) and is a high performance EBS storage option designed for critical, I/O 
        /// intensive database and application workloads, as well as throughput-intensive database and data warehouse workloads,
        /// such as HBase, Vertica, and Cassandra. These volumes are ideal for both IOPS-intensive and throughput-intensive 
        /// workloads that require low latency and have moderate durability requirements or include built-in application redundancy.
        /// </para>
        /// <para>
        /// io1 is designed to deliver a consistent baseline performance of up to 50 IOPS/GB to a maximum of 64,000 IOPS and 
        /// provide up to 1,000 MB/s of throughput per volume. To maximize the benefit of io1, we recommend using EBS-optimized 
        /// EC2 instances. When attached to EBS-optimized EC2 instances, io1 is designed to achieve single-digit millisecond 
        /// latencies and is designed to deliver the provisioned performance 99.9% of the time. For more information about 
        /// instance types that can be launched as EBS-optimized instances, see Amazon EC2 Instance Types. For more information 
        /// about Amazon EBS performance guidelines, see Increasing EBS Performance.
        /// </para>
        /// <para>
        /// To achieve the limit of 64,000 IOPS and 1,000 MB/s throughput, the volume must be attached to an EC2 instance built
        /// on the AWS Nitro System.
        /// </para>
        /// </remarks>
        [EnumMember(Value = "io1")]
        Io1,

        /// <summary>
        /// <b>SSD:</b> The latest generation of SSD volume offering the best performance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// io2 is the latest generation of the Provisioned IOPS SSD volumes that is designed to provide 100X durability of 99.999% 
        /// as well as a 10X higher IOPS to storage ratio of 500 IOPS for every provisioned GB –at the same price as the previous
        /// generation (io1). io2 is a high performance EBS storage option designed for business-critical, I/O intensive database 
        /// applications, such as SAP HANA, Oracle, Microsoft SQL Server, and IBM DB2 that have high durability requirements.
        /// </para>
        /// <para>
        /// io2 is designed to deliver a consistent baseline performance of up to 500 IOPS/GB to a maximum of 64,000 IOPS. io2 volumes
        /// also provide up to 1,000 MB/s of throughput per volume. To maximize the benefit of io2, we recommend using EBS-optimized
        /// EC2 instances. When attached to EBS-optimized EC2 instances, io2 is designed to achieve single-digit millisecond 
        /// latencies and is designed to deliver the provisioned performance 99.9% of the time. For more information about 
        /// instance types that can be launched as EBS-optimized instances, see Amazon EC2 Instance Types. For more information
        /// about Amazon EBS performance guidelines, see Increasing EBS Performance.
        /// </para>
        /// <para>
        /// To achieve the limit of 64,000 IOPS and 1,000 MB/s throughput, the volume must be attached to an EC2 instance built 
        /// on the AWS Nitro System.
        /// </para>
        /// </remarks>
        [EnumMember(Value = "io2")]
        Io2
    }
}
