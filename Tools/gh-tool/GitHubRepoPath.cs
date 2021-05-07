//-----------------------------------------------------------------------------
// FILE:	    GitHubRepoPath.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace GHTool
{
    /// <summary>
    /// Abstracts GitHub repo paths like: <b>github.com/owner/repo</b>.
    /// </summary>
    public class GitHubRepoPath
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Parses a GitHub repository path.
        /// </summary>
        /// <param name="path">The path, like: <b>[SERVER]/OWNER/REPO</b></param>
        /// <returns>The parsed <see cref="GitHubRepoPath"/>.</returns>
        /// <exception cref="FormatException">Thrown when the input is invalid.</exception>
        /// <remarks>
        /// <note>
        /// <b>github.com</b> will be assumed when no server is specified.
        /// </note>
        /// </remarks>
        public static GitHubRepoPath Parse(string path)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));

            var parts    = path.Split('/');

            foreach (var part in parts)
            {
                if (part.Length == 0 || part.Contains(' '))
                {
                    throw new FormatException($"Invalid GitHub repo path: {path}");
                }
            }

            var repoPath = new GitHubRepoPath();

            switch (parts.Length)
            {
                case 2:

                    repoPath.Server = "github.com";
                    repoPath.Owner  = parts[0];
                    repoPath.Repo   = parts[1];
                    break;

                case 3:

                    repoPath.Server = parts[0];
                    repoPath.Owner  = parts[1];
                    repoPath.Repo   = parts[2];
                    break;

                default:

                    throw new FormatException($"Invalid GitHub repo path: {path}");
            }

            return repoPath;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Static constructor.
        /// </summary>
        private GitHubRepoPath()
        {
        }

        /// <summary>
        /// Returns the <b>server</b> part of the path.
        /// </summary>
        public string Server { get; private set; }

        /// <summary>
        /// Returns the <b>owner</b> part of the path.
        /// </summary>
        public string Owner { get; private set; }

        /// <summary>
        /// Returns the <b>repo</b> part of the path.
        /// </summary>
        public string Repo { get; private set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Server}/{Owner}/{Repo}";
        }
    }
}
