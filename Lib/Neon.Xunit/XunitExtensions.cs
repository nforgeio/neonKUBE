//-----------------------------------------------------------------------------
// FILE:	    XunitExtensions.cs
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
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Data;

namespace Neon.Xunit
{
    /// <summary>
    /// Unit test related extensions.
    /// </summary>
    public static class XunitExtensions
    {
        //---------------------------------------------------------------------
        // IGeneratedServiceClient extensions

        /// <summary>
        /// <para>
        /// Compares the service model implemented by the generated service client against
        /// the actual ASP.NET service controller implementation.  This ensures that the
        /// generated client actually matches the controller implementation.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> You should always include a call to this in your service unit
        /// tests to ensure that the service models used to generate the service clients 
        /// actually match the service as implemented.  It is very likely for definitions
        /// and implementations to diverge over time.
        /// </note>
        /// </summary>
        /// <typeparam name="TServiceImplementation">The service controller implementation type.</typeparam>
        /// <param name="client">The service client implementation being tested.</param>
        /// <exception cref="IncompatibleServiceException">Thrown when the service implementaton doesn't match the generated client.</exception>
        public static void ValidateController<TServiceImplementation>(this IGeneratedServiceClient client)
        {
            // 
        }
    }
}
