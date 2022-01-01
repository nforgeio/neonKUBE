//-----------------------------------------------------------------------------
// FILE:	    SchemaStatus.cs
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
    /// Enumerates the possible keyspace states as returned by <see cref="SchemaManager.GetStatusAsync()"/>.
    /// </summary>
    public enum SchemaStatus
    {
        /// <summary>
        /// The database doesn't exist.
        /// </summary>
        NotFound,

        /// <summary>
        /// The database exists but has no <see cref="SchemaManager.DbInfoTableName"/> table 
        /// with any schema information.
        /// </summary>
        ExistsNoSchema,

        /// <summary>
        /// The database exists with schema information.
        /// </summary>
        ExistsWithSchema,

        /// <summary>
        /// Another updater is currently updating the database or has failed before
        /// completing the update.
        /// </summary>
        Updating,

        /// <summary>
        /// An error occured during the previous update indicating that the database
        /// schema may have been partially updated.  It's likely that manual intervention
        /// may be necessary to rollback to the previous schema version or manually
        /// apply the remaining updates.
        /// </summary>
        UpgradeError
    }
}
