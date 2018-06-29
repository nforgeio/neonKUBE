//-----------------------------------------------------------------------------
// FILE:	    CertificateManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Hive
{
    /// <summary>
    /// Handles TLS certificate related operations for a <see cref="ClusterProxy"/>.
    /// </summary>
    public sealed class CertificateManager
    {
        private const string vaultCertPrefix = "neon-secret/cert";

        private ClusterProxy cluster;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="cluster">The parent <see cref="ClusterProxy"/>.</param>
        internal CertificateManager(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            this.cluster = cluster;
        }

        /// <summary>
        /// Removes a cluster certificate if it exists.
        /// </summary>
        /// <param name="name">The certificate name.</param>
        public void Remove(string name)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(name));

            cluster.Vault.Client.DeleteAsync(HiveHelper.GetVaultCertificateKey(name)).Wait();
            cluster.SignalLoadBalancerUpdate();
        }

        /// <summary>
        /// Retrieves a cluster certificate.
        /// </summary>
        /// <param name="name">The certificate name.</param>
        /// <returns>The certificate if present or <c>null</c> if it doesn't exist.</returns>
        public TlsCertificate Get(string name)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(name));

            return cluster.Vault.Client.ReadJsonOrDefaultAsync<TlsCertificate>(HiveHelper.GetVaultCertificateKey(name)).Result;
        }

        /// <summary>
        /// Lists the names of the cluster certificates.
        /// </summary>
        /// <returns>The certificate names.</returns>
        public IEnumerable<string> List()
        {
            return cluster.Vault.Client.ListAsync(vaultCertPrefix).Result;
        }

        /// <summary>
        /// Adds or updates a cluster certificate.
        /// </summary>
        /// <param name="name">The certificate name.</param>
        /// <param name="certificate">The certificate.</param>
        /// <exception cref="ArgumentException">Thrown if the certificate is not valid.</exception>
        /// <remarks>
        /// <note>
        /// The <paramref name="certificate"/> must be fully parsed (it's
        /// <see cref="TlsCertificate.Parse()"/> method must have been called at
        /// some point to load the <see cref="TlsCertificate.Hosts"/>, 
        /// <see cref="TlsCertificate.ValidFrom"/> and <see cref="TlsCertificate.ValidUntil"/> 
        /// properties).
        /// </note>
        /// </remarks>
        public void Set(string name, TlsCertificate certificate)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(name));
            Covenant.Requires<ArgumentNullException>(certificate != null);

            cluster.Vault.Client.WriteJsonAsync(HiveHelper.GetVaultCertificateKey(name), certificate).Wait();
            cluster.SignalLoadBalancerUpdate();
        }

        /// <summary>
        /// Loads the cluster certificates from Vault.
        /// </summary>
        /// <returns>A dictionary of cluster certificates keyed by name.</returns>
        public Dictionary<string, TlsCertificate> GetAll()
        {
            var certificates = new Dictionary<string, TlsCertificate>();

            foreach (var certName in cluster.Vault.Client.ListAsync(vaultCertPrefix).Result)
            {
                var certJson    = cluster.Vault.Client.ReadDynamicAsync($"{vaultCertPrefix}/{certName}").Result.ToString();
                var certificate = NeonHelper.JsonDeserialize<TlsCertificate>(certJson);

                certificates.Add(certName, certificate);
            }

            return certificates;
        }
    }
}
