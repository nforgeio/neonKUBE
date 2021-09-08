//-----------------------------------------------------------------------------
// FILE:	    Service.cs
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
using System.Linq;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Service;

namespace NeonIdentityOperator
{
    /// <summary>
    /// Implements the <c>neon-identity-operator</c> that manages <b>neon-identity-service</b>
    /// instances as well as the related identity database.
    /// </summary>
    public partial class Service : NeonService
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="serviceMap">Optionally specifies the service map.</param>
        public Service(string name, ServiceMap serviceMap = null)
            : base(name, serviceMap: serviceMap)
        {
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected async override Task<int> OnRunAsync()
        {
            // Read and verify the environment variables.

            var connectionString = GetEnvironmentVariable("CONNECTION_STRING");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Log.LogError("[CONNECTION_STRING] environment variable is blank or missing.");
            }

            // Indicate that the service is ready.

            await SetRunningAsync();

            // Wait for the process terminator to signal that the service is stopping.

            await Terminator.StopEvent.WaitAsync();
            Terminator.ReadyToExit();

            return 0;
        }
    }
}
