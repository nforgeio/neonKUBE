//-----------------------------------------------------------------------------
// FILE:	    Test_Type.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

    internal class Foo : IFoo
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

    public class Test_Type
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Implements()
        {
            var fooType         = typeof(Foo);
            var fooExtendedType = typeof(FooExtended);
            var notFooType      = typeof(NotFoo);

            Assert.True(fooType.Implements<IFoo>());
            Assert.True(fooExtendedType.Implements<IFoo>());

            Assert.False(notFooType.Implements<IFoo>());

            Assert.Throws<ArgumentNullException>(() => ((Type)null).Implements<IFoo>());
            Assert.Throws<ArgumentException>(() => fooType.Implements<NotFoo>());
        }
    }
}
