//-----------------------------------------------------------------------------
// FILE:	    KubeContextName.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;

namespace Neon.Kube.Login
{
    /// <summary>
    /// Handles the parsing of a Kubernetes context name which by convention
    /// encodes the user, cluster, and optional namespace as a string.
    /// </summary>
    /// <remarks>
    /// <para>
    /// neonKUBE encodes context names like:
    /// </para>
    /// <para>
    /// <b>USER</b> "@" <b>CLUSTER</b> [ "/" <b>NAMESPACE</b> ]
    /// </para>
    /// <para>k
    /// where <b>USER</b> is the username, <b>CLUSTER</b> identifies the
    /// cluster and <b>NAMESPACE</b> optionally identifies the Kubernetes
    /// namespace (which defaults to <b>default</b> when not specified).
    /// </para>
    /// </remarks>
    public class KubeContextName
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Explicitly casts a <see cref="KubeContextName"/> into a <c>string</c>.
        /// </summary>
        /// <param name="name">The context name or <c>null</c>.</param>
        /// <returns>The converted string.</returns>
        public static explicit operator string(KubeContextName name)
        {
            if (name == null)
            {
                return null;
            }
            else
            {
                return name.ToString();
            }
        }

        /// <summary>
        /// Explicitly casts a <c>string</c> into a <see cref="KubeContextName"/>.
        /// </summary>
        /// <param name="name">The context name or <c>null</c>.</param>
        /// <returns>The converted context name.</returns>
        public static explicit operator KubeContextName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            else
            {
                return KubeContextName.Parse(name);
            }
        }

        /// <summary>
        /// Compares <see cref="KubeContextName"/> for equality.
        /// </summary>
        /// <param name="name1">Name 1</param>
        /// <param name="name2">Name 2</param>
        /// <returns><c>true</c> if the names are equal.</returns>
        public static bool operator ==(KubeContextName name1, KubeContextName name2)
        {
            var name1IsNull = object.ReferenceEquals(name1, null);
            var name2IsNull = object.ReferenceEquals(name2, null);

            if (name1IsNull && name2IsNull)
            {
                return true;
            }
            else if (name1IsNull != name2IsNull)
            {
                return false;
            }

            return name1.User == name2.User &&
                   name1.Cluster == name2.Cluster &&
                   name1.Namespace == name2.Namespace;
        }

        /// <summary>
        /// Compares <see cref="KubeContextName"/> for inequality.
        /// </summary>
        /// <param name="name1">Name 1</param>
        /// <param name="name2">Name 2</param>
        /// <returns><c>true</c> if the names are not equal.</returns>
        public static bool operator !=(KubeContextName name1, KubeContextName name2)
        {
            var name1IsNull = object.ReferenceEquals(name1, null);
            var name2IsNull = object.ReferenceEquals(name2, null);

            if (name1IsNull && name2IsNull)
            {
                return false;
            }
            else if (name1IsNull != name2IsNull)
            {
                return true;
            }

            return name1.User != name2.User ||
                   name1.Cluster != name2.Cluster ||
                   name1.Namespace != name2.Namespace;
        }

        /// <summary>
        /// Parses a Kubernetes context name.
        /// </summary>
        /// <param name="input">The input text.</param>
        /// <returns>The parsed name.</returns>
        /// <remarks>
        /// <para>
        /// neonKUBE supports a context name format that includes a user name like:
        /// </para>
        /// <example>
        /// <b>USER</b> "@" <b>CLUSTER</b> [ "/" <b>NAMESPACE</b> ]
        /// </example>
        /// <para>
        /// We assume that contexts including an <b>"@"</b> are associated with a
        /// neonKUBE cluster and use this to link to additional login information.
        /// We can also parse context names without an <b>"@"</b>.  The parsed
        /// <see cref="User"/> property will be set to <c>null</c> in this case.
        /// </para>
        /// <example>
        /// <b>CLUSTER</b> [ "/" <b>NAMESPACE</b> ]
        /// </example>
        /// <note>
        /// The <see cref="User"/>, <see cref="Cluster"/>, and <see cref="Namespace"/>
        /// properties will be converted to lowercase.
        /// </note>
        /// </remarks>
        /// <exception cref="FormatException">Thrown if the name is not valid.</exception>
        public static KubeContextName Parse(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new FormatException("Context name cannot be empty.");
            }

            var pAt    = input.IndexOf('@');
            var pSlash = input.IndexOf('/');

            if (pAt == -1)
            {
                // Looks like a standard (non-neonKUBE) context name.

                var name = new KubeContextName();

                if (pSlash == -1)
                {
                    name.Cluster   = input;
                    name.Namespace = "default";
                }
                else
                {
                    name.Cluster   = input.Substring(0, pSlash);
                    name.Namespace = input.Substring(pSlash + 1);

                    if (name.Namespace == string.Empty)
                    {
                        name.Namespace = "default";
                    }
                }

                if (name.User == string.Empty)
                {
                    throw new FormatException($"Kubernetes context [name={input}] specifies an invalid user.");
                }

                if (name.Cluster == string.Empty)
                {
                    throw new FormatException($"Kubernetes context [name={input}] specifies an invalid cluster.");
                }

                name.User      = null;
                name.Cluster   = name.Cluster.ToLowerInvariant();
                name.Namespace = name.Namespace.ToLowerInvariant();

                name.Validate();

                return name;
            }
            else
            {
                // Looks like a neonKUBE context name.

                if (pSlash != -1 && pSlash < pAt)
                {
                    throw new FormatException($"Kubernetes context [name={input}] has a '/' before the '@'.");
                }

                var name = new KubeContextName();

                name.User = input.Substring(0, pAt);

                pAt++;
                if (pSlash == -1)
                {
                    name.Cluster   = input.Substring(pAt);
                    name.Namespace = "default";
                }
                else
                {
                    name.Cluster   = input.Substring(pAt, pSlash - pAt);
                    name.Namespace = input.Substring(pSlash + 1);

                    if (name.Namespace == string.Empty)
                    {
                        name.Namespace = "default";
                    }
                }

                if (name.User == string.Empty)
                {
                    throw new FormatException($"Kubernetes context [name={input}] specifies an invalid user.");
                }

                if (name.Cluster == string.Empty)
                {
                    throw new FormatException($"Kubernetes context [name={input}] specifies an invalid cluster.");
                }

                name.User      = name.User.ToLowerInvariant();
                name.Cluster   = name.Cluster.ToLowerInvariant();
                name.Namespace = name.Namespace.ToLowerInvariant();

                name.Validate();

                return name;
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Internal constructor.
        /// </summary>
        private KubeContextName()
        {
        }

        /// <summary>
        /// Parameterized constructor.
        /// </summary>
        /// <param name="username">The username.  This may be <c>null</c> for non-neonKUBE cluster logins.</param>
        /// <param name="cluster">The cluster name.</param>
        /// <param name="kubeNamespace">Optionally specifies the namespace (defaults to <b>"default"</b>).</param>
        /// <remarks>
        /// <note>
        /// The username, cluster, and namespace will be converted to lowercase.
        /// </note>
        /// </remarks>
        public KubeContextName(string username, string cluster, string kubeNamespace = "default")
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(cluster), nameof(cluster));

            if (string.IsNullOrEmpty(kubeNamespace))
            {
                kubeNamespace = "default";
            }

            this.User      = username?.ToLowerInvariant();
            this.Cluster   = cluster.ToLowerInvariant();
            this.Namespace = kubeNamespace.ToLowerInvariant();

            Validate();
        }

        /// <summary>
        /// <para>
        /// Returns the username if present, otherwise <c>null</c>.
        /// </para>
        /// <note>
        /// We currently assume that only contexts with a <see cref="User"/> are
        /// neonKUBE clusters.
        /// </note>
        /// </summary>
        public string User { get; private set; }

        /// <summary>
        /// Returns the cluster name.
        /// </summary>
        public string Cluster { get; private set; }

        /// <summary>
        /// Returns the namespace or <b>default</b>.
        /// </summary>
        public string Namespace { get; private set; }

        /// <summary>
        /// Indicates whether this is a neonKUBE context name or a standard one.
        /// neonKUBE contexts include a user name before the <b>"@"</b> symbol.
        /// </summary>
        public bool IsNeonKube => User != null;

        /// <summary>
        /// Validates that a name component includes only nvalid characters.
        /// </summary>
        /// <param name="name">The name beoing tested.</param>
        /// <returns><c>true</c> if the name is OK.</returns>
        private bool ValidateName(string name)
        {
            foreach (var ch in name)
            {
                if (char.IsLetterOrDigit(ch) || ch == '.' || ch == '-')
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// Ensures that the properties are valid.
        /// </summary>
        /// <exception cref="FormatException">Thrown when there's a problem.</exception>
        private void Validate()
        {
            // The user property may be NULL.

            if (User != null)
            {
                if (User.Length == 0)
                {
                    throw new FormatException($"[{nameof(KubeConfig)}.{nameof(User)}] cannot be empty.");
                }

                if (User.Length > 253)
                {
                    throw new FormatException($"[{nameof(KubeConfig)}.{nameof(User)}={User}] exceeds 253 characters.");
                }

                if (!ValidateName(User))
                {
                    throw new FormatException($"[{nameof(KubeConfig)}.{nameof(User)}={User}] includes invalid characters.");
                }
            }

            // Looks like these fields must all be non-empty, be a maximum of 253 characters
            // long and may include only letters, digits, dashes, and periods.

            if (Cluster.Length == 0)
            {
                throw new FormatException($"[{nameof(KubeConfig)}.{nameof(Cluster)}] cannot be empty.");
            }

            if (Cluster.Length > 253)
            {
                throw new FormatException($"[{nameof(KubeConfig)}.{nameof(Cluster)}={Cluster}] exceeds 253 characters.");
            }

            if (!ValidateName(Cluster))
            {
                throw new FormatException($"[{nameof(KubeConfig)}.{nameof(Cluster)}={Cluster}] includes invalid characters.");
            }

            if (Namespace.Length == 0)
            {
                throw new FormatException($"[{nameof(KubeConfig)}.{nameof(Namespace)}] cannot be empty.");
            }

            if (Namespace.Length > 253)
            {
                throw new FormatException($"[{nameof(KubeConfig)}.{nameof(Namespace)}={Namespace}] exceeds 253 characters.");
            }

            if (!ValidateName(Namespace))
            {
                throw new FormatException($"[{nameof(KubeConfig)}.{nameof(Namespace)}={Namespace}] includes invalid characters.");
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (User == null)
            {
                if (string.IsNullOrEmpty(Namespace) || Namespace == "default")
                {
                    return $"{Cluster}";
                }
                else
                {
                    return $"{Cluster}/{Namespace}";
                }
            }
            else
            {
                if (string.IsNullOrEmpty(Namespace) || Namespace == "default")
                {
                    return $"{User}@{Cluster}";
                }
                else
                {
                    return $"{User}@{Cluster}/{Namespace}";
                }
            }
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            var other = obj as KubeContextName;

            if (other == null)
            {
                return false;
            }

            return this.User == other.User &&
                   this.Cluster == other.Cluster &&
                   this.Namespace == other.Namespace;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            if (User == null)
            {
                return Cluster.GetHashCode() ^ Namespace.GetHashCode();
            }
            else
            {
                return User.GetHashCode() ^ Cluster.GetHashCode() ^ Namespace.GetHashCode();
            }
        }
    }
}
