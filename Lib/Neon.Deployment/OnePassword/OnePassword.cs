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
    /// <para>
    /// Provides access to a developers's 1Password secrets for testing and executing CI/CD purposes.
    /// </para>
    /// <note>
    /// This class is not entirely general purpose; it's currently oriented towards supporting CI/CD
    /// for neonFORGE maintainers who have configured their workstations by installing both the
    /// 1Password application and CLI with the client having been manually signedin for the first
    /// time and the <b>NC_USER</b> environment variable set to the 1Password shortname used to sign-in.
    /// This also assumes that account's vaults configured a a specific way.
    /// </note>
    /// </summary>
    /// <remarks>
    /// <note>
    /// This class requires that the developer has followed the developer onboarding instructions
    /// and has already configured the current workstation by installing 1Password etc.
    /// </note>
    /// <para>
    /// neonFORGE has decided to use the <a href="https://1password.com">1Password</a> password manager
    /// company wide for password management including devops related activities.  All developers will
    /// have a neonFORGE 1Password account holding necessary credentials for the individual.  All 
    /// company users will have a 1Password vault named [user-USERNAME] where USERNAME is the person's
    /// Microsoft Office user name, such as [sally] from [sally@neonforge.com].  Most, if not all of
    /// a person's credentials will be stored in this vault.
    /// </para>
    /// <para>
    /// Each developer will have a standard set of passwords with well-defined names such that they
    /// can be accessed by common CI/CD scripts, etc.
    /// </para>
    /// <para>
    /// We're currently persisting credentials available for automation as 1Password passwords.  For real
    /// secrets, we'll persist the value using the [password] field within the 1Password password.  For
    /// information that is not really a secret (and may trigger 1Password complexity warnings), we use
    /// the [value] field.
    /// </para>
    /// <para>
    /// Use <see cref="GetPassword(string, string)"/> to retrieve a password and <see cref="GetValue(string, string)"/>
    /// to retrieve a value for the current user.
    /// </para>
    /// <note>
    /// The current user is determined by the <b>NC_USER</b> environment variable.  This is
    /// set to the user's neonFORGE Microsoft Office user name when the workstation is configured
    /// by the developer.
    /// </note>
    /// <note>
    /// This class mimics the PowerShell 1Password script functions found in <b>$/Powershell/includes.ps1</b>
    /// so C# code can also access secrets.
    /// </note>
    /// <note>
    /// This class can interoperate with the 1Password PowerShell script functions.
    /// </note>
    /// </remarks>
    public static class OnePassword
    {
        /// <summary>
        /// Prompts the user for the master 1Password when the <c>NC_OP_MASTER_PASSWORD</c>
        /// environment is not set.  This can be used for tools that may be run directly by
        /// the user and will need the user to manually enter the password as well as by
        /// automated scripts where the password is already present as the environment
        /// variable.
        /// </summary>
        /// <param name="prompt">Optionally specifies a custom password prompt.</param>
        /// <returns></returns>
        public static string ReadPasswordFromConsole(string prompt = null)
        {
            prompt = prompt ?? "Please enter your master 1Password: ";

            var password = Environment.GetEnvironmentVariable("NC_OP_MASTER_PASSWORD");

            if (password != null)
            {
                return password;
            }

            Console.WriteLine();
            password = NeonHelper.ReadConsolePassword(prompt);
            Console.WriteLine();

            return password;
        }

        /// <summary>
        /// Signs into 1Password so you'll be able to access secrets. 
        /// </summary>
        /// <param name="masterPassword">
        /// Optionally specifies master password rather than using the
        /// <b>NC_OP_MASTER_PASSWORD</b> environment variable.
        /// </param>
        /// <exception cref="OnePasswordException">Thrown on errors.</exception>
        /// <remarks>
        /// <para>
        /// This will start a 30 minute session that will allow you to use the 1Password [op]
        /// CLI without requiring additional credentials.  We recommend that you call this at
        /// the  beginning of all CI/CD operations and then immediately grab all of the secrets 
        /// you'll need for that operation.  This way you won't need to worry about the
        /// 30 minute session expiring.  This must be called before <see cref="GetPassword(string, string)"/>
        /// or <see cref="GetValue(string, string)"/>.
        /// </para>
        /// <para>
        /// This script requires the following environment variables to be defined:
        /// </para>
        /// <list type="bullet">
        ///     <item><c>NC_OP_DOMAIN</c></item>
        ///     <item><c>NC_OP_MASTER_PASSWORD</c> - <i>optional</i></item>
        /// </list>
        /// <para>
        /// NC_OP_MASTER_PASSWORD is optional and you'll be prompted for this (once for 
        /// the current Powershell session) if this isn't defined.  The other variables
        /// are initialized by <b>$/buildenv.cmd</b> so re-run that as administrator if necessary.
        /// </para>
        /// </remarks>
        public static void Signin(string masterPassword = null)
        {
            const string errorHelp = " environment variable is not defined.  Please run [$/buildenv.cmd] as administrator";

            var neonOpDomain         = Environment.GetEnvironmentVariable("NC_OP_DOMAIN");
            var neonOpMasterPassword = string.Empty;

            if (!string.IsNullOrEmpty(masterPassword))
            {
                neonOpMasterPassword = masterPassword;
            }
            else
            {
                neonOpMasterPassword = Environment.GetEnvironmentVariable("NC_OP_MASTER_PASSWORD");
            }

            if (string.IsNullOrEmpty(neonOpDomain))
            {
                throw new OnePasswordException($"[NC_OP_DOMAIN]{errorHelp}");
            }

            ExecuteResponse response;

            if (string.IsNullOrEmpty(neonOpMasterPassword))
            {
                // The user will be prompted for the master password.

                response = NeonHelper.ExecuteCapture("op",
                    new string[]
                    {
                        "--cache",
                        "signin",
                        "--raw",
                        neonOpDomain
                    });
            }
            else
            {
                var input = new StringReader(neonOpMasterPassword);

                response = NeonHelper.ExecuteCapture("op",
                    new string[]
                    {
                        "--cache",
                        "signin",
                        "--raw",
                        neonOpDomain
                    },
                    input: input);
            }

            if (response.ExitCode != 0)
            {
                throw new OnePasswordException(response.ErrorText);
            }

            Environment.SetEnvironmentVariable("NC_OP_SESSION_TOKEN", response.OutputText.Trim());
        }

        /// <summary>
        /// Signs out from 1Password by removing the session environment variable.
        /// </summary>
        public static void Signout()
        {
            Environment.SetEnvironmentVariable("NC_OP_SESSION_TOKEN", null);
        }

        /// <summary>
        /// Returns <c>true</c> when we're signed into 1Password.
        /// </summary>
        public static bool SignedIn => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NC_OP_SESSION_TOKEN"));

        /// <summary>
        /// Returns a named password from the current user's standard 1Password 
        /// vault like [user-sally] by default or a custom named vault.
        /// </summary>
        /// <param name="name">The password name.</param>
        /// <param name="vault">Optionally specifies a specific vault.</param>
        /// <returns>The requested password (from the password's [password] field).</returns>
        /// <exception cref="OnePasswordException">Thrown for 1Password related problems.</exception>
        public static string GetPassword(string name, string vault = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            EnsureSignedIn();

            vault = GetVault(vault);

            var response = NeonHelper.ExecuteCapture("op",
                new string[]
                {
                    "--cache",
                    "--session", Environment.GetEnvironmentVariable("NC_OP_SESSION_TOKEN"),
                    "get", "item", name,
                    "--vault", vault,
                    "--fields", "password"
                });

            if (response.ExitCode != 0)
            {
                throw new OnePasswordException(response.ErrorText);
            }

            return response.OutputText.Trim();
        }

        /// <summary>
        /// Returns a named value from the current user's standard 1Password 
        /// vault like [user-sally] by default or a custom named vault.
        /// </summary>
        /// <param name="name">The password name.</param>
        /// <param name="vault">Optionally specifies a specific vault.</param>
        /// <returns>The requested value (from the password's [value] field).</returns>
        /// <exception cref="OnePasswordException">Thrown for 1Password related problems.</exception>
        public static string GetValue(string name, string vault = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            EnsureSignedIn();

            vault = GetVault(vault);

            var response = NeonHelper.ExecuteCapture("op",
                new string[]
                {
                    "--cache",
                    "--session", Environment.GetEnvironmentVariable("NC_OP_SESSION_TOKEN"),
                    "get", "item", name,
                    "--vault", vault,
                    "--fields", "value"
                });

            if (response.ExitCode != 0)
            {
                throw new OnePasswordException(response.ErrorText);
            }

            return response.OutputText.Trim();
        }

        /// <summary>
        /// <para>
        /// Retrieves the AWS-CLI NEON_OP_AWS_ACCESS_KEY_ID and NEON_OP_AWS_SECRET_ACCESS_KEY
        /// credentials from 1Password and sets these enviroment variables:
        /// </para>
        /// <list type="bullet">
        ///     <item><c>AWS_ACCESS_KEY_ID</c></item>
        ///     <item><c>AWS_SECRET_ACCESS_KEY</c></item>
        /// </list>
        /// </summary>
        public static void GetAwsCredentials()
        {
            EnsureSignedIn();

            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", GetPassword("NEON_OP_AWS_ACCESS_KEY_ID"));
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", GetPassword("NEON_OP_AWS_SECRET_ACCESS_KEY"));
        }

        /// <summary>
        /// <para>
        /// Removes the AWS-CLI credential environment variables if present:
        /// </para>
        /// <list type="bullet">
        ///     <item><c>AWS_ACCESS_KEY_ID</c></item>
        ///     <item><c>AWS_SECRET_ACCESS_KEY</c></item>
        /// </list>
        /// </summary>
        public static void ClearAwsCredentials()
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", null);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", null);
        }

        /// <summary>
        /// Ensures that we're signed into 1Password.
        /// </summary>
        /// <exception cref="OnePasswordException">Thrown if we're not signed in.</exception>
        private static void EnsureSignedIn()
        {
            if (Environment.GetEnvironmentVariable("NC_OP_SESSION_TOKEN") == null)
            {
                throw new OnePasswordException("You are not signed into 1Password.");
            }
        }

        /// <summary>
        /// Returns the target vault name to be accessed.
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
    }
}
