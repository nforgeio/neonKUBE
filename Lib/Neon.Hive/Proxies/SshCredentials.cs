//-----------------------------------------------------------------------------
// FILE:	    SshCredentials.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Renci.SshNet;

namespace Neon.Hive
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
        /// <param name="username">The user name.</param>
        /// <param name="password">The password.</param>
        /// <returns>The <see cref="SshCredentials"/>.</returns>
        public static SshCredentials FromUserPassword(string username, string password)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));
            Covenant.Requires<ArgumentNullException>(password != null);

            return new SshCredentials()
            {
                Username             = username,
                AuthenticationMethod = new PasswordAuthenticationMethod(username, password)
            };
        }

        /// <summary>
        /// Returns credentials based on a user name and password.
        /// </summary>
        /// <param name="username">The user name.</param>
        /// <param name="privateKey">The unencrypted PEM-encoded private key.</param>
        /// <returns>The <see cref="SshCredentials"/>.</returns>
        public static SshCredentials FromPrivateKey(string username, string privateKey)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));
            Covenant.Requires<ArgumentNullException>(privateKey != null);

            using (var privateKeyStream = new MemoryStream(Encoding.UTF8.GetBytes(privateKey)))
            {
                return new SshCredentials()
                {
                    Username             = username,
                    AuthenticationMethod = new PrivateKeyAuthenticationMethod(username, new PrivateKeyFile(privateKeyStream))
                };
            }
        }

        /// <summary>
        /// Returns an instance indicating that there are no available credentials.
        /// </summary>
        /// <returns></returns>
        public static SshCredentials None
        {
            get { return new SshCredentials(); }
        }

        //---------------------------------------------------------------------
        // Instance members

        private AuthenticationMethod authenticationMethod;

        /// <summary>
        /// Returns the user name.
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// Returns the authentication method to be used to establish an SSH.NET session.
        /// </summary>
        /// <exception cref="HiveException">Thrown if the SSH credentials are not available.</exception>
        internal AuthenticationMethod AuthenticationMethod
        {
            get
            {
                if (authenticationMethod == null)
                {
                    throw new HiveException("Hive SSH credentials are not available.");
                }

                return authenticationMethod;
            }

            set { authenticationMethod = value; }
        }
    }
}
