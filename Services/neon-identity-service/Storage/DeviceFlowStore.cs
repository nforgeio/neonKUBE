//-----------------------------------------------------------------------------
// FILE:	    DeviceFlowStore.cs
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
    /// Implements the <see cref="DeviceFlowStore"/> extension for our custom Postgres/Yugabyte database.
    /// </summary>
    public class DeviceFlowStore : IDeviceFlowStore
    {
        /// <summary>
        /// Gets a <see cref="DeviceCode"/> by device code string.
        /// </summary>
        /// <param name="deviceCode">The device code string.</param>
        /// <returns>The <see cref="DeviceCode"/> or <c>null</c>.</returns>
        public Task<DeviceCode> FindByDeviceCodeAsync(string deviceCode)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets a <see cref="DeviceCode"/> by user code.
        /// </summary>
        /// <param name="userCode">The user code string.</param>
        /// <returns>The <see cref="DeviceCode"/> or <c>null</c>.</returns>
        public Task<DeviceCode> FindByUserCodeAsync(string userCode)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes a <see cref="DeviceCode"/> by device code string.
        /// </summary>
        /// <param name="deviceCode">The device code string.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public Task RemoveByDeviceCodeAsync(string deviceCode)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Persists a <see cref="DeviceCode"/> by its device code and user code strings.
        /// </summary>
        /// <param name="deviceCode">The device code string.</param>
        /// <param name="userCode">The user code string.</param>
        /// <param name="data">The <see cref="DeviceCode"/> being persisted.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public Task StoreDeviceAuthorizationAsync(string deviceCode, string userCode, DeviceCode data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Updates a <see cref="DeviceCode"/> by user code string.
        /// </summary>
        /// <param name="userCode">The user code string.</param>
        /// <param name="data">The <see cref="DeviceCode"/> being updated.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public Task UpdateByUserCodeAsync(string userCode, DeviceCode data)
        {
            throw new NotImplementedException();
        }
    }
}
