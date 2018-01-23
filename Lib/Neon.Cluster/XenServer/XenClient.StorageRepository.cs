//-----------------------------------------------------------------------------
// FILE:	    XenClient.StoragerRepository.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Cluster;
using Neon.Common;

namespace Neon.Cluster.XenServer
{
    public partial class XenClient
    {
        /// <summary>
        /// Implements the <see cref="XenClient"/> virtual machine template operations.
        /// </summary>
        public class StorageRepositoryOperations
        {
            private XenClient client;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="client">The XenServer client instance.</param>
            internal StorageRepositoryOperations(XenClient client)
            {
                this.client = client;
            }

            /// <summary>
            /// Lists the XenServer storage repositories.
            /// </summary>
            /// <returns>The list of storage repositories.</returns>
            public List<XenStorageRepository> List()
            {
                var response     = client.InvokeItems("sr-list");
                var repositories = new List<XenStorageRepository>();

                foreach (var result in response.Results)
                {
                    repositories.Add(new XenStorageRepository(result));
                }

                return repositories;
            }

            /// <summary>
            /// Finds a specific storage repository by name or unique ID.
            /// </summary>
            /// <param name="name">Specifies the target name.</param>
            /// <param name="uuid">Specifies the target unique ID.</param>
            /// <returns>The named item or <c>null</c> if it doesn't exist.</returns>
            /// <remarks>
            /// <note>
            /// One of <paramref name="name"/> or <paramref name="uuid"/> must be specified.
            /// </note>
            /// </remarks>
            public XenStorageRepository Find(string name = null, string uuid = null)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(uuid));

                if (!string.IsNullOrWhiteSpace(name))
                {
                    return List().FirstOrDefault(item => item.NameLabel == name);
                }
                else if (!string.IsNullOrWhiteSpace(uuid))
                {
                    return List().FirstOrDefault(item => item.Uuid == uuid);
                }
                else
                {
                    throw new ArgumentException($"One of [{nameof(name)}] or [{nameof(uuid)}] must be specified.");
                }
            }

            /// <summary>
            /// Returns the local XenServer storage repository.
            /// </summary>
            /// <returns>The local storage repository.</returns>
            /// <exception cref="XenException">Thrown if the operation failed.</exception>
            public XenStorageRepository GetLocal()
            {
                // We're going to first look for a repo named "Local storage"
                // and if that doesn't exist, we'll return the first SR with
                // type==lvm.

                var repos = List();
                var sr    = repos.FirstOrDefault(item => item.NameLabel == "Local storage");

                if (sr != null)
                {
                    return sr;
                }

                sr = repos.FirstOrDefault(item => item.Type == "lvm");

                if (sr != null)
                {
                    return sr;
                }
                else
                {
                    throw new XenException("Cannot find the [Local storage] repository.");
                }
            }
        }
    }
}
