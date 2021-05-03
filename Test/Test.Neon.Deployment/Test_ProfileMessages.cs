//-----------------------------------------------------------------------------
// FILE:	    Test_ProfileMessages.cs
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
    public class Test_ProfileMessages
    {
        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonDeployment)]
        public void Request_Create()
        {
            //---------------------------------------------

            var request = ProfileRequest.Create("TEST");

            Assert.Equal("TEST", request.Command);
            Assert.Empty(request.Args);
            Assert.Equal("TEST:", request.ToString());

            //---------------------------------------------

            var args = new Dictionary<string, string>();

            request = ProfileRequest.Create("TEST", args);

            Assert.Equal("TEST", request.Command);
            Assert.Empty(request.Args);
            Assert.Equal("TEST:", request.ToString());

            //---------------------------------------------

            args["arg1"] = "1";

            request = ProfileRequest.Create("TEST", args);

            Assert.Equal("TEST", request.Command);
            Assert.Single(request.Args);
            Assert.Equal("1", request.Args["arg1"]);
            Assert.Equal("TEST: arg1=1", request.ToString());

            //---------------------------------------------

            args["arg2"] = "2";

            request = ProfileRequest.Create("TEST", args);

            Assert.Equal("TEST", request.Command);
            Assert.Equal(2, request.Args.Count);
            Assert.Equal("1", request.Args["arg1"]);
            Assert.Equal("2", request.Args["arg2"]);
            Assert.Equal("TEST: arg1=1, arg2=2", request.ToString());
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonDeployment)]
        public void Request_Parse_NoArgs()
        {
            var request = ProfileRequest.Parse("TEST:");

            Assert.Equal("TEST", request.Command);
            Assert.Empty(request.Args);
            Assert.Equal("TEST:", request.ToString());
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonDeployment)]
        public void Request_Parse_WithArgs()
        {
            var request = ProfileRequest.Parse("TEST: arg1=1, arg2=2");

            Assert.Equal("TEST", request.Command);
            Assert.Equal(2, request.Args.Count);
            Assert.Equal("1", request.Args["arg1"]);
            Assert.Equal("2", request.Args["arg2"]);
            Assert.Equal("TEST: arg1=1, arg2=2", request.ToString());
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonDeployment)]
        public void Request_ParseFailure()
        {
            Assert.Throws<ArgumentNullException>(() => ProfileRequest.Parse(null));
            Assert.Throws<ArgumentNullException>(() => ProfileRequest.Parse(string.Empty));
            Assert.Throws<FormatException>(() => ProfileRequest.Parse("TEST"));
            Assert.Throws<FormatException>(() => ProfileRequest.Parse("TEST: arg"));
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonDeployment)]
        public void Response_Create()
        {
            var response = ProfileResponse.Create("HELLO WORLD!");

            Assert.True(response.Success);
            Assert.Equal("HELLO WORLD!", response.Value);
            Assert.Null(response.JObject);
            Assert.Equal("OK: HELLO WORLD!", response.ToString());
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonDeployment)]
        public void Response_CreateJson()
        {
            //---------------------------------------------
            
            var response = ProfileResponse.Create(new JObject());

            Assert.True(response.Success);
            Assert.Equal(ProfileStatus.OK, response.Status);
            Assert.Null(response.Value);
            Assert.NotNull(response.JObject);

            Assert.Empty(response.JObject.Properties());
            Assert.Equal("OK-JSON: {}", response.ToString());

            //---------------------------------------------

            var jObj = 
                new JObject(
                    new JProperty("hello", "world!")
                );

            response = ProfileResponse.Create(jObj);

            Assert.True(response.Success);
            Assert.Equal(ProfileStatus.OK, response.Status);
            Assert.Null(response.Value);
            Assert.NotNull(response.JObject);

            Assert.Single(response.JObject.Properties());
            Assert.Equal("world!", response.JObject["hello"]);
            Assert.Equal("OK-JSON: {\"hello\":\"world!\"}", response.ToString());
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonDeployment)]
        public void Response_CreateError()
        {
            //---------------------------------------------

            var response = ProfileResponse.CreateError(ProfileStatus.Connect, "ERROR MESSAGE");

            Assert.False(response.Success);
            Assert.Equal(ProfileStatus.Connect, response.Status);
            Assert.Null(response.Value);
            Assert.Null(response.JObject);
            Assert.Equal("ERROR MESSAGE", response.Error);
            Assert.Equal($"ERROR[{ProfileStatus.Connect}]: ERROR MESSAGE", response.ToString());

            //---------------------------------------------

            Assert.Throws<ArgumentNullException>(() => ProfileResponse.CreateError(null, null));
            Assert.Throws<ArgumentNullException>(() => ProfileResponse.CreateError(string.Empty, string.Empty));
            Assert.Throws<ArgumentNullException>(() => ProfileResponse.CreateError(ProfileStatus.Connect, null));
            Assert.Throws<ArgumentNullException>(() => ProfileResponse.CreateError(null, "Error"));
            Assert.Throws<ArgumentNullException>(() => ProfileResponse.CreateError(ProfileStatus.Connect, string.Empty));
            Assert.Throws<ArgumentNullException>(() => ProfileResponse.CreateError(string.Empty, "Error"));
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonDeployment)]
        public void Response_Parse()
        {
            var response = ProfileResponse.Parse("OK: HELLO WORLD!");

            Assert.True(response.Success);
            Assert.Equal("HELLO WORLD!", response.Value);
            Assert.Null(response.JObject);
            Assert.Equal("OK: HELLO WORLD!", response.ToString());
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonDeployment)]
        public void Response_ParseJson()
        {
            var response = ProfileResponse.Parse("OK-JSON: {\"hello\":\"world!\"}");

            Assert.True(response.Success);
            Assert.Equal(ProfileStatus.OK, response.Status);
            Assert.Null(response.Value);
            Assert.Single(response.JObject.Properties());
            Assert.Equal("world!", response.JObject["hello"]);
            Assert.Equal("OK-JSON: {\"hello\":\"world!\"}", response.ToString());
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonDeployment)]
        public void Response_ParseError()
        {
            var response = ProfileResponse.Parse($"ERROR[{ProfileStatus.BadRequest}]: HELLO WORLD!");

            Assert.False(response.Success);
            Assert.Null(response.Value);
            Assert.Null(response.JObject);
            Assert.Equal("HELLO WORLD!", response.Error);
            Assert.Equal($"ERROR[{ProfileStatus.BadRequest}]: HELLO WORLD!", response.ToString());
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonDeployment)]
        public void Response_ParseFailure()
        {
            Assert.Throws<ArgumentNullException>(() => ProfileResponse.Parse(null));
            Assert.Throws<ArgumentNullException>(() => ProfileResponse.Parse(string.Empty));
            Assert.Throws<FormatException>(() => ProfileResponse.Parse("NOT-OK"));
            Assert.Throws<FormatException>(() => ProfileResponse.Parse("NOT-OK"));
            Assert.Throws<FormatException>(() => ProfileResponse.Parse("ERROR:"));
            Assert.Throws<FormatException>(() => ProfileResponse.Parse("ERROR[:"));
            Assert.Throws<FormatException>(() => ProfileResponse.Parse("ERROR]:"));
            Assert.Throws<FormatException>(() => ProfileResponse.Parse("ERROR[]:"));
            Assert.Throws<FormatException>(() => ProfileResponse.Parse("OK-JSON: { BAD JSON }"));
        }
    }
}
