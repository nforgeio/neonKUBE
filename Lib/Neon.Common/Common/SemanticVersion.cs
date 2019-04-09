//-----------------------------------------------------------------------------
// FILE:        SemanticVersion.cs
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

namespace Neon.Common
{
    /// <summary>
    /// Implements a semantic version as defined by the <a href="https://semver.org/spec/v2.0.0.html">Semantic Versioning 2.0.0</a>
    /// specification.  This is similar to the base <see cref="Version"/> class but includes support for pre-release identifiers
    /// as well as build information.
    /// </summary>
    public class SemanticVersion : IComparable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Attempts to parse a semantic version string.
        /// </summary>
        /// <param name="versionText">The version text.</param>
        /// <param name="version">Returns as the parsed version on success.</param>
        /// <returns><c>true</c> if the version was parsed successfully.</returns>
        public static bool TryParse(string versionText, out SemanticVersion version)
        {
            version = null;

            if (string.IsNullOrEmpty(versionText))
            {
                return false;
            }

            version = new SemanticVersion();

            // Extract the prerelease and build metadate if present.

            var metadataPos = versionText.IndexOf('+');

            if (metadataPos != -1)
            {
                version.Build = versionText.Substring(metadataPos + 1).Trim();

                if (version.Build.Length == 0)
                {
                    return false;   // Build cannot be empty.
                }

                versionText = versionText.Substring(0, metadataPos);
            }

            var prereleasePos = versionText.IndexOf('-');

            if (prereleasePos != -1)
            {
                version.Prerelease = versionText.Substring(prereleasePos + 1).Trim();

                if (version.Prerelease.Length == 0)
                {
                    return false;   // Prerelease cannot be empty
                }

                versionText = versionText.Substring(0, prereleasePos);

                // Ensure that the prerelease consists of only letters, digits, and periods.

                foreach (var ch in version.Prerelease)
                {
                    var lower = char.ToLowerInvariant(ch);

                    if ('a' <= lower && lower <= 'z')
                    {
                        continue;
                    }
                    else if ('0' <= lower && lower <= '9')
                    {
                        continue;
                    }
                    else if (ch == '.' || ch == '-')
                    {
                        continue;
                    }
                    else
                    {
                        return false;
                    }
                }

                // Split the prerelease into parts and verify.

                var parts = version.Prerelease.Split('.');

                // Ensure that no part is empty and that that contain only digits 
                // don't have a leading '0'.

                foreach (var part in parts)
                {
                    if (part.Length == 0)
                    {
                        return false;
                    }
                    else if (uint.TryParse(part, out var v) && part.Length > 1 && part.StartsWith("0"))
                    {
                        return false;
                    }
                }
            }

            // Parse the major, minor, and patch version numbers.

            var verParts = versionText.Split('.');

            if (verParts.Length > 3)
            {
                return false;
            }

            for (int i = 0; i < verParts.Length; i++)
            {
                if (!uint.TryParse(verParts[i], out var v))
                {
                    return false;
                }

                switch (i)
                {
                    case 0:

                        version.major      = (int)v;
                        version.majorValue = verParts[i];
                        break;

                    case 1:

                        version.minor      = (int)v;
                        version.minorValue = verParts[i];
                        break;

                    case 2:

                        version.patch      = (int)v;
                        version.patchValue = verParts[i];
                        break;
                }
            }

            return true;
        }

