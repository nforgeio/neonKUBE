//-----------------------------------------------------------------------------
// FILE:	    Test_ReflectionExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Retry;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    internal interface IFoo
    {
        void Test();
    }

    internal interface IFooFoo : IFoo
    {
    }

    internal class Foo : IFoo
    {
        public void Test()
        {
        }
    }

    internal class FooFoo : IFooFoo
    {
        public void Test()
        {
        }
    }

    internal class FooExtended : Foo
    {
    }

    internal class NotFoo
    {
        public void Test()
        {
        }
    }

    internal class MethodTest
    {
        public void Test(string p1, int p2, double p3)
        {
        }
    }

    internal class Bar
    {
    }

    internal class BarExtended : Bar
    {
    }

    internal class BarBarExtended : BarExtended
    {
    }

    public class Test_ReflectionExtensions
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Type_Implements()
        {
            var fooType         = typeof(Foo);
            var foofooType      = typeof(FooFoo);
            var fooExtendedType = typeof(FooExtended);
            var notFooType      = typeof(NotFoo);

            Assert.True(fooType.Implements<IFoo>());
            Assert.True(foofooType.Implements<IFoo>());
            Assert.True(fooExtendedType.Implements<IFoo>());

            Assert.False(notFooType.Implements<IFoo>());

            Assert.Throws<ArgumentNullException>(() => ((Type)null).Implements<IFoo>());
            Assert.Throws<ArgumentException>(() => fooType.Implements<NotFoo>());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Type_Is()
        {
            Assert.True(typeof(Bar).Is<object>());
            Assert.True(typeof(Bar).Is<Bar>());
            Assert.False(typeof(Bar).Is<int>());

            Assert.True(typeof(BarExtended).Is<object>());
            Assert.True(typeof(BarExtended).Is<Bar>());
            Assert.True(typeof(BarExtended).Is<BarExtended>());
            Assert.False(typeof(BarExtended).Is<int>());

            Assert.True(typeof(BarBarExtended).Is<object>());
            Assert.True(typeof(BarBarExtended).Is<Bar>());
            Assert.True(typeof(BarBarExtended).Is<BarExtended>());
            Assert.True(typeof(BarBarExtended).Is<BarBarExtended>());
            Assert.False(typeof(BarBarExtended).Is<int>());

            Assert.True(typeof(Bar).Is(typeof(object)));
            Assert.True(typeof(Bar).Is(typeof(Bar)));
            Assert.False(typeof(Bar).Is(typeof(int)));

            Assert.True(typeof(BarExtended).Is(typeof(object)));
            Assert.True(typeof(BarExtended).Is(typeof(Bar)));
            Assert.True(typeof(BarExtended).Is(typeof(BarExtended)));
            Assert.False(typeof(BarExtended).Is(typeof(int)));

            Assert.True(typeof(BarBarExtended).Is(typeof(object)));
            Assert.True(typeof(BarBarExtended).Is(typeof(Bar)));
            Assert.True(typeof(BarBarExtended).Is(typeof(BarExtended)));
            Assert.True(typeof(BarBarExtended).Is(typeof(BarBarExtended)));
            Assert.False(typeof(BarBarExtended).Is(typeof(int)));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Method_GetParameterTypes()
        {
            var type       = typeof(MethodTest);
            var method     = type.GetMethod("Test");
            var paramTypes = method.GetParameterTypes();

            Assert.Equal(new Type[] { typeof(string), typeof(int), typeof(double) }, paramTypes);
        }
    }
}
