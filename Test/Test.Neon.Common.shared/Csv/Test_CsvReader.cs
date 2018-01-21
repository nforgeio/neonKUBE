//-----------------------------------------------------------------------------
// FILE:        Test_CsvReader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Neon.Csv;

using Xunit;

namespace LillTek.Common.Test
{
    public class Test_CsvReader
    {
        [Fact]
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
        public void CsvReader_NoRows()
        {
            Assert.Null(new CsvReader("").Read());
        }

        [Fact]
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
    }
}

