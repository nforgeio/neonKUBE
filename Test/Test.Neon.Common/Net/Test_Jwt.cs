//-----------------------------------------------------------------------------
// FILE:	    Test_Jwt.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Net;
using Neon.Retry;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_Jwt
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Parse()
        {
            // Verify that we can parse a valid JWT.

            var headerPart    = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";
            var payloadPart   = "eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ";
            var signaturePart = "SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
            var encoded       = $"{headerPart}.{payloadPart}.{signaturePart}";

            var jwt = Jwt.Parse(encoded);

            Assert.Equal("HS256", jwt.Header["alg"]);
            Assert.Equal("JWT", jwt.Header["typ"]);

            Assert.Equal("1234567890", jwt.Payload["sub"]);
            Assert.Equal("John Doe", jwt.Payload["name"]);
            Assert.Equal("1516239022", jwt.Payload["iat"]);

            // Verify that we parsed the signature correctly by converting
            // the base64url encoded input into a standard base64 encoding
            // and then comparing the parsed byte array with the decoded input.

            var base64Signature = signaturePart.Replace('-', '+');

            base64Signature = base64Signature.Replace('_', '/');

            switch (base64Signature.Length % 3)
            {
                case 0:

                    break;  // No padding required.

                case 1:

                    base64Signature += "=";
                    break;

                case 2:

                    base64Signature += "==";
                    break;
            }

            var signatureBytes = Convert.FromBase64String(base64Signature);

            Assert.Equal(signatureBytes, jwt.Signature);

            // Verify that jwt.ToString() returns the input.

            Assert.Equal(encoded, jwt.ToString());
        }
    }
}
