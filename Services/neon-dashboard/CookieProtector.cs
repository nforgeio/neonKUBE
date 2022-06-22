//-----------------------------------------------------------------------------
// FILE:	    CookieProtector.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Web;

using Blazor.Analytics;
using Blazor.Analytics.Components;

using Blazored.LocalStorage;

using k8s;

using Prometheus;

using Segment;

using StackExchange.Redis;

using Tailwind;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using System.Xml.Linq;
using System.IO;
using Neon.Cryptography;

namespace NeonDashboard
{
    /// <summary>
    /// Provides data protection using AES Cipher.
    /// </summary>
    public class CookieProtector : IDataProtector
    {
        private AesCipher cipher;
        public CookieProtector(AesCipher aesCipher)
        {
            cipher = aesCipher;
        }

        /// <inheritdoc/>
        public IDataProtector CreateProtector(string purpose)
        {
            return this;
        }

        /// <inheritdoc/>
        public byte[] Protect(byte[] plaintext)
        {
            return cipher.EncryptToBytes(plaintext);
        }

        /// <inheritdoc/>
        public byte[] Unprotect(byte[] protectedData)
        {
            return cipher.DecryptBytesFrom(protectedData);
        }
    }
}
