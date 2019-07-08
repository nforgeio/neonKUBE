//-----------------------------------------------------------------------------
// FILE:	    ICadenceClient.Domain.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    public partial interface ICadenceClient
    {
        //---------------------------------------------------------------------
        // Cadence domain related operations.

        /// <summary>
        /// Registers a Cadence domain using the specified parameters.
        /// </summary>
        /// <param name="name">The domain name.</param>
        /// <param name="description">Optionally specifies a description.</param>
        /// <param name="ownerEmail">Optionally specifies the owner's email address.</param>
        /// <param name="retentionDays">
        /// Optionally specifies the number of days to retain the history for workflows 
        /// completed in this domain.  This defaults to <b>7 days</b>.
        /// </param>
        /// <param name="securityToken">Optional security token.</param>
        /// <param name="ignoreDuplicates">
        /// Optionally ignore duplicate domain registrations.  This defaults
        /// to <c>false</c>.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceDomainAlreadyExistsException">Thrown if the domain already exists.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        Task RegisterDomainAsync(string name, string description = null, string ownerEmail = null, int retentionDays = 7, string securityToken = null, bool ignoreDuplicates = false);

        /// <summary>
        /// Describes the named Cadence domain.
        /// </summary>
        /// <param name="name">The domain name.</param>
        /// <returns>Tyhe domain description.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        Task<DomainDescription> DescribeDomainAsync(string name);

        /// <summary>
        /// Updates the named Cadence domain.
        /// </summary>
        /// <param name="name">Identifies the target domain.</param>
        /// <param name="request">The updated domain information.</param>
        /// <param name="securityToken">Optionally specifies the security token.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        Task UpdateDomainAsync(string name, DomainUpdateArgs request, string securityToken = null);
    }
}
