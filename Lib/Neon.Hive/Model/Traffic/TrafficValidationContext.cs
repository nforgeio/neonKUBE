//-----------------------------------------------------------------------------
// FILE:	    TrafficValidationContext.cs
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
    /// Holds global traffic manager definition state that may be 
    /// accessed while validating a tree of traffic manager settings
    /// and rules.
    /// </summary>
    public class TrafficValidationContext
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="trafficManagerName">The traffic manager name.</param>
        /// <param name="settings">The traffic manager settings.</param>
        /// <param name="certificates">The optional certificates as name/value tuples.</param>
        public TrafficValidationContext(string trafficManagerName, TrafficSettings settings, Dictionary<string, TlsCertificate> certificates = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(trafficManagerName));

            this.TrafficManagerName = trafficManagerName;
            this.Settings           = settings;
            this.Certificates       = certificates ?? new Dictionary<string, TlsCertificate>();
        }

        /// <summary>
        /// Returns the traffic manager name.
        /// </summary>
        public string TrafficManagerName { get; private set; }

        /// <summary>
        /// Returns the traffic manager settings.
        /// </summary>
        public TrafficSettings Settings { get; private set; }

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
        /// Indicates that DNS resolvers will be validated.  This defaults to <c>true</c>.
        /// </summary>
        public bool ValidateResolvers { get; set; } = true;

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
        /// <exception cref="HiveDefinitionException">Thrown when there are errors.</exception>
        public void ThrowIfErrors()
        {
            if (HasErrors)
            {
                var sb = new StringBuilder();

                foreach (var message in Errors)
                {
                    sb.AppendLine("ERROR: " + message);
                }

                throw new HiveDefinitionException(sb.ToString());
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
