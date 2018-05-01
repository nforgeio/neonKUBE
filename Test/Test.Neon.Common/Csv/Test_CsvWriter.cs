//-----------------------------------------------------------------------------
// FILE:        Test_CsvWriter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Neon.Csv;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_CsvWriter
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void CsvWriter_Basic()
        {
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            CsvWriter writer = new CsvWriter(sw);

            writer.WriteLine(10, 20, 11.5, "Hello", "Hello,World", "Hello \"Cruel\" World", null);
            writer.WriteLine("End");
            writer.Close();

            Assert.Equal(
@"10,20,11.5,Hello,""Hello,World"",""Hello """"Cruel"""" World"",
End
", sb.ToString());
        }
    }
}

