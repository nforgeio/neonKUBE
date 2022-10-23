//-----------------------------------------------------------------------------
// FILE:	    ThisAssembly.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

/// <summary>
/// This is a drop-in replacement for the <b>GitInfo</b> <see cref="Internal.ThisAssembly"/> class.
/// </summary>
/// <remarks>
/// <para>
/// This is a workaround for duplicate symbol compiler errors we often see for more complex solutions
/// that target multiple build configurations and target frameworks.  The root problem is that the GitInfo
/// nuget package generates a C# file under [/obj/$Configuration)] or [/obj/$Configuration)/$(TargetFramework)]
/// and when there are multiple configurations and/or target frameworks, we can end up with multiple versions
/// of the generated file and since MSBUILD recursively compiles all C# files within the project folder, we
/// end up with compiler errors.
/// </para>
/// <para>
/// This library works by using the <b>GitInfo</b> nuget but this project only has one build configuration 
/// (Release) and only one target framework (netstandard2.0), so we we'll never see duplicate source files.
/// </para>
/// </remarks>
public static class ThisAssembly
{
    /// <summary>
    /// Returns information about the current git repo for the solution.
    /// </summary>
    public static class Git
    {
        /// <summary>
        /// Returns <c>true</c> when the git repo has uncommited changes.
        /// </summary>
        public const bool IsDirty = Internal.ThisAssembly.Git.IsDirty;

        /// <summary>
        /// Returns <b>"true</b> when the git repo has uncommited changes, <b>"false"</b>
        /// otherwise.
        /// </summary>
        public const string IsDirtyString = Internal.ThisAssembly.Git.IsDirtyString;

        /// <summary>
        /// Returns the upstream git repository URL.
        /// </summary>
        public const string RepositoryUrl = Internal.ThisAssembly.Git.RepositoryUrl;

        /// <summary>
        /// Returns the name of the current branch.
        /// </summary>
        public const string Branch = Internal.ThisAssembly.Git.Branch;

        /// <summary>
        /// Returns the current commit hash (short).
        /// </summary>
        public const string Commit = Internal.ThisAssembly.Git.Commit;

        /// <summary>
        /// Returns the current commit SHA.
        /// </summary>
        public const string Sha = Internal.ThisAssembly.Git.Sha;

        /// <summary>
        /// Returns the commit timestamp.
        /// </summary>
        public const string CommitDate = Internal.ThisAssembly.Git.CommitDate;

        /// <summary>
        /// Returns the commits on top of the base version.
        /// </summary>
        public const string Commits = Internal.ThisAssembly.Git.Commits;

        /// <summary>
        /// Returns the full tag.
        /// </summary>
        public const string Tag = Internal.ThisAssembly.Git.Tag;

        /// <summary>
        /// Returns the base tag.
        /// </summary>
        public const string BaseTag = Internal.ThisAssembly.Git.BaseTag;

        /// <summary>
        /// Provides access to the base version information used to determine the <see cref="SemVer" />.
        /// </summary>
        public static partial class BaseVersion
        {
            /// <summary>
            /// The major version.
            /// </summary>
            public const string Major = Internal.ThisAssembly.Git.BaseVersion.Major;

            /// <summary>
            /// The minor version.
            /// </summary>
            public const string Minor = Internal.ThisAssembly.Git.BaseVersion.Minor;

            /// <summary>
            /// The patch version.
            /// </summary>
            public const string Patch = Internal.ThisAssembly.Git.BaseVersion.Patch;
        }

        /// <summary>
        /// Provides access to SemVer information for the current assembly.
        /// </summary>
        public partial class SemVer
        {
            /// <summary>
            /// The major version.
            /// </summary>
            public const string Major = Internal.ThisAssembly.Git.SemVer.Major;

            /// <summary>
            /// The minor version.
            /// </summary>
            public const string Minor = Internal.ThisAssembly.Git.SemVer.Minor;

            /// <summary>
            /// The patch version.
            /// </summary>
            public const string Patch = Internal.ThisAssembly.Git.SemVer.Patch;

            /// <summary>
            /// The label.
            /// </summary>
            public const string Label = Internal.ThisAssembly.Git.SemVer.Label;

            /// <summary>
            /// The label (if any) prefixed with a dash.
            /// </summary>
            public const string DashLabel = Internal.ThisAssembly.Git.SemVer.DashLabel;

            /// <summary>
            /// The source.
            /// </summary>
            public const string Source = Internal.ThisAssembly.Git.SemVer.Source;
        }
    }
}
