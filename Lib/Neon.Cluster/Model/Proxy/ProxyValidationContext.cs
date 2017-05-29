//-----------------------------------------------------------------------------
// FILE:	    ProxyValidationContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Cryptography;

namespace Neon.Cluster
{
    /// <summary>
    /// Holds global proxy definition state that may be accessed while
    /// validating a tree of proxy definition related objects.
    /// </summary>
    public class ProxyValidationContext
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The proxy name.</param>
        /// <param name="settings">The proxy settings.</param>
        /// <param name="certificates">The optional certificates as name/value tuples.</param>
        public ProxyValidationContext(string name, ProxySettings settings, Dictionary<string, TlsCertificate> certificates = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(settings != null);

            this.Name         = name;
            this.Settings     = settings;
            this.Certificates = certificates ?? new Dictionary<string, TlsCertificate>();
        }

        /// <summary>
        /// Returns the proxy name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the proxy settings.
        /// </summary>
        public ProxySettings Settings { get; private set; }

        /// <summary>
        /// Returns a dictionary that maps a case sensitive certificate name to the
        /// corresponding <see cref="TlsCertificate"/>.
        /// </summary>
        public Dictionary<string, TlsCertificate> Certificates { get; private set; }

        /// <summary>
        /// Returns the list of error messages.
        /// </summary>
        public List<string> Errors { get; private set; } = new List<string>();

        /// <summary>
        /// Returns <c>true</c> if any errors were reported.
        /// </summary>
        public bool HasErrors
        {
            get { return Errors.Count > 0; }
        }

        /// <summary>
        /// Reports an error.
        /// </summary>
        /// <param name="message">The error message.</param>
        public void Error(string message)
        {
            Errors.Add(message);
        }

        /// <summary>
        /// Throws an exception if any errors were detected.
        /// </summary>
        /// <exception cref="ClusterDefinitionException">Thrown when there are errors.</exception>
        public void ThrowIfErrors()
        {
            if (HasErrors)
            {
                var sb = new StringBuilder();

                foreach (var message in Errors)
                {
                    sb.AppendLine("ERROR: " + message);
                }

                throw new ClusterDefinitionException(sb.ToString());
            }
        }

        /// <summary>
        /// Returns the errors as a string, one error per line.
        /// </summary>
        /// <returns>The error string.</returns>
        public string GetErrors()
        {
            var sb = new StringBuilder();

            foreach (var message in Errors)
            {
                sb.AppendLine(message);
            }

            return sb.ToString();
        }
    }
}
