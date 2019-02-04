//-----------------------------------------------------------------------------
// FILE:	    ManagerExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase.Lite;

using Neon.Common;
using Neon.DynamicData;

namespace Couchbase.Lite
{
    /// <summary>
    /// <see cref="Manager"/> extension methods.
    /// </summary>
    public static class ManagerExtensions
    {
        /// <summary>
        /// Opens the named <see cref="EntityDatabase"/>, creating one if it doesn't exist.
        /// </summary>
        /// <param name="manager">The database manager.</param>
        /// <param name="name">The database name.</param>
        /// <returns>The <see cref="EntityDatabase"/>.</returns>
        public static EntityDatabase GetEntityDatabase(this Manager manager, string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            return EntityDatabase.From(manager.GetDatabase(name));
        }

        /// <summary>
        /// Opens the named <see cref="EntityDatabase"/> if it exists.
        /// </summary>
        /// <param name="manager">The database manager.</param>
        /// <param name="name">The database name.</param>
        /// <returns>The <see cref="EntityDatabase"/> or <c>null</c> if the database doesn't exist.</returns>
        public static EntityDatabase GetExistingEntityDatabase(this Manager manager, string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            var database = manager.GetExistingDatabase(name);

            if (database == null)
            {
                return null;
            }

            return EntityDatabase.From(database);
        }
    }
}