        /// <summary>
        /// Parses a semantic version string.
        /// </summary>
        /// <param name="versionText">The version text.</param>
        /// <returns>The parsed <see cref="SemanticVersion"/>.</returns>
        /// <exception cref="FormatException">Thrown if the version could not be parsed.</exception>
        public static SemanticVersion Parse(string versionText)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(versionText));

            if (!TryParse(versionText, out var version))
            {
                throw new FormatException($"Invalid semantic version [{versionText}].");
            }

            return version;
        }

        /// <summary>
        /// Creates a semantic version number from parameters.
        /// </summary>
        /// <param name="major">The major version.</param>
        /// <param name="minor">Optional minor version.</param>
        /// <param name="patch">Optional patch version.</param>
        /// <param name="build">Optional build.</param>
        /// <param name="prerelease">Optional prerelease.</param>
        /// <returns>The <see cref="SemanticVersion"/>.</returns>
        /// <exception cref="ArgumentException">Thrown if any of the parameters are invalid.</exception>
        public static SemanticVersion Create(int major, int minor = 0, int patch = 0, string build = null, string prerelease = null)
        {
            var sb = new StringBuilder();

            sb.Append(major);
            sb.AppendWithSeparator(minor.ToString(), ".");
            sb.AppendWithSeparator(patch.ToString(), ".");

            if (!string.IsNullOrEmpty(build))
            {
                sb.AppendWithSeparator(build, "+");
            }

            if (!string.IsNullOrEmpty(prerelease))
            {
                sb.AppendWithSeparator(prerelease, "-");
            }

            return Parse(sb.ToString());
        }

        /// <summary>
        /// Compares two non-null semantic versions.
        /// </summary>
        /// <param name="v1">The first version.</param>
        /// <param name="v2">The second version.</param>
        /// <returns>
        /// <b>-1</b> if <paramref name="v1"/> is less than <paramref name="v2"/><br/>
        /// <b>0</b> if <paramref name="v1"/> equals <paramref name="v2"/><br/>
        /// <b>+1</b> if <paramref name="v1"/> is greater than <paramref name="v2"/>
        /// </returns>
        /// <remarks>
        /// <note>
        /// A <c>null</c> version is considered to be less than a non-null version.
        /// </note>
        /// </remarks>
        public static int Compare(SemanticVersion v1, SemanticVersion v2)
        {
            var v1IsNull = object.ReferenceEquals(v1, null);
            var v2IsNull = object.ReferenceEquals(v2, null);

            if (v1IsNull && v2IsNull)
            {
                return 0;
            }
            else if (v1IsNull && !v2IsNull)
            {
                return -1;
            }
            else if (!v1IsNull && v2IsNull)
            {
                return 1;
            }

            if (v1.Major < v2.Major)
            {
                return -1;
            }
            else if (v1.Major > v2.Major)
            {
                return 1;
            }

            if (v1.Minor < v2.Minor)
            {
                return -1;
            }
            else if (v1.Minor > v2.Minor)
            {
                return 1;
            }

            if (v1.Patch < v2.Patch)
            {
                return -1;
            }
            else if (v1.Patch > v2.Patch)
            {
                return 1;
            }

            if (v1.Prerelease == null && v2.Prerelease == null)
            {
                return 0;
            }
            if (v1.Prerelease == null && v2.Prerelease != null)
            {
                return 1;
            }
            else if (v1.Prerelease != null && v2.Prerelease == null)
            {
                return -1;
            }

            // We need to compare the prerelease string parts.

            var v1Parts = v1.Prerelease.Split('.');
            var v2Parts = v2.Prerelease.Split('.');

            for (int i = 0; i < Math.Min(v1Parts.Length, v2Parts.Length); i++)
            {
                var v1Part = v1Parts[i];
                var v2Part = v2Parts[i];
                var v1Num  = uint.TryParse(v1Part, out var v1PartValue);
                var v2Num  = uint.TryParse(v2Part, out var v2PartValue);

                if (v1Num && v2Num)
                {
                    if (v1PartValue < v2PartValue)
                    {
                        return -1;
                    }
                    else if (v1PartValue > v2PartValue)
                    {
                        return 1;
                    }
                }
                else if (v1Num && !v2Num)
                {
                    return -1;
                }
                else if (!v1Num && v2Num)
                {
                    return 1;
                }
                else
                {
                    var comp = string.Compare(v1Part, v2Part, StringComparison.InvariantCultureIgnoreCase);

                    if (comp != 0)
                    {
                        return comp;
                    }
                }
            }

            if (v1Parts.Length > v2Parts.Length)
            {
                return 1;
            }
            else if (v1Parts.Length < v2Parts.Length)
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Compares two <see cref="SemanticVersion"/> instances for equality.
        /// </summary>
        /// <param name="v1">Version #1.</param>
        /// <param name="v2">Version #2.</param>
        /// <returns><c>true</c> if the versions have the same precedence.</returns>
        /// <exception cref="ArgumentNullException">Throw if either parameter is <c>null</c>.</exception>
        public static bool operator ==(SemanticVersion v1, SemanticVersion v2)
        {
            return Compare(v1, v2) == 0;
        }

        /// <summary>
        /// Compares two <see cref="SemanticVersion"/> instances for inequality.
        /// </summary>
        /// <param name="v1">Version #1.</param>
        /// <param name="v2">Version #2.</param>
        /// <returns><c>true</c> if the versions have the different precedences.</returns>
        /// <exception cref="ArgumentNullException">Throw if either parameter is <c>null</c>.</exception>
        public static bool operator !=(SemanticVersion v1, SemanticVersion v2)
        {
            return Compare(v1, v2) != 0;
        }

        /// <summary>
        /// Compares two <see cref="SemanticVersion"/> instances to see if the first is greater.
        /// </summary>
        /// <param name="v1">Version #1.</param>
        /// <param name="v2">Version #2.</param>
        /// <returns><c>true</c> <paramref name="v1"/> has greater precedence.</returns>
        /// <exception cref="ArgumentNullException">Throw if either parameter is <c>null</c>.</exception>
        public static bool operator >(SemanticVersion v1, SemanticVersion v2)
        {
            return Compare(v1, v2) > 0;
        }

        /// <summary>
        /// Compares two <see cref="SemanticVersion"/> instances to see if the first is greater or equal.
        /// </summary>
        /// <param name="v1">Version #1.</param>
        /// <param name="v2">Version #2.</param>
        /// <returns><c>true</c> <paramref name="v1"/> has the same or greater precedence.</returns>
        /// <exception cref="ArgumentNullException">Throw if either parameter is <c>null</c>.</exception>
        public static bool operator >=(SemanticVersion v1, SemanticVersion v2)
        {
            return Compare(v1, v2) >= 0;
        }

        /// <summary>
        /// Compares two <see cref="SemanticVersion"/> instances to see if the first is less.
        /// </summary>
        /// <param name="v1">Version #1.</param>
        /// <param name="v2">Version #2.</param>
        /// <returns><c>true</c> <paramref name="v1"/> has lower precedence.</returns>
        /// <exception cref="ArgumentNullException">Throw if either parameter is <c>null</c>.</exception>
        public static bool operator <(SemanticVersion v1, SemanticVersion v2)
        {
            return Compare(v1, v2) < 0;
        }

        /// <summary>
        /// Compares two <see cref="SemanticVersion"/> instances to see if the first is less or equal.
        /// </summary>
        /// <param name="v1">Version #1.</param>
        /// <param name="v2">Version #2.</param>
        /// <returns><c>true</c> <paramref name="v1"/> has the same or lower precedence.</returns>
        /// <exception cref="ArgumentNullException">Throw if either parameter is <c>null</c>.</exception>
        public static bool operator <=(SemanticVersion v1, SemanticVersion v2)
        {
            return Compare(v1, v2) <= 0;
        }

        /// <summary>
        /// Explicitly casts a <see cref="SemanticVersion"/> into a string.
        /// </summary>
        /// <param name="version">The version input.</param>
        public static explicit operator string(SemanticVersion version)
        {
            if (object.ReferenceEquals(version, null))
            {
                return null;
            }

            return version.ToString();
        }

        /// <summary>
        /// Explicitly casts a string into a <see cref="SemanticVersion"/>.
        /// </summary>
        /// <param name="version">The version input.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="version"/> is <c>null</c>.</exception>
        public static explicit operator SemanticVersion(string version)
        {
            if (version == null)
            {
                return null;
            }

            return Parse(version);
        }

        //---------------------------------------------------------------------
        // Instance members

        // We need to retain the original version strings so we can serialize
        // the version into the same format the we parsed originally.  For example,
        // we want to retain the leading 0 in the minor version of "1.01.0" and
        // also render "1.1" back as "1.0" rather than "1.0.0".

        private int     major;
        private int     minor;
        private int     patch;
        private string  majorValue;
        private string  minorValue;
        private string  patchValue;

        /// <summary>
        /// Default constuctor.
        /// </summary>
        public SemanticVersion()
        {
        }

        /// <summary>
        /// The major version number.
        /// </summary>
        public int Major
        {
            get => major;

            set
            {
                major      = value;
                majorValue = value.ToString();
            }
        }

        /// <summary>
        /// The minor version number.
        /// </summary>
        public int Minor
        {
            get => minor;

            set
            {
                minor      = value;
                minorValue = value.ToString();
            }
        }

        /// <summary>
        /// The patch version number.
        /// </summary>
        public int Patch
        {
            get => patch;

            set
            {
                patch      = value;
                patchValue = value.ToString();
            }
        }


        /// <summary>
        /// The prerelease identifer or <c>null</c>.
        /// </summary>
        public string Prerelease { get; set; }

        /// <summary>
        /// The build information or <c>null</c>.
        /// </summary>
        public string Build { get; set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (Prerelease == null && Build == null)
            {
                if (majorValue != null && minorValue != null && patchValue != null)
                {
                    return $"{majorValue}.{minorValue}.{patchValue}";
                }
                else if (minorValue != null)
                {
                    return $"{majorValue}.{minorValue}";
                }
                else
                {
                    return $"{majorValue}";
                }
            }
            else if (Prerelease != null && Build == null)
            {
                if (majorValue != null && minorValue != null && patchValue != null)
                {
                    return $"{majorValue}.{minorValue}.{patchValue}-{Prerelease}";
                }
                else if (minorValue != null)
                {
                    return $"{majorValue}.{minorValue}-{Prerelease}";
                }
                else
                {
                    return $"{majorValue}-{Prerelease}";
                }
            }
            else if (Prerelease == null && Build != null)
            {
                if (majorValue != null && minorValue != null && patchValue != null)
                {
                    return $"{majorValue}.{minorValue}.{patchValue}+{Build}";
                }
                else if (minorValue != null)
                {
                    return $"{majorValue}.{minorValue}+{Build}";
                }
                else
                {
                    return $"{majorValue}+{Build}";
                }
            }
            else
            {
                if (majorValue != null && minorValue != null && patchValue != null)
                {
                    return $"{majorValue}.{minorValue}.{patchValue}-{Prerelease}+{Build}";
                }
                else if (minorValue != null)
                {
                    return $"{majorValue}.{minorValue}-{Prerelease}+{Build}";
                }
                else
                {
                    return $"{majorValue}-{Prerelease}+{Build}";
                }
            }
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            var other = obj as SemanticVersion;

            if (object.ReferenceEquals(other, null))
            {
                return false;
            }

            return this.ToString().Equals(other.ToString(), StringComparison.InvariantCultureIgnoreCase);
        }

        /// <inheritdoc/>
        public int CompareTo(object obj)
        {
            var other = obj as SemanticVersion;

            if (other  == null)
            {
                throw new InvalidOperationException($"[{nameof(SemanticVersion)}] can only be compared to another non-null [{nameof(SemanticVersion)}] instance.");
            }

            return Compare(this, other);
        }
    }
}
