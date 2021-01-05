//-----------------------------------------------------------------------------
// FILE:	    ResourceStore.cs
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
    /// Implements the <see cref="IResourceStore"/> extension for our custom Postgres/Yugabyte database.
    /// </summary>
    public class ResourceStore : IResourceStore
    {
        /// <summary>
        /// Gets API resources by API resource names.
        /// </summary>
        /// <param name="apiResourceNames">The API resource names.</param>
        /// <returns>The matching API resources.</returns>
        public Task<IEnumerable<ApiResource>> FindApiResourcesByNameAsync(IEnumerable<string> apiResourceNames)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets API resources by scope names.
        /// </summary>
        /// <param name="scopeNames">The scope names.</param>
        /// <returns>The matching API resources.</returns>
        public Task<IEnumerable<ApiResource>> FindApiResourcesByScopeNameAsync(IEnumerable<string> scopeNames)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the API scopes by scope names.
        /// </summary>
        /// <param name="scopeNames">The scope names.</param>
        /// <returns>The matching API scopes.</returns>
        public Task<IEnumerable<ApiScope>> FindApiScopesByNameAsync(IEnumerable<string> scopeNames)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets identity resources by scope names.
        /// </summary>
        /// <param name="scopeNames">The scope names.</param>
        /// <returns>The matching identity resources.</returns>
        public Task<IEnumerable<IdentityResource>> FindIdentityResourcesByScopeNameAsync(IEnumerable<string> scopeNames)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets all resources.
        /// </summary>
        /// <returns>The resopurces.</returns>
        public Task<Resources> GetAllResourcesAsync()
        {
            throw new NotImplementedException();
        }
    }
}
