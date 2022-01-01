//-----------------------------------------------------------------------------
// FILE:        Test_CsvWriter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Text;

using Neon.Csv;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    [Trait(TestTrait.Category, TestArea.NeonCommon)]
    public class Test_CsvWriter
    {
        [Fact]
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

