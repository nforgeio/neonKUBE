//-----------------------------------------------------------------------------
// FILE:	    TemporalClient.Domain.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

using Neon.Common;
using Neon.Tasks;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    public partial class TemporalClient
    {
        //---------------------------------------------------------------------
        // Temporal namespace related operations.

        /// <summary>
        /// Registers a Temporal namespace using the <see cref="InternalRegisterDomainRequest"/> information passed.
        /// </summary>
        /// <param name="request">The namespace properties.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="DomainAlreadyExistsException">Thrown if the namespace already exists.</exception>
        /// <exception cref="BadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="InternalServiceException">Thrown for internal Temporal cluster problems.</exception>
        /// <exception cref="ServiceBusyException">Thrown when Temporal is too busy.</exception>
        private async Task RegisterDomainAsync(InternalRegisterDomainRequest request)
        {
            EnsureNotDisposed();

            var domainRegisterRequest =
                new DomainRegisterRequest()
                {
                    Name          = request.Name,
                    Description   = request.Description,
                    OwnerEmail    = request.OwnerEmail,
                    RetentionDays = request.RetentionDays,
                    SecurityToken = request.SecurityToken
                };

            var reply = await CallProxyAsync(domainRegisterRequest);

            reply.ThrowOnError();
        }

        /// <summary>
        /// Registers a Temporal namespace using the specified parameters.
        /// </summary>
        /// <param name="name">The namespace name.</param>
        /// <param name="description">Optionally specifies a description.</param>
        /// <param name="ownerEmail">Optionally specifies the owner's email address.</param>
        /// <param name="retentionDays">
        /// Optionally specifies the number of days to retain the history for workflows 
        /// completed in this namespace.  This defaults to <b>7 days</b> and may be as long
        /// as <b>30 days</b>.
        /// </param>
        /// <param name="ignoreDuplicates">
        /// Optionally ignore duplicate namespace registrations.  This defaults
        /// to <c>false</c>.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="DomainAlreadyExistsException">Thrown if the namespace already exists.</exception>
        /// <exception cref="BadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="InternalServiceException">Thrown for internal Temporal cluster problems.</exception>
        /// <exception cref="ServiceBusyException">Thrown when Temporal is too busy.</exception>
        public async Task RegisterDomainAsync(string name, string description = null, string ownerEmail = null, int retentionDays = 7, bool ignoreDuplicates = false)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentException>(retentionDays > 0, nameof(retentionDays));
            Covenant.Requires<ArgumentException>(retentionDays <= 30, nameof(retentionDays));
            EnsureNotDisposed();

            try
            {
                await RegisterDomainAsync(
                    new InternalRegisterDomainRequest()
                    {
                        Name          = name,
                        Description   = description,
                        OwnerEmail    = ownerEmail,
                        RetentionDays = retentionDays,
                        SecurityToken = Settings.SecurityToken
                    });
            }
            catch (DomainAlreadyExistsException)
            {
                if (!ignoreDuplicates)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Describes the named Temporal namespace.
        /// </summary>
        /// <param name="name">The namespace name.</param>
        /// <returns>The <see cref="DomainDescription"/>.</returns>
        /// <exception cref="EntityNotExistsException">Thrown if the named namespace does not exist.</exception>
        /// <exception cref="BadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="InternalServiceException">Thrown for internal Temporal cluster problems.</exception>
        /// <exception cref="ServiceBusyException">Thrown when Temporal is too busy.</exception>
        public async Task<DomainDescription> DescribeDomainAsync(string name)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            EnsureNotDisposed();

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

                Configuration = new DomainConfiguration()
                {
                    EmitMetrics   = reply.ConfigurationEmitMetrics,
                    RetentionDays = reply.ConfigurationRetentionDays
                },
            };
        }

        /// <summary>
        /// Describes a Temporal namespace by UUID.
        /// </summary>
        /// <param name="uuid">The namespace ID.</param>
        /// <returns>The <see cref="DomainDescription"/>.</returns>
        public async Task<DomainDescription> DescribeDomainByIdAsync(string uuid)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uuid), nameof(uuid));
            EnsureNotDisposed();

            var domainDescribeRequest =
                new DomainDescribeRequest()
                {
                    Uuid = uuid,
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

                Configuration = new DomainConfiguration()
                {
                    EmitMetrics   = reply.ConfigurationEmitMetrics,
                    RetentionDays = reply.ConfigurationRetentionDays
                },
            };
        }

        /// <summary>
        /// Updates the named Temporal namespace.
        /// </summary>
        /// <param name="name">Identifies the target namespace.</param>
        /// <param name="request">The updated namespace information.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task UpdateDomainAsync(string name, UpdateDomainRequest request)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));
            Covenant.Requires<ArgumentNullException>(request.Options != null, nameof(request));
            Covenant.Requires<ArgumentNullException>(request.DomainInfo != null, nameof(request));
            EnsureNotDisposed();

            var domainUpdateRequest 
                = new DomainUpdateRequest()
                {
                    Name                       = name,
                    UpdatedInfoDescription     = request.DomainInfo.Description,
                    UpdatedInfoOwnerEmail      = request.DomainInfo.OwnerEmail,
                    ConfigurationEmitMetrics   = request.Options.EmitMetrics,
                    ConfigurationRetentionDays = request.Options.RetentionDays,
                    SecurityToken              = Settings.SecurityToken
                };

            var reply = await CallProxyAsync(domainUpdateRequest);

            reply.ThrowOnError();
        }

        /// <summary>
        /// Lists the Temporal domains.
        /// </summary>
        /// <param name="pageSize">
        /// The maximum number of domains to be returned.  This must be
        /// greater than or equal to one.
        /// </param>
        /// <param name="nextPageToken">
        /// Optionally specifies an opaque token that can be used to retrieve subsequent
        /// pages of domains.
        /// </param>
        /// <returns>A <see cref="DomainListPage"/> with the domains.</returns>
        /// <remarks>
        /// <para>
        /// This method can be used to retrieve one or more pages of namespace
        /// results.  You'll pass <paramref name="pageSize"/> as the maximum number
        /// of domains to be returned per page.  The <see cref="DomainListPage"/>
        /// returned will list the domains and if there are more domains waiting
        /// to be returned, will return token that can be used in a subsequent
        /// call to retrieve the next page of results.
        /// </para>
        /// <note>
        /// <see cref="DomainListPage.NextPageToken"/> will be set to <c>null</c>
        /// when there are no more result pages remaining.
        /// </note>
        /// </remarks>
        public async Task<DomainListPage> ListDomainsAsync(int pageSize, byte[] nextPageToken = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentException>(pageSize >= 1, nameof(pageSize));
            EnsureNotDisposed();

            var reply = (DomainListReply)await CallProxyAsync(
                new DomainListRequest()
                {
                     PageSize      = pageSize,
                     NextPageToken = nextPageToken
                });

            reply.ThrowOnError();

            var domains = new List<DomainDescription>(reply.Domains.Count);

            foreach (var domain in reply.Domains)
            {
                domains.Add(domain.ToPublic());
            }

            nextPageToken = reply.NextPageToken;

            if (nextPageToken != null && nextPageToken.Length == 0)
            {
                nextPageToken = null;
            }

            return new DomainListPage()
            { 
                Domains       = domains,
                NextPageToken = nextPageToken
            };
        }
    }
}
