//-----------------------------------------------------------------------------
// FILE:	    NamespaceDescription.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using Neon.Common;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// Information returned by <see cref="TemporalClient.DescribeNamespaceAsync(string)"/>.
    /// </summary>
    public class NamespaceDescription
    {
        /// <summary>
        /// The namespace information.
        /// </summary>
        public NamespaceInfo NamespaceInfo { get; set; }

        /// <summary>
        /// The namespace configuration.
        /// </summary>
        public NamespaceConfig Config { get; set; }

        /// <summary>
        /// The namespace replication configuration.
        /// </summary>
        public NamespaceReplicationConfig ReplicationConfig { get; set; }

        /// <summary>
        /// The namespace failover version.
        /// </summary>
        public long FailoverVersion { get; set; }

        /// <summary>
        /// Indicates whether the namespace is global.
        /// </summary>
        public bool IsGlobalNamespace { get; set; }
    }
}
