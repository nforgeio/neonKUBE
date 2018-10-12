//-----------------------------------------------------------------------------
// FILE:	    Test_HiveBus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.HiveMQ;
using Neon.Xunit;
using Neon.Xunit.RabbitMQ;

using Xunit;

namespace TestCommon
{
    public partial class Test_HiveBus
    {
        //---------------------------------------------------------------------
        // Test message types:

        public class TestMessage1
        {
            public string Text { get; set; }
        }

        public class TestMessage2
        {
            public string Text { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        private readonly TimeSpan   timeout = TimeSpan.FromSeconds(15);
        private RabbitMQFixture     fixture;

        public Test_HiveBus(RabbitMQFixture fixture)
        {
            this.fixture = fixture;

            fixture.Start();
            fixture.Clear();
        }
    }
}
