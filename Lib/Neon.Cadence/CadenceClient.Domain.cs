//-----------------------------------------------------------------------------
// FILE:	    CadenceClient.Domain.cs
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
    public partial class CadenceClient
    {
        //---------------------------------------------------------------------
        // Cadence domain related operations.

        /// <summary>
        /// Registers a Cadence domain using the <see cref="InternalRegisterDomainRequest"/> information passed.
        /// </summary>
        /// <param name="request">The domain properties.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceDomainAlreadyExistsException">Thrown if the domain already exists.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        private async Task RegisterDomainAsync(InternalRegisterDomainRequest request)
        {
            var domainRegisterRequest =
                new DomainRegisterRequest()
                {
                    Name          = request.Name,
                    Description   = request.Description,
                    OwnerEmail    = request.OwnerEmail,
                    RetentionDays = request.RetentionDays
                };

            var reply = await CallProxyAsync(domainRegisterRequest);

            reply.ThrowOnError();
        }

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
        /// <param name="ignoreDuplicates">
        /// Optionally ignore duplicate domain registrations.  This defaults
        /// to <c>false</c>.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="CadenceDomainAlreadyExistsException">Thrown if the domain already exists.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        public async Task RegisterDomainAsync(string name, string description = null, string ownerEmail = null, int retentionDays = 7, bool ignoreDuplicates = false)
        {
            try
            {
                await RegisterDomainAsync(
                    new InternalRegisterDomainRequest()
                    {
                        Name          = name,
                        Description   = description,
                        OwnerEmail    = ownerEmail,
                        RetentionDays = retentionDays
                    });
            }
            catch (CadenceDomainAlreadyExistsException)
            {
                if (!ignoreDuplicates)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Describes the named Cadence domain.
        /// </summary>
        /// <param name="name">The domain name.</param>
        /// <returns>Tyhe domain description.</returns>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        public async Task<DomainDescription> DescribeDomainAsync(string name)
        {
            var domainDescribeRequest =
                new DomainDescribeRequest()
                {
                    Name = name,
                };

            var reply = (DomainDescribeReply)await CallProxyAsync(domainDescribeRequest);

            reply.ThrowOnError();

            return new DomainDescription()
            {
                DomainInfo = new DomainInfo()
                {
                    Description = reply.DomainInfoDescription,
                    Name        = reply.DomainInfoName,
                    OwnerEmail  = reply.DomainInfoOwnerEmail,
                    Status      = reply.DomainInfoStatus
                },

                Configuration = new DomainOptions()
                {
                    EmitMetrics   = reply.ConfigurationEmitMetrics,
                    RetentionDays = reply.ConfigurationRetentionDays
                },
            };
        }

        /// <summary>
        /// Updates the named Cadence domain.
        /// </summary>
        /// <param name="name">Identifies the target domain.</param>
        /// <param name="request">The updated domain information.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task UpdateDomainAsync(string name, DomainUpdateArgs request)
        {
            Covenant.Requires<ArgumentNullException>(request != null);
            Covenant.Requires<ArgumentNullException>(request.Options != null);
            Covenant.Requires<ArgumentNullException>(request.DomainInfo != null);

            var domainUpdateRequest 
                = new DomainUpdateRequest()
                {
                    Name                       = name,
                    UpdatedInfoDescription     = request.DomainInfo.Description,
                    UpdatedInfoOwnerEmail      = request.DomainInfo.OwnerEmail,
                    ConfigurationEmitMetrics   = request.Options.EmitMetrics,
                    ConfigurationRetentionDays = request.Options.RetentionDays
                };

            var reply = await CallProxyAsync(domainUpdateRequest);

            reply.ThrowOnError();
        }
    }
}
