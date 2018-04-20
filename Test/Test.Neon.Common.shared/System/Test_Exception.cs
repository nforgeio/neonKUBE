//-----------------------------------------------------------------------------
// FILE:	    Test_Exception.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Retry;

using Xunit;

namespace TestCommon
{
    public class Test_Exception : IClassFixture<ResetFixture>
    {
        [Fact]
        public void Contains()
        {
            Assert.False(((Exception)null).Contains<Exception>());

            Assert.True(new KeyNotFoundException().Contains<KeyNotFoundException>());
            Assert.False(new KeyNotFoundException().Contains<IndexOutOfRangeException>());

            Assert.True(new IndexOutOfRangeException("message", new KeyNotFoundException("key")).Contains<KeyNotFoundException>());
            Assert.True(new IndexOutOfRangeException("message", new IndexOutOfRangeException("message", new KeyNotFoundException("key"))).Contains<KeyNotFoundException>());
            Assert.False(new IndexOutOfRangeException("message", new KeyNotFoundException("key")).Contains<FormatException>());

            Assert.False(new AggregateException().Contains<KeyNotFoundException>());
            Assert.True(new AggregateException(new KeyNotFoundException("key")).Contains<KeyNotFoundException>());
            Assert.True(new AggregateException(new IndexOutOfRangeException("message", new KeyNotFoundException("key"))).Contains<KeyNotFoundException>());
        }

        [Fact]
        public void Find()
        {
            Assert.Null (((Exception)null).Find<Exception>());

            Assert.NotNull(new KeyNotFoundException().Find<KeyNotFoundException>());
            Assert.Null(new KeyNotFoundException().Find<IndexOutOfRangeException>());

            Assert.Equal("key", new IndexOutOfRangeException("message", new KeyNotFoundException("key")).Find<KeyNotFoundException>().Message);
            Assert.Equal("key", new IndexOutOfRangeException("message", new IndexOutOfRangeException("message", new KeyNotFoundException("key"))).Find<KeyNotFoundException>().Message);
            Assert.Null(new IndexOutOfRangeException("message", new KeyNotFoundException("key")).Find<FormatException>());

            Assert.Null(new AggregateException().Find<KeyNotFoundException>());
            Assert.Equal("key", new AggregateException(new KeyNotFoundException("key")).Find<KeyNotFoundException>().Message);
            Assert.Equal("key", new AggregateException(new IndexOutOfRangeException("message", new KeyNotFoundException("key"))).Find<KeyNotFoundException>().Message);
        }
    }
}
