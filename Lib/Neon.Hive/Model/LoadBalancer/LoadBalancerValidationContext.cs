//-----------------------------------------------------------------------------
// FILE:	    LoadBalancerValidationContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

namespace Neon.Hive
{
    /// <summary>
    /// Holds global load balancer definition state that may be 
    /// accessed while validating a tree of load balancer settings
    /// and rules.
    /// </summary>
    public class LoadBalancerValidationContext
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="loadBalancerName">The load balancer name.</param>
        /// <param name="settings">The load balancer settings.</param>
        /// <param name="certificates">The optional certificates as name/value tuples.</param>
        public LoadBalancerValidationContext(string loadBalancerName, LoadBalancerSettings settings, Dictionary<string, TlsCertificate> certificates = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(loadBalancerName));

            this.LoadBalancerName = loadBalancerName;
            this.Settings         = settings;
            this.Certificates     = certificates ?? new Dictionary<string, TlsCertificate>();
        }

        /// <summary>
        /// Returns the load balancer name.
        /// </summary>
        public string LoadBalancerName { get; private set; }

        /// <summary>
        /// Returns the load balancer settings.
        /// </summary>
        public LoadBalancerSettings Settings { get; private set; }

        /// <summary>
        /// Returns a dictionary that maps case sensitive certificate names to the
        /// corresponding <see cref="TlsCertificate"/>.
        /// </summary>
        public Dictionary<string, TlsCertificate> Certificates { get; private set; }

        /// <summary>
        /// Returns the list of error messages.
        /// </summary>
        public List<string> Errors { get; private set; } = new List<string>();

        /// <summary>
        /// Indicates that certificate references will be validated.  This defaults to <c>true</c>.
        /// </summary>
        public bool ValidateCertificates { get; set; } = true;

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
