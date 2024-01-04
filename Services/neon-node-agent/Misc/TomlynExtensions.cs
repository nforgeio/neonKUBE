//-----------------------------------------------------------------------------
// FILE:        TomlynExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;

using Tomlyn;
using Tomlyn.Syntax;

namespace NeonNodeAgent
{
    /// <summary>
    /// Tomlyn is difficult to use because it's hard to extract the actual string name from
    /// an table or item.  For tables, we can use [Name.ToString()] to get the string name
    /// but doing the same for items may include double quotes and possibly extra spaces after
    /// the name.  We're to add some extension methods to deal with this.
    /// </summary>
    public static class TomlynExtensions
    {
        /// <summary>
        /// Normalizes a name string by removing any quotes and then triming any whitespace.
        /// </summary>
        /// <param name="raw">The raw name.</param>
        /// <returns>The normalized name.</returns>
        private static string Normalize(string raw)
        {
            Covenant.Requires<ArgumentException>(!string.IsNullOrEmpty(raw));

            if (raw.StartsWith('"') && raw.EndsWith('"'))
            {
                raw = raw.Substring(1, raw.Length - 2);
            }

            return raw.Trim();
        }

        /// <summary>
        /// $hack(jefflill): This extension returns the actual string name for a <see cref="KeySyntax"/>.
        /// </summary>
        /// <param name="key">The source key.</param>
        /// <returns>The key's string name.</returns>
        public static string GetName(this KeySyntax key) => Normalize(key.ToString());

        /// <summary>
        /// $hack(jefflill): This extension returns the actual string name for a <see cref="BareKeyOrStringValueSyntax"/>.
        /// </summary>
        /// <param name="key">The source key.</param>
        /// <returns>The key's string name.</returns>
        public static string GetName(this BareKeyOrStringValueSyntax key) => Normalize(key.ToString());

        /// <summary>
        /// $hack(jefflill): This extension returns the actual string value for a <see cref="ValueSyntax"/>.
        /// </summary>
        /// <param name="value">The source value.</param>
        /// <returns>The values's string name.</returns>
        public static string GetValue(this ValueSyntax value) => Normalize(value.ToString());
    }
}
