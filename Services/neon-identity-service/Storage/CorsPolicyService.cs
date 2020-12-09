//-----------------------------------------------------------------------------
// FILE:	    CorsPolicyService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Service;

using IdentityServer4;
using IdentityServer4.Stores;
using IdentityServer4.Models;
using IdentityServer4.Services;

using Npgsql;

namespace NeonIdentityService
{
    /// <summary>
    /// Implements the <see cref="ICorsPolicyService"/> extension for our custom Postgres/Yugabyte database.
    /// </summary>
    public class CorsPolicyService : ICorsPolicyService
    {
        /// <summary>
        /// Determines whether an origin is allowed access.
        /// </summary>
        /// <param name="origin">Identifies the origin.</param>
        /// <returns><c>true</c> if access is allowed.</returns>
        public Task<bool> IsOriginAllowedAsync(string origin)
        {
            throw new NotImplementedException();
        }
    }
}
