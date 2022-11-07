//-----------------------------------------------------------------------------
// FILE:	    CookieProtector.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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

using Microsoft.AspNetCore.DataProtection;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Web;

namespace NeonDashboard
{
    /// <summary>
    /// Provides data protection using AES Cipher.
    /// </summary>
    public class CookieProtector : IDataProtector
    {
        private AesCipher cipher;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="aesCipher"></param>
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
