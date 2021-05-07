//-----------------------------------------------------------------------------
// FILE:	    Test_ExceptionResult.cs
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

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    [Trait(TestTrait.Category, TestArea.NeonCommon)]
    public partial class Test_ExceptionResult
    {
        public class TestException1 : Exception
        {
            public TestException1()
            {
            }
        }

        internal class TestException2 : Exception
        {
            public TestException2(string message)
                : base(message)
            {
            }
        }

        private class TestException3 : Exception
        {
            public TestException3(string message, Exception innerException)
                : base(message, innerException)
            {
            }
        }

        [Fact]
        public void ExceptionResult_NoError()
        {
            // Verify that an exception result with no error doesn't rethrow.

            var er = new ExceptionResult();

            Assert.Null(er.ExceptionType);
            Assert.Null(er.ExceptionMessage);

            er.ThrowOnError();
        }

        [Fact]
        public void ExceptionResult_Rethrow()
        {
            // Verify that an exception result with an exception type
            // that exists in a loaded assembly will be rethrown.  Note
            // that we're going to white box testing to ensure that we
            // handle different combinations of exception constructor
            // parameters as well as public, internal, and private
            // exceptions.

            // Default constructor

            var er = new ExceptionResult()
            {
                ExceptionType = typeof(TestException1).FullName
            };

            try
            {
                er.ThrowOnError();
            }
            catch (TestException1 e)
            {
                Assert.NotNull(e.Message);
            }

            // Message only constructor.

            er = new ExceptionResult()
            {
                ExceptionType    = typeof(TestException2).FullName,
                ExceptionMessage = "test"
            };

            try
            {
                er.ThrowOnError();
            }
            catch (TestException2 e)
            {
                Assert.Equal("test", e.Message);
            }

            // Message and inner exception constructor.  We don't actually
            // marshall the inner exception but we want to be able to rethrow
            // exception types that don't have a constuctor with only the
            // message parameter.

            er = new ExceptionResult()
            {
                ExceptionType    = typeof(TestException3).FullName,
                ExceptionMessage = "test"
            };

            try
            {
                er.ThrowOnError();
            }
            catch (TestException3 e)
            {
                Assert.Equal("test", e.Message);
            }

            // Verify that we can rethrow an exception defined in another assembly.

            er = new ExceptionResult()
            {
                ExceptionType    = typeof(InvalidOperationException).FullName,
                ExceptionMessage = "test"
            };

            try
            {
                er.ThrowOnError();
            }
            catch (InvalidOperationException e)
            {
                Assert.Equal("test", e.Message);
            }
        }

        [Fact]
        public void ExceptionResult_CatchAll()
        {
            // Verify that a [CatchAllException] is thrown when a local exception
            // type matching the type name doesn't exist.

            // Default constructor

            var er = new ExceptionResult()
            {
                ExceptionType    = "This.Exception.Not.Present",
                ExceptionMessage = "Hello"
            };

            try
            {
                er.ThrowOnError();
            }
            catch (CatchAllException e)
            {
                Assert.Equal("This.Exception.Not.Present", e.ExceptionType);
                Assert.Equal("Hello", e.Message);
            }
        }

        [Fact]
        public void ExceptionResult_Serialize()
        {
            // Verify that we serialize an [ExceptionResult].

            var erOut = new ExceptionResult()
            {
                ExceptionType     = typeof(TestException2).FullName,
                ExceptionMessage = "test"
            };

            var json = NeonHelper.JsonSerialize(erOut);
            var erIn = NeonHelper.JsonDeserialize<ExceptionResult>(json);

            Assert.Equal(erOut.ExceptionType, erIn.ExceptionType);
            Assert.Equal(erOut.ExceptionMessage, erIn.ExceptionMessage);
        }

        [Fact]
        public void ExceptionResultT()
        {
            // Verify that an exception result with no error doesn't rethrow.

            var er = new ExceptionResult<int>();

            Assert.Null(er.ExceptionType);
            Assert.Null(er.ExceptionMessage);

            er.ThrowOnError();
        }

        [Fact]
        public void ExceptionResultT_Rethrow()
        {
            // Verify that an exception result with an exception type
            // that exists in a loaded assembly will be rethrown.  Note
            // that we're going to white box testing to ensure that we
            // handle different combinations of exception constructor
            // parameters as well as public, internal, and private
            // exceptions.

            // Default constructor

            var er = new ExceptionResult<int>()
            {
                ExceptionType = typeof(TestException1).FullName
            };

            try
            {
                er.ThrowOnError();
            }
            catch (TestException1 e)
            {
                Assert.NotNull(e.Message);
            }

            // Message only constructor.

            er = new ExceptionResult<int>()
            {
                ExceptionType    = typeof(TestException2).FullName,
                ExceptionMessage = "test"
            };

            try
            {
                er.ThrowOnError();
            }
            catch (TestException2 e)
            {
                Assert.Equal("test", e.Message);
            }

            // Message and inner exception constructor.  We don't actually
            // marshall the inner exception but we want to be able to rethrow
            // exception types that don't have a constuctor with only the
            // message parameter.

            er = new ExceptionResult<int>()
            {
                ExceptionType    = typeof(TestException3).FullName,
                ExceptionMessage = "test"
            };

            try
            {
                er.ThrowOnError();
            }
            catch (TestException3 e)
            {
                Assert.Equal("test", e.Message);
            }

            // Verify that we can rethrow an exception defined in another assembly.

            er = new ExceptionResult<int>()
            {
                ExceptionType    = typeof(InvalidOperationException).FullName,
                ExceptionMessage = "test"
            };

            try
            {
                er.ThrowOnError();
            }
            catch (InvalidOperationException e)
            {
                Assert.Equal("test", e.Message);
            }
        }

        [Fact]
        public void ExceptionResultT_CatchAll()
        {
            // Verify that a [CatchAllException] is thrown when a local exception
            // type matching the type name doesn't exist.

            // Default constructor

            var er = new ExceptionResult<int>()
            {
                ExceptionType    = "This.Exception.Not.Present",
                ExceptionMessage = "Hello"
            };

            try
            {
                er.ThrowOnError();
            }
            catch (CatchAllException e)
            {
                Assert.Equal("This.Exception.Not.Present", e.ExceptionType);
                Assert.Equal("Hello", e.Message);
            }
        }

        [Fact]
        public void ExceptionResultT_Serialize()
        {
            // Verify that we serialize an [ExceptionResult<T>].

            var erOut = new ExceptionResult<int>()
            {
                ExceptionType    = typeof(TestException2).FullName,
                ExceptionMessage = "test",
                Result           = 666
            };

            var json = NeonHelper.JsonSerialize(erOut);
            var erIn = NeonHelper.JsonDeserialize<ExceptionResult<int>>(json);

            Assert.Equal(erOut.ExceptionType, erIn.ExceptionType);
            Assert.Equal(erOut.ExceptionMessage, erIn.ExceptionMessage);
            Assert.Equal(erOut.Result, erIn.Result);
        }
    }
}
