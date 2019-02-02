//-----------------------------------------------------------------------------
// FILE:	    NeonVaultException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Cryptography
{
    /// <summary>
    /// Thrown by <see cref="NeonVault"/> to indicate problems.
    /// </summary>
    public class NeonVaultException : Exception
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">Optional message.</param>
        /// <param name="innerException">Optional inner exception.</param>
        public NeonVaultException(string message = null, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}
