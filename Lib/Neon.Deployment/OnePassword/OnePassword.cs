//-----------------------------------------------------------------------------
// FILE:	    OnePassword.cs
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
using System.Diagnostics.Contracts;
using System.IO;

using Neon.Common;

namespace Neon.Deployment
{
    /// <summary>
    /// Wraps the 1Password CLI.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public static class OnePassword
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Unfortunately, the 1Password CLI doesn't appear to return specific
        /// exit codes detailing the for specific error yet.  We're going to 
        /// hack this by examining the response text.
        /// </summary>
        private enum OnePasswordStatus
        {
            /// <summary>
            /// The operation was successful.
            /// </summary>
            OK = 0,

            /// <summary>
            /// The session token has expired.
            /// </summary>
            SessionExpired,

            /// <summary>
            /// Unspecified error.
            /// </summary>
            Other
        }

        //---------------------------------------------------------------------
        // Implementation

        private static readonly object      syncLock = new object();
        private static string               account;
        private static string               defaultVault;
        private static string               masterPassword;
        private static string               sessionToken;

        /// <summary>
        /// Returns <c>true</c> if the class is signed-in.
        /// </summary>
        public static bool Signedin => masterPassword != null;

        /// <summary>
        /// Configures and signs into 1Password for the first time on a machine.  This
        /// must be called once before <see cref="Signin(string, string, string)"/> will
        /// work.
        /// </summary>
        /// <param name="signinAddress">Specifies the 1Password signin address.</param>
        /// <param name="account">Specifies the 1Password shorthand name to use for the account (e.g. "sally@neonforge.com").</param>
        /// <param name="secretKey">The 1Password secret key for the account.</param>
        /// <param name="masterPassword">Specified the master 1Password.</param>
        /// <param name="defaultVault">Specifies the default 1Password vault.</param>
        /// <remarks>
        /// <para>
        /// Typically, you'll first call <see cref="Configure(string, string, string, string, string)"/> once
        /// for a workstation to configure the signin address and 1Password secret key during manual
        /// configuration.  The account shorthand name used for that operation can then be used thereafter
        /// for calls to <see cref="Signin(string, string, string)"/> which don't require the additional 
        /// information.
        /// </para>
        /// <para>
        /// This two-stage process enhances security because both the master password and secret
        /// key are required to authenticate and the only time the secret key will need to be
        /// presented for the full login which will typically done manually once.  1Password
        /// securely stores the secret key on the workstation and it will never need to be present
        /// as plaintext again on the machine.
        /// </para>
        /// </remarks>
        public static void Configure(string signinAddress, string account, string secretKey, string masterPassword, string defaultVault)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signinAddress), nameof(signinAddress));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(account), nameof(account));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secretKey), nameof(secretKey));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(masterPassword), nameof(masterPassword));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(defaultVault), nameof(defaultVault));

            lock (syncLock)
            {
                // 1Password doesn't allow reconfiguring without being signed-out first.

                Signout();

                // Sign back in.

                OnePassword.account        = account;
                OnePassword.defaultVault   = defaultVault;
                OnePassword.masterPassword = masterPassword;

                var input = new StringReader(masterPassword);

                var response = NeonHelper.ExecuteCapture("op",
                    new string[]
                    {
                        "--cache",
                        "signin",
                        "--raw",
                        signinAddress,
                        account,
                        secretKey,
                        "--shorthand", account
                    },
                    input: input);

                if (response.ExitCode != 0)
                {
                    Signout();
                    throw new OnePasswordException(response.AllText);
                }

                SetSessionToken(response.OutputText.Trim());
            }
        }

        /// <summary>
        /// Signs into 1Password using just the account, master password, and default vault.  You'll
        /// typically call this rather than <see cref="Configure(string, string, string, string, string)"/>
        /// which also requires the signin address as well as the secret key.
        /// </summary>
        /// <param name="account">The account's shorthand name (e.g. (e.g. "sally@neonforge.com").</param>
        /// <param name="masterPassword">The master password.</param>
        /// <param name="defaultVault">The default vault.</param>
        /// <remarks>
        /// <para>
        /// Typically, you'll first call <see cref="Configure(string, string, string, string, string)"/> once
        /// for a workstation to configure the signin address and 1Password secret key during manual
        /// configuration.  The account shorthand name used for that operation can then be used thereafter
        /// for calls to this method which don't require the additional information.
        /// </para>
        /// <para>
        /// This two-stage process enhances security because both the master password and secret
        /// key are required to authenticate and the only time the secret key will need to be
        /// presented for the full login which will typically done manually once.  1Password
        /// securely stores the secret key on the workstation and it will never need to be present
        /// as plaintext again on the machine.
        /// </para>
        /// </remarks>
        public static void Signin(string account, string masterPassword, string defaultVault)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(account), nameof(account));;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(masterPassword), nameof(masterPassword));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(defaultVault), nameof(defaultVault));

            lock (syncLock)
            {
                OnePassword.account        = account;
                OnePassword.defaultVault   = defaultVault;
                OnePassword.masterPassword = masterPassword;

                var input = new StringReader(masterPassword);
File.AppendAllText(@"C:\Temp\log.txt", $"*** SignIn-0: MASTER-PASSWORD: {masterPassword}" + Environment.NewLine);

                var response = NeonHelper.ExecuteCapture("op",
                    new string[]
                    {
                        "--cache",
                        "signin",
                        "--raw",
                        account
                    },
                    input: input);

File.AppendAllText(@"C:\Temp\log.txt", $"*** SignIn-1: EXITCODE: {response.ExitCode}" + Environment.NewLine);
                if (response.ExitCode != 0)
                {
                    Signout();
                    throw new OnePasswordException(response.AllText);
                }

File.AppendAllText(@"C:\Temp\log.txt", $"*** SignIn-2: TOKEN: {response.OutputText.Trim()}" + Environment.NewLine);
                SetSessionToken(response.OutputText.Trim());
File.AppendAllText(@"C:\Temp\log.txt", $"*** SignIn-3:" + Environment.NewLine);
            }
        }

        /// <summary>
        /// Signs out.
        /// </summary>
        public static void Signout()
        {
            lock (syncLock)
            {
                NeonHelper.ExecuteCapture("op", new string[] { "signout" });

                OnePassword.account        = null;
                OnePassword.defaultVault   = null;
                OnePassword.masterPassword = null;
                OnePassword.sessionToken   = null;
            }
        }

        /// <summary>
        /// Returns a named password from the current user's standard 1Password 
        /// vault like [user-sally] by default or a custom named vault.
        /// </summary>
        /// <param name="name">The password name with optional property.</param>
        /// <param name="vault">Optionally specifies a specific vault.</param>
        /// <returns>The requested password (from the password's [password] field).</returns>
        /// <exception cref="OnePasswordException">Thrown for 1Password related problems.</exception>
        /// <remarks>
        /// <para>
        /// The <paramref name="name"/> parameter may optionally specify the desired
        /// 1Password property to override the default <b>"password"</b> for this
        /// method.  Properties are specified like:
        /// </para>
        /// <example>
        /// SECRETNAME[PROPERTY]
        /// </example>
        /// </remarks>
        public static string GetSecretPassword(string name, string vault = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            var parsedName = ProfileServer.ParseSecretName(name);
            var property   = parsedName.Property ?? "password";

            name = parsedName.Name;

            var retrying = false;

            lock (syncLock)
            {
                EnsureSignedIn();

                vault = GetVault(vault);

retry:          var response = NeonHelper.ExecuteCapture("op",
                    new string[]
                    {
                        "--cache",
                        "--session", sessionToken,
                        "get", "item", name,
                        "--vault", vault,
                        "--fields", property
                    });

                switch (GetStatus(response))
                {
                    case OnePasswordStatus.OK:

                        var value = response.OutputText.Trim();

                        if (value == string.Empty)
                        {
                            throw new OnePasswordException($"Property [{property}] returned an empty string.  Does it exist?.");
                        }

                        return value;

                    case OnePasswordStatus.SessionExpired:

                        if (retrying)
                        {
                            throw new OnePasswordException(response.AllText);
                        }

                        // Obtain a fresh session token and retry the operation.

                        Signin(account, masterPassword, defaultVault);

                        retrying = true;
                        goto retry;

                    default:

                        throw new OnePasswordException(response.AllText);
                }
            }
        }

        /// <summary>
        /// Returns a named value from the current user's standard 1Password 
        /// vault like [user-sally] by default or a custom named vault.
        /// </summary>
        /// <param name="name">The password name with optional property.</param>
        /// <param name="vault">Optionally specifies a specific vault.</param>
        /// <returns>The requested value (from the password's [value] field).</returns>
        /// <exception cref="OnePasswordException">Thrown for 1Password related problems.</exception>
        /// <remarks>
        /// <para>
        /// The <paramref name="name"/> parameter may optionally specify the desired
        /// 1Password property to override the default <b>"value"</b> for this
        /// method.  Properties are specified like:
        /// </para>
        /// <example>
        /// SECRETNAME[PROPERTY]
        /// </example>
        /// </remarks>
        public static string GetSecretValue(string name, string vault = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

File.AppendAllText(@"C:\Temp\log.txt", "*** GetSecretValue-0:" + Environment.NewLine);  // <-- $debug(jefflill): DELETE THESE!
            var parsedName = ProfileServer.ParseSecretName(name);
            var property   = parsedName.Property ?? "value";

            name = parsedName.Name;

            var retrying = false;

            lock (syncLock)
            {
                EnsureSignedIn();

                vault = GetVault(vault);
File.AppendAllText(@"C:\Temp\log.txt", $"*** GetSecretValue-2: TOKEN: {sessionToken}" + Environment.NewLine);

retry:          var response = NeonHelper.ExecuteCapture("op",
                    new string[]
                    {
                        "--cache",
                        "--session", sessionToken,
                        "get", "item", name,
                        "--vault", vault,
                        "--fields", property
                    });
File.AppendAllText(@"C:\Temp\log.txt", "*** GetSecretValue-3: RESPONSE:" + Environment.NewLine);
File.AppendAllText(@"C:\Temp\log.txt", $"EXITCODE: {response.ExitCode}" + Environment.NewLine);
File.AppendAllText(@"C:\Temp\log.txt", $"TEXT: {response.AllText}" + Environment.NewLine);

                switch (GetStatus(response))
                {
                    case OnePasswordStatus.OK:

                        var value = response.OutputText.Trim();

                        if (value == string.Empty)
                        {
                            throw new OnePasswordException($"Property [{property}] returned an empty string.  Does it exist?.");
                        }

                        return value;

                    case OnePasswordStatus.SessionExpired:

File.AppendAllText(@"C:\Temp\log.txt", "*** GetSecretValue-4:" + Environment.NewLine);
                        if (retrying)
                        {
                            throw new OnePasswordException(response.AllText);
                        }
File.AppendAllText(@"C:\Temp\log.txt", "*** GetSecretValue-5:" + Environment.NewLine);

                        // Obtain a fresh session token and retry the operation.

                        Signin(account, masterPassword, defaultVault);
File.AppendAllText(@"C:\Temp\log.txt", "*** GetSecretValue-6:" + Environment.NewLine);

                        retrying = true;
                        goto retry;

                    default:

                        throw new OnePasswordException(response.AllText);
                }
            }
        }

        /// <summary>
        /// Updates the session token.
        /// </summary>
        /// <param name="sessionToken">The new session token or <c>null</c>.</param>
        private static void SetSessionToken(string sessionToken)
        {
            OnePassword.sessionToken = sessionToken;
        }

        /// <summary>
        /// Ensures that we're signed into 1Password.
        /// </summary>
        /// <exception cref="OnePasswordException">Thrown if we're not signed in.</exception>
        private static void EnsureSignedIn()
        {
            if (!Signedin)
            {
                throw new OnePasswordException("You are not signed into 1Password.");
            }
        }

        /// <summary>
        /// Returns the target vault name.
        /// </summary>
        /// <param name="vault">Optionally specifies a specific vault.</param>
        /// <returns>The target vault name.</returns>
        /// <exception cref="OnePasswordException">Thrown for 1Password related problems.</exception>
        private static string GetVault(string vault = null)
        {
            if (!string.IsNullOrWhiteSpace(vault))
            {
                return vault;
            }

            var user = Environment.GetEnvironmentVariable("NC_USER");

            if (string.IsNullOrEmpty(user))
            {
                throw new OnePasswordException("The [NC_USER] environment variable is not defined.  You may need to re-run [$/buildenv.cmd] as administrator.");
            }

            return $"user-{user}";
        }

        /// <summary>
        /// Returns a <see cref="OnePasswordStatus"/> corresponding to a 1Password CLI response.
        /// </summary>
        /// <param name="response">The 1Password CLI response.</param>
        /// <returns>The status code.</returns>
        private static OnePasswordStatus GetStatus(ExecuteResponse response)
        {
            Covenant.Requires<ArgumentNullException>(response != null, nameof(response));
            
            // $hack(jefflill):
            //
            // The 1Password CLI doesn't return useful exit codes at this time,
            // so we're going to try to figure out what we need from the response
            // text returned by the CLI.

            if (response.ExitCode == 0)
            {
                return OnePasswordStatus.OK;
            }
            else
            {
                if (response.AllText.Contains("session expired") || response.AllText.Contains("you are not currently signed in"))
                {
                    return OnePasswordStatus.SessionExpired;
                }
                else
                {
                    return OnePasswordStatus.Other;
                }
            }
        }
    }
}
