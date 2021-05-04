//-----------------------------------------------------------------------------
// FILE:	    Test_NeonAssistantServer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

using Neon.Common;
using Neon.Deployment;
using Neon.IO;
using Neon.Xunit;

namespace TestDeployment
{
    // NOTE: These tests need to be run manually.

    [Trait(TestTrait.Area, TestArea.NeonDeployment)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_OnePassword
    {
        [Fact(Skip = "This test needs to be run manually.")]
        public void Basics()
        {
            // You need to manually configure your 1Password credentials below for this
            // test to work.
            //
            // +========================================================+
            // | WARNING!                                               |
            // | --------                                               |
            // | BE VERY SURE TO DELETE THESE CREDENTIALS AFTERWARDS!!! |
            // | YOU DON'T WANT SECRETS TO BE COMMITTED TO GITHUB!      |
            // +========================================================+

            var signinAddress  = "https://neonforge.1password.com/";            // Edit as required
            var account        = "jeff@neonforge.com";                          // Edit as required
            var secretKey      = "";                                            // Insert your secret key
            var masterPassword = "";                                            // Insert your master password
            var defaultVault   = "user-jeff";                                   // Edit as required

            //-----------------------------------------------------------------
            // Verify that we see exceptions when 1Password isn't signed-in.

            Assert.False(OnePassword.Signedin);
            Assert.Throws<OnePasswordException>(() => OnePassword.GetSecretPassword("AWS_ACCESS_KEY_ID"));
            Assert.Throws<OnePasswordException>(() => OnePassword.GetSecretValue("EMAIL_ADDRESS"));
            OnePassword.Signout();      // This shouldn't throw an exception when we're not signed-in

            //-----------------------------------------------------------------
            // Verify 1Password configuratiion

            OnePassword.Configure(signinAddress, account, secretKey, masterPassword, defaultVault);

            var value = OnePassword.GetSecretPassword("AWS_ACCESS_KEY_ID");

            Assert.NotEmpty(value);

            OnePassword.Signout();

            //-----------------------------------------------------------------
            // Verify normal sign-in.

            OnePassword.Signin(account, masterPassword, defaultVault);

            value = OnePassword.GetSecretPassword("AWS_ACCESS_KEY_ID");

            Assert.NotEmpty(value);

            //-----------------------------------------------------------------
            // Verify that we can automatically renew session tokens after they
            // expire by waiting over 30 minutes before attempting a lookup.

            Thread.Sleep(TimeSpan.FromMinutes(35));

            value = OnePassword.GetSecretPassword("AWS_ACCESS_KEY_ID");

            Assert.NotEmpty(value);
        }
    }
}
