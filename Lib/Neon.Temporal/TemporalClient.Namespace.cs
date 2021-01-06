//-----------------------------------------------------------------------------
// FILE:	    TemporalClient.Namespace.cs
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
        /// Registers a Temporal namespace using the <see cref="InternalRegisterNamespaceRequest"/> information passed.
        /// </summary>
        /// <param name="request">The namespace properties.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="NamespaceAlreadyExistsException">Thrown if the namespace already exists.</exception>
        /// <exception cref="BadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="InternalServiceException">Thrown for internal Temporal cluster problems.</exception>
        /// <exception cref="ServiceBusyException">Thrown when Temporal is too busy.</exception>
        private async Task RegisterNamespaceAsync(InternalRegisterNamespaceRequest request)
        {
            EnsureNotDisposed();

            var namespaceRegisterRequest =
                new NamespaceRegisterRequest()
                {
                    Name          = request.Name,
                    Description   = request.Description,
                    OwnerEmail    = request.OwnerEmail,
                    RetentionDays = request.RetentionDays,
                    SecurityToken = request.SecurityToken
                };

            var reply = await CallProxyAsync(namespaceRegisterRequest);

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
        /// <exception cref="NamespaceAlreadyExistsException">Thrown if the namespace already exists.</exception>
        /// <exception cref="BadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="InternalServiceException">Thrown for internal Temporal cluster problems.</exception>
        /// <exception cref="ServiceBusyException">Thrown when Temporal is too busy.</exception>
        public async Task RegisterNamespaceAsync(string name, string description = null, string ownerEmail = null, int retentionDays = 7, bool ignoreDuplicates = false)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentException>(retentionDays > 0, nameof(retentionDays));
            Covenant.Requires<ArgumentException>(retentionDays <= 30, nameof(retentionDays));
            EnsureNotDisposed();

            try
            {
                await RegisterNamespaceAsync(
                    new InternalRegisterNamespaceRequest()
                    {
                        Name          = name,
                        Description   = description,
                        OwnerEmail    = ownerEmail,
                        RetentionDays = retentionDays,
                        SecurityToken = Settings.SecurityToken
                    });
            }
            catch (NamespaceAlreadyExistsException)
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
        /// <returns>The <see cref="NamespaceDescription"/>.</returns>
        /// <exception cref="EntityNotExistsException">Thrown if the named namespace does not exist.</exception>
        /// <exception cref="BadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="InternalServiceException">Thrown for internal Temporal cluster problems.</exception>
        /// <exception cref="ServiceBusyException">Thrown when Temporal is too busy.</exception>
        public async Task<NamespaceDescription> DescribeNamespaceAsync(string name)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            EnsureNotDisposed();

            var namespaceDescribeRequest =
                new NamespaceDescribeRequest()
                {
                    Name = name,
                };

            var reply = (NamespaceDescribeReply)await CallProxyAsync(namespaceDescribeRequest);

            reply.ThrowOnError();

            return new NamespaceDescription()
            {
                NamespaceInfo     = reply.NamespaceInfo,
                Config            = reply.NamespaceConfig,
                IsGlobalNamespace = reply.IsGlobalNamespace,
                FailoverVersion   = reply.FailoverVersion,
                ReplicationConfig = reply.NamespaceReplicationConfig
            };
        }

        /// <summary>
        /// Updates the named Temporal namespace.
        /// </summary>
        /// <param name="name">Identifies the target namespace.</param>
        /// <param name="request">The updated namespace information.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task UpdateNamespaceAsync(string name, UpdateNamespaceRequest request)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));
            Covenant.Requires<ArgumentNullException>(request.Config != null, nameof(request));
            Covenant.Requires<ArgumentNullException>(request.UpdateInfo != null, nameof(request));
            EnsureNotDisposed();

            var namespaceUpdateRequest =
                new NamespaceUpdateRequest()
                {
                    Name                       = name,
                    UpdateNamespaceInfo        = request.UpdateInfo,
                    NamespaceReplicationConfig = request.ReplicationConfig,
                    NamespaceConfig            = request.Config,
                    SecurityToken              = Settings.SecurityToken,
                    DeleteBadBinary            = request.DeleteBadBinary
                };

            var reply = await CallProxyAsync(namespaceUpdateRequest);

            reply.ThrowOnError();
        }

        /// <summary>
        /// Lists the Temporal namespaces.
        /// </summary>
        /// <param name="pageSize">
        /// The maximum number of namespaces to be returned.  This must be
        /// greater than or equal to one.
        /// </param>
        /// <param name="nextPageToken">
        /// Optionally specifies an opaque token that can be used to retrieve subsequent
        /// pages of namespaces.
        /// </param>
        /// <returns>A <see cref="NamespaceListPage"/> with the namespaces.</returns>
        /// <remarks>
        /// <para>
        /// This method can be used to retrieve one or more pages of namespace
        /// results.  You'll pass <paramref name="pageSize"/> as the maximum number
        /// of namespaces to be returned per page.  The <see cref="NamespaceListPage"/>
        /// returned will list the namespaces and if there are more namespaces waiting
        /// to be returned, will return token that can be used in a subsequent
        /// call to retrieve the next page of results.
        /// </para>
        /// <note>
        /// <see cref="NamespaceListPage.NextPageToken"/> will be set to <c>null</c>
        /// when there are no more result pages remaining.
        /// </note>
        /// </remarks>
        public async Task<NamespaceListPage> ListNamespacesAsync(int pageSize, byte[] nextPageToken = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentException>(pageSize >= 1, nameof(pageSize));
            EnsureNotDisposed();

            var reply = (NamespaceListReply)await CallProxyAsync(
                new NamespaceListRequest()
                {
                     PageSize      = pageSize,
                     NextPageToken = nextPageToken
                });

            reply.ThrowOnError();

            nextPageToken = reply.NextPageToken;

            if (nextPageToken != null && nextPageToken.Length == 0)
            {
                nextPageToken = null;
            }

            return new NamespaceListPage()
            { 
                Namespaces    = reply.Namespaces,
                NextPageToken = nextPageToken
            };
        }
    }
}
