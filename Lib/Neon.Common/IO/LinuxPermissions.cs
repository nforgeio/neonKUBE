//-----------------------------------------------------------------------------
// FILE:	    LinuxPermissions.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.IO
{
    /// <summary>
    /// Manipulates Linux style file permissions.
    /// </summary>
    public struct LinuxPermissions
    {
        //-----------------------------------------------------------
        // Static members

        /// <summary>
        /// Verifies that the character passed is a valid permissions digit.
        /// </summary>
        /// <param name="digit">The permissions digit.</param>
        /// <returns><c>true</c> if the digit is valid.</returns>
        [Pure]
        public static bool IsValidDigit(char digit)
        {
            return '0' <= digit && digit <= '7';
        }

        /// <summary>
        /// Attempts to parse permissions from an octal string.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="permissions">Returns as the parsed permissions.</param>
        /// <returns><c>true</c> if valid permissions were parsed.</returns>
        public static bool TryParse(string input, out LinuxPermissions permissions)
        {
            permissions = new LinuxPermissions();

            if (string.IsNullOrEmpty(input) ||
                input.Length != 3 ||
                !IsValidDigit(input[0]) ||
                !IsValidDigit(input[1]) ||
                !IsValidDigit(input[2]))
            {
                return false;
            }

            var owner    = input[0] - '0';
            var group    = input[1] - '0';
            var all      = input[2] - '0';

            permissions.OwnerRead    = (owner & 4) != 0;
            permissions.OwnerWrite   = (owner & 2) != 0;
            permissions.OwnerExecute = (owner & 1) != 0;

            permissions.GroupRead    = (group & 4) != 0;
            permissions.GroupWrite   = (group & 2) != 0;
            permissions.GroupExecute = (group & 1) != 0;

            permissions.AllRead      = (all & 4) != 0;
            permissions.AllWrite     = (all & 2) != 0;
            permissions.AllExecute   = (all & 1) != 0;

            return true;
        }

        /// <summary>
        /// Sets the Linux file permissions.
        /// </summary>
        /// <param name="path">Path to the target file or directory.</param>
        /// <param name="mode">Linux file permissions.</param>
        /// <param name="recursive">Optionally apply the permissions recursively.</param>
        public static void Set(string path, string mode, bool recursive = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(mode));

            // $todo(jeff.lill):
            //
            // We're going to hack this by running [chmod MODE PATH].  Eventually,
            // we could convert this to using a low-level package but I didn't
            // want to spend time trying to figure that out right now.
            //
            //      https://www.nuget.org/packages/Mono.Posix.NETStandard/1.0.0

            if (!NeonHelper.IsLinux)
            {
                throw new NotSupportedException("This method requires Linux.");
            }

            object[] args;

            if (recursive)
            {
                args = new object[] { "-R", mode, path };
            }
            else
            {
                args = new object[] { mode, path };
            }

            var response = NeonHelper.ExecuteCaptureAsync("chmod", new object[] { mode, path }).Result;

            if (response.ExitCode != 0)
            {
                throw new IOException(response.ErrorText);
            }
        }

        //-----------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructs permissions from an octal string.
        /// </summary>
        /// <param name="input">The permissions string encoded in their octal form.</param>
        public LinuxPermissions(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (!TryParse(input, out var permissions))
            {
                throw new ArgumentException($"Invalid Linux permissions [{input}].");
            }

            this.OwnerRead    = permissions.OwnerRead;
            this.OwnerWrite   = permissions.OwnerWrite;
            this.OwnerExecute = permissions.OwnerExecute;
            this.GroupRead    = permissions.GroupRead;
            this.GroupWrite   = permissions.GroupWrite;
            this.GroupExecute = permissions.GroupExecute;
            this.AllRead      = permissions.AllRead;
            this.AllWrite     = permissions.AllWrite;
            this.AllExecute   = permissions.AllExecute;
        }

        /// <summary>
        /// The owner can read the file.
        /// </summary>
        public bool OwnerRead { get; set; }

        /// <summary>
        /// The owner can modify the file.
        /// </summary>
        public bool OwnerWrite { get; set; }

        /// <summary>
        /// The owner can execute the file.
        /// </summary>
        public bool OwnerExecute { get; set; }

        /// <summary>
        /// The group can read the file.
        /// </summary>
        public bool GroupRead { get; set; }

        /// <summary>
        /// The group can modify the file.
        /// </summary>
        public bool GroupWrite { get; set; }

        /// <summary>
        /// The group can execute the file.
        /// </summary>
        public bool GroupExecute { get; set; }

        /// <summary>
        /// Everyone can read the file.
        /// </summary>
        public bool AllRead { get; set; }

        /// <summary>
        /// Everyone can modify the file.
        /// </summary>
        public bool AllWrite { get; set; }

        /// <summary>
        /// Everyone can execute the file.
        /// </summary>
        public bool AllExecute { get; set; }

        /// <summary>
        /// Converts the permissions passed into the equivalent octal character.
        /// </summary>
        /// <param name="read">Read flag.</param>
        /// <param name="write">Write flag.</param>
        /// <param name="execute">Execute flag.</param>
        /// <returns>The octal character.</returns>
        private char ToOctal(bool read, bool write, bool execute)
        {
            var value = 0;

            if (read)
            {
                value += 4;
            }

            if (write)
            {
                value += 2;
            }

            if (execute)
            {
                value += 1;
            }

            return (char)('0' + value);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return new string(
                new char[]
                {
                    ToOctal(OwnerRead, OwnerWrite, OwnerExecute),
                    ToOctal(GroupRead, GroupWrite, GroupExecute),
                    ToOctal(AllRead, AllWrite, AllExecute)
                });
        }
    }
}
