//-----------------------------------------------------------------------------
// FILE:	    Test_Exception.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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
    public class Test_Exception
    {
        [Fact]
        public void TriggeredBy()
        {
            Assert.False(((Exception)null).TriggeredBy<Exception>());

            Assert.True(new KeyNotFoundException().TriggeredBy<KeyNotFoundException>());
            Assert.False(new KeyNotFoundException().TriggeredBy<IndexOutOfRangeException>());

            Assert.True(new IndexOutOfRangeException("message", new KeyNotFoundException("key")).TriggeredBy<KeyNotFoundException>());
            Assert.True(new IndexOutOfRangeException("message", new IndexOutOfRangeException("message", new KeyNotFoundException("key"))).TriggeredBy<KeyNotFoundException>());
            Assert.False(new IndexOutOfRangeException("message", new KeyNotFoundException("key")).TriggeredBy<FormatException>());

            Assert.False(new AggregateException().TriggeredBy<KeyNotFoundException>());
            Assert.True(new AggregateException(new KeyNotFoundException("key")).TriggeredBy<KeyNotFoundException>());
            Assert.True(new AggregateException(new IndexOutOfRangeException("message", new KeyNotFoundException("key"))).TriggeredBy<KeyNotFoundException>());
        }

        [Fact]
        public void GetTrigger()
        {
            Assert.Null (((Exception)null).GetTrigger<Exception>());

            Assert.NotNull(new KeyNotFoundException().GetTrigger<KeyNotFoundException>());
            Assert.Null(new KeyNotFoundException().GetTrigger<IndexOutOfRangeException>());

            Assert.Equal("key", new IndexOutOfRangeException("message", new KeyNotFoundException("key")).GetTrigger<KeyNotFoundException>().Message);
            Assert.Equal("key", new IndexOutOfRangeException("message", new IndexOutOfRangeException("message", new KeyNotFoundException("key"))).GetTrigger<KeyNotFoundException>().Message);
            Assert.Null(new IndexOutOfRangeException("message", new KeyNotFoundException("key")).GetTrigger<FormatException>());

            Assert.Null(new AggregateException().GetTrigger<KeyNotFoundException>());
            Assert.Equal("key", new AggregateException(new KeyNotFoundException("key")).GetTrigger<KeyNotFoundException>().Message);
            Assert.Equal("key", new AggregateException(new IndexOutOfRangeException("message", new KeyNotFoundException("key"))).GetTrigger<KeyNotFoundException>().Message);
        }
    }
}
