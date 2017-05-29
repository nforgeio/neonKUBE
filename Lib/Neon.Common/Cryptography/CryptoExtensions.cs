//-----------------------------------------------------------------------------
// FILE:	    CryptoExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Cryptography
{
    /// <summary>
    /// Crytography extensions.
    /// </summary>
    public static class CryptoExtensions
    {
        /// <summary>
        /// Computes a hash from a UTF-8 encoded string.
        /// </summary>
        /// <param name="hasher">The hasher.</param>
        /// <param name="input">The input string.</param>
        /// <returns>The hash bytes.</returns>
        public static byte[] ComputeHash(this MD5 hasher, string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return new byte[16];
            }

            return hasher.ComputeHash(Encoding.UTF8.GetBytes(input));
        }
    }
}
