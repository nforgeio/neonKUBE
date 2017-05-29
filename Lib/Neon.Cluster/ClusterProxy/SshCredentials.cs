//-----------------------------------------------------------------------------
// FILE:	    SshCredentials.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Renci.SshNet;

namespace Neon.Cluster
{
    /// <summary>
    /// Provides credentials suitable for connecting to a server machine via SSH.
    /// </summary>
    public class SshCredentials
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns credentials based on a user name and password.
        /// </summary>
        /// <param name="userName">The user name.</param>
        /// <param name="password">The password.</param>
        /// <returns>The <see cref="SshCredentials"/>.</returns>
        public static SshCredentials FromUserPassword(string userName, string password)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(userName));
            Covenant.Requires<ArgumentNullException>(password != null);

            return new SshCredentials()
            {
                UserName             = userName,
                AuthenticationMethod = new PasswordAuthenticationMethod(userName, password)
            };
        }

        /// <summary>
        /// Returns credentials based on a user name and password.
        /// </summary>
        /// <param name="userName">The user name.</param>
        /// <param name="privateKey">The unencrypted PEM-encoded private key.</param>
        /// <returns>The <see cref="SshCredentials"/>.</returns>
        public static SshCredentials FromPrivateKey(string userName, string privateKey)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(userName));
            Covenant.Requires<ArgumentNullException>(privateKey != null);

            using (var privateKeyStream = new MemoryStream(Encoding.UTF8.GetBytes(privateKey)))
            {
                return new SshCredentials()
                {
                    UserName             = userName,
                    AuthenticationMethod = new PrivateKeyAuthenticationMethod(userName, new PrivateKeyFile(privateKeyStream))
                };
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Returns the user name.
        /// </summary>
        public string UserName { get; private set; }

        /// <summary>
        /// Returns the authentication method to be used to establish an SSH.NET session.
        /// </summary>
        internal AuthenticationMethod AuthenticationMethod { get; private set; }
    }
}
