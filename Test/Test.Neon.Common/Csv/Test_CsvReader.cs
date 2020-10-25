//-----------------------------------------------------------------------------
// FILE:        Test_CsvReader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
    public class Test_CsvReader
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void CsvReader_Basic()
        {
            string input =
@"0-0,1-0,2-0
0-1,1-1,2-1
0-2,1-2,2-2";
            using (CsvReader reader = new CsvReader(input))
            {
                Assert.Equal(new string[] { "0-0", "1-0", "2-0" }, reader.Read());
                Assert.Equal(new string[] { "0-1", "1-1", "2-1" }, reader.Read());
                Assert.Equal(new string[] { "0-2", "1-2", "2-2" }, reader.Read());
                Assert.Null(reader.Read());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void CsvReader_NoRows()
        {
            Assert.Null(new CsvReader("").Read());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void CsvReader_Ragged()
        {
            string input =
@"0-0,1-0,2-0
0-1,1-1
0-2,1-2,2-2
";
            using (CsvReader reader = new CsvReader(input))
            {
                Assert.Equal(new string[] { "0-0", "1-0", "2-0" }, reader.Read());
                Assert.Equal(new string[] { "0-1", "1-1" }, reader.Read());
                Assert.Equal(new string[] { "0-2", "1-2", "2-2" }, reader.Read());
                Assert.Null(reader.Read());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void CsvReader_EmptyFields()
        {
            string input =
@",1-0,2-0
0-1,,2-1
0-2,1-2,
";
            using (CsvReader reader = new CsvReader(input))
            {
                Assert.Equal(new string[] { "", "1-0", "2-0" }, reader.Read());
                Assert.Equal(new string[] { "0-1", "", "2-1" }, reader.Read());
                Assert.Equal(new string[] { "0-2", "1-2", "" }, reader.Read());
                Assert.Null(reader.Read());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void CsvReader_Quoted()
        {
            string input =
@"""Hello, """"World"""""",!,""Now""
Row,""Two""
";
            using (CsvReader reader = new CsvReader(input))
            {
                Assert.Equal(new string[] { "Hello, \"World\"", "!", "Now" }, reader.Read());
                Assert.Equal(new string[] { "Row", "Two" }, reader.Read());
                Assert.Null(reader.Read());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void CsvReader_QuotedMultiLine()
        {
            string input = "\"Hello\r\nWorld\",Col2\r\nRow,\"Two\"\r\n";

            using (CsvReader reader = new CsvReader(input))
            {
                Assert.Equal(new string[] { "Hello\r\nWorld", "Col2" }, reader.Read());
                Assert.Equal(new string[] { "Row", "Two" }, reader.Read());
                Assert.Null(reader.Read());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void CsvReader_LF_Terminated()
        {
            string input = "0-0,1-0,2-0\n0-1,1-1,2-1\n0-2,1-2,2-2";

            using (CsvReader reader = new CsvReader(input))
            {
                Assert.Equal(new string[] { "0-0", "1-0", "2-0" }, reader.Read());
                Assert.Equal(new string[] { "0-1", "1-1", "2-1" }, reader.Read());
                Assert.Equal(new string[] { "0-2", "1-2", "2-2" }, reader.Read());
                Assert.Null(reader.Read());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void CsvReader_QuotedMultiLine_LF_Terminated()
        {
            string input = "\"Hello\nWorld\",Col2\nRow,\"Two\"";

            using (CsvReader reader = new CsvReader(input))
            {
                Assert.Equal(new string[] { "Hello\nWorld", "Col2" }, reader.Read());
                Assert.Equal(new string[] { "Row", "Two" }, reader.Read());
                Assert.Null(reader.Read());
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void CsvTableReader_RowEnumeration()
        {
            CsvTableReader reader;
            string  table =
@"Col1,Col2,Col2
1,10
2,20
3,30
4,40
";
            reader = new CsvTableReader(new CsvReader(table));

            Assert.Equal(2, reader.ColumnMap.Count);

            var count = 0;

            foreach (var row in reader.Rows())
            {
                count++;

                Assert.Equal(count, int.Parse(row[0]));
                Assert.Equal(count * 10, int.Parse(row[1]));
            }

            Assert.Null(reader.ReadRow());
            Assert.Equal(4, count);
        }
    }
}

