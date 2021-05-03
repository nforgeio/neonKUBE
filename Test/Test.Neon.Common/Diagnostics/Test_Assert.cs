//-----------------------------------------------------------------------------
// FILE:	    Test_Assert.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_Assert
    {
        public class NoArgsException : Exception
        {
            public NoArgsException()
            {
            }
        }

        public class OneArgException : Exception
        {
            public OneArgException()
            {
            }

            public OneArgException(string arg)
            {
                this.Arg = arg;
            }

            public string Arg { get; private set; }
        }

        public class TwoArgsException : Exception
        {
            public TwoArgsException()
            {
            }

            public TwoArgsException(string arg1, string arg2)
            {
                this.Arg1 = arg1;
                this.Arg2 = arg2;
            }

            public string Arg1 { get; private set; }
            public string Arg2 { get; private set; }
        }

        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void Requires()
        {
            // Verify that the Covenant.Requires<T>() optional string
            // parameters work correctly.

            //---------------------------------------------
            // No parameters:

            try
            {
                Covenant.Requires<NoArgsException>(false);
                Assert.True(false);
            }
            catch (NoArgsException)
            {
                // Expecting this.
            }

            //---------------------------------------------
            // One parameter:

            try
            {
                Covenant.Requires<OneArgException>(false);
                Assert.True(false);
            }
            catch (OneArgException e)
            {
                // Expecting this.

                Assert.Null(e.Arg);
            }

            try
            {
                Covenant.Requires<OneArgException>(false, "value1");
                Assert.True(false);
            }
            catch (OneArgException e)
            {
                // Expecting this.

                Assert.Equal("value1", e.Arg);
            }

            try
            {
                Covenant.Requires<OneArgException>(false, "value1", "value2");
                Assert.True(false);
            }
            catch (OneArgException e)
            {
                // Expecting this.

                Assert.Equal("value1", e.Arg);
            }

            //---------------------------------------------
            // Two parameters:

            try
            {
                Covenant.Requires<TwoArgsException>(false);
                Assert.True(false);
            }
            catch (TwoArgsException e)
            {
                // Expecting this.

                Assert.Null(e.Arg1);
            }

            try
            {
                Covenant.Requires<TwoArgsException>(false, "value1");
                Assert.True(false);
            }
            catch (TwoArgsException e)
            {
                // Expecting this.

                Assert.Equal("value1", e.Arg1);
            }

            try
            {
                Covenant.Requires<TwoArgsException>(false, "value1", "value2");
                Assert.True(false);
            }
            catch (TwoArgsException e)
            {
                // Expecting this.

                Assert.Equal("value1", e.Arg1);
                Assert.Equal("value2", e.Arg2);
            }
        }
    }
}
