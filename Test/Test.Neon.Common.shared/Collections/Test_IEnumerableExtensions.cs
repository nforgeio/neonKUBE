//-----------------------------------------------------------------------------
// FILE:	    Test_IEnumerableExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Owin;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Collections;
using Neon.Net;
using Neon.Retry;

using Xunit;

namespace TestCommon
{
    public class Test_IEnumerableExtensions
    {
        [Fact]
        public void SelectRandom_Single()
        {
            var items = new int[] { 0, 1, 2, 3 };

            // Perform a selection up to 1 million times to ensure that we're
            // actually getting different values (there's a million-to-one
            // chance that this test could report an invalid failure).

            var selected = items.SelectRandom();

            Assert.Single(selected);

            var item = selected.Single();

            for (int i = 0; i < 1000000; i++)
            {
                if (items.SelectRandom().Single() != item)
                {
                    return;
                }
            }

            Assert.True(false, "Random selection may not be working (there's a one-in-a-million chance that this is an invalid failure).");
        }

        [Fact]
        public void SelectRandom_Multiple()
        {
            var items    = new int[] { 0, 1, 2, 3 };
            var selected = items.SelectRandom(2);

            Assert.Equal(2, selected.Count());                              // Ensure that we got two items
            Assert.NotEqual(selected.First(), selected.Skip(1).First());    // Ensure that the items are different
        }

        [Fact]
        public void SelectRandom_Exceptions()
        {
            Assert.Throws<ArgumentNullException>(() => ((IEnumerable<int>)null).SelectRandom());

            var items = new int[] { 0, 1, 2, 3 };

            Assert.Throws<ArgumentException>(() => items.SelectRandom(-1));
            Assert.Throws<ArgumentException>(() => items.SelectRandom(0));
            Assert.Throws<ArgumentException>(() => items.SelectRandom(5));
        }
    }
}
