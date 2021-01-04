//-----------------------------------------------------------------------------
// FILE:	    KeyspaceStatus.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Neon.Common;

using Cassandra;

namespace Neon.Cassandra
{
    /// <summary>
    /// Holds information about a database's schema as returned by <see cref="SchemaManager.GetStatusAsync()"/>.
    /// </summary>
    public class KeyspaceStatus
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        internal KeyspaceStatus()
        {
        }

        /// <summary>
        /// Returns an indication of whether the database exisis and has schema information.
        /// </summary>
        public SchemaStatus SchemaStatus { get; internal set; }

        /// <summary>
        /// Returns the database's current schema version or <b>-1</b> when the
        /// database doesn't exist or have a DBINFO table.
        /// </summary>
        public int Version { get; internal set; } = -1;

        /// <summary>
        /// Returns the maximum known schema version as determined by the available
        /// schema scripts.
        /// </summary>
        public int MaxVersion { get; internal set; }

        /// <summary>
        /// Returns a dictionary that maps a schema version to the script to be used
        /// to upgrade the database to that version.
        /// </summary>
        internal Dictionary<int, string> VersionToScript { get; set; }

        /// <summary>
        /// Identifes the updater claiming to be currently upgrading the database when
        /// <see cref="SchemaStatus.SchemaStatus"/><c>=</c><see cref="SchemaStatus.Updating"/>.
        /// </summary>
        public string Updater { get; internal set; }

        /// <summary>
        /// Returns the error from a previous upgrade attempt when 
        /// <see cref="SchemaStatus"/><c>=</c><see cref="SchemaStatus.UpgradeError"/>.
        /// </summary>
        public string Error { get; internal set; }

        /// <summary>
        /// Returns <c>true</c> when the database has schema information and the current version
        /// is the same as the most recent schema script.
        /// </summary>
        public bool IsCurrent => SchemaStatus == SchemaStatus.ExistsWithSchema && Version == MaxVersion;
    }
}
