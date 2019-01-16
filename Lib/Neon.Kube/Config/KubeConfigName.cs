//-----------------------------------------------------------------------------
// FILE:	    KubeConfigName.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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

namespace Neon.Kube
{
    /// <summary>
    /// Handles the parsing of a Kubernetes configuration name which by
    /// convention encodes the user, cluster, and namespace as a string.
    /// </summary>
    /// <remarks>
    /// <para>
    /// neonKUBE encodes configuration names like:
    /// </para>
    /// <para>
    /// <b>USER</b> "@" <b>CLUSTER</b> [ "/" <b>NAMESPACE</b> ]
    /// </para>
    /// <para>
    /// where <b>USER</b> is the username, <b>CLUSTER</b> identifies the
    /// cluster and <b>NAMESPACE</b> optionally identifies the Kubernetes
    /// namespace (which defaults to <b>default</b> when not specified).
    /// </para>
    /// </remarks>
    public class KubeConfigName
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Parses a Kubernetes context name like: <b>USER</b> "@" <b>CLUSTER</b> [ "/" <b>NAMESPACE</b> ]
        /// </summary>
        /// <param name="text">The input text.</param>
        /// <returns>The parsed name.</returns>
        /// <exception cref="FormatException">Thrown if the name is not valid.</exception>
        public KubeConfigName Parse(string text)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(text));

            // $todo(jeff.lill):
            //
            // We should probably honor any Kubernetes restrictions on 
            // the name parts.

            var pAt    = text.IndexOf('@');
            var pSlash = text.IndexOf('/');

            if (pAt == -1)
            {
                throw new FormatException($"Kubernetes context [name={text}] is missing an '@'.");
            }

            if (pSlash != -1 && pSlash < pAt)
            {
                throw new FormatException($"Kubernetes context [name={text}] has a '/' before the '@'.");
            }

            var name = new KubeConfigName();

            name.User = text.Substring(0, pAt);

            pAt++;
            if (pSlash == -1)
            {
                name.Cluster   = text.Substring(pAt);
                name.Namespace = "default";
            }
            else
            {
                name.Cluster   = text.Substring(pAt, pSlash - pAt);
                name.Namespace = text.Substring(pSlash + 1);

                if (name.Namespace == string.Empty)
                {
                    name.Namespace = "default";
                }
            }

            if (name.User == string.Empty)
            {
                throw new FormatException($"Kubernetes context [name={text}] specifies an invalid user.");
            }

            if (name.Cluster == string.Empty)
            {
                throw new FormatException($"Kubernetes context [name={text}] specifies an invalid cluster.");
            }

            return name;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Internal constructor.
        /// </summary>
        private KubeConfigName()
        {
        }

        /// <summary>
        /// Returns the username.
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

        /// <inheritdoc/>
        public override string ToString()
        {
            if (string.IsNullOrEmpty(Namespace))
            {
                return $"{User}@{Cluster}";
            }
            else
            {
                return $"{User}@{Cluster}/{Namespace}";
            }
        }
    }
}
