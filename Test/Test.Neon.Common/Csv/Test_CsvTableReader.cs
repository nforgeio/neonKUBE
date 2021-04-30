//-----------------------------------------------------------------------------
// FILE:        Test_CsvTableReader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
    public class Test_CsvTableReader
    {
        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public void CsvTableReader_EmptyTable()
        {
            CsvTableReader reader;
            string table = "";

            reader = new CsvTableReader(new CsvReader(table));
            Assert.Empty(reader.ColumnMap);
            Assert.Null(reader.ReadRow());
            Assert.Null(reader.ReadRow());
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public void CsvTableReader_NoRows()
        {
            CsvTableReader reader;
            string table = "Col1,Col2,Col3";

            reader = new CsvTableReader(new CsvReader(table));
            Assert.Equal(3, reader.ColumnMap.Count);
            Assert.Equal(0, reader.ColumnMap["Col1"]);
            Assert.Equal(1, reader.ColumnMap["Col2"]);
            Assert.Equal(2, reader.ColumnMap["Col3"]);
            Assert.Null(reader.ReadRow());
            Assert.Null(reader.ReadRow());
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public void CsvTableReader_Parsing()
        {
            CsvTableReader reader;
            string table =
@"Col1,Col2,Col3
10,true,25.20
no,10
""Hello """"World""""!"",BAR";

            reader = new CsvTableReader(new CsvReader(table));
            Assert.Equal(3, reader.ColumnMap.Count);
            Assert.Equal(0, reader.ColumnMap["Col1"]);
            Assert.Equal(1, reader.ColumnMap["Col2"]);
            Assert.Equal(2, reader.ColumnMap["Col3"]);

            Assert.Equal(3, reader.Columns.Count);
            Assert.Equal("Col1", reader.Columns[0]);
            Assert.Equal("Col2", reader.Columns[1]);
            Assert.Equal("Col3", reader.Columns[2]);

            Assert.NotNull(reader.ReadRow());
            Assert.Equal("10", reader.GetColumn("Col1"));
            Assert.Equal("true", reader.GetColumn("Col2"));
            Assert.Equal("25.20", reader.GetColumn("Col3"));

            Assert.Equal("10", reader["Col1"]);
            Assert.Equal("true", reader["Col2"]);
            Assert.Equal("25.20", reader["Col3"]);
            Assert.Null(reader["not-there"]);

            Assert.Equal("10", reader[0]);
            Assert.Equal("true", reader[1]);
            Assert.Equal("25.20", reader[2]);
            Assert.Null(reader[100]);

            Assert.NotNull(reader.ReadRow());
            Assert.Equal("no", reader["Col1"]);
            Assert.Equal("10", reader["Col2"]);

            Assert.NotNull(reader.ReadRow());
            Assert.Equal(@"Hello ""World""!", reader["Col1"]);
            Assert.Equal("BAR", reader["Col2"]);

            Assert.Null(reader.ReadRow());
            Assert.Null(reader.ReadRow());
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public void CsvTableReader_DuplicateColumns()
        {
            CsvTableReader reader;
            string table =
@"Col1,Col2,Col2
10,true,25.20
no,10";

            reader = new CsvTableReader(new CsvReader(table));
            Assert.Equal(2, reader.ColumnMap.Count);
            Assert.Equal(0, reader.ColumnMap["Col1"]);
            Assert.Equal(1, reader.ColumnMap["Col2"]);

            Assert.NotNull(reader.ReadRow());
            Assert.Equal("10", reader["Col1"]);
            Assert.Equal("true", reader["Col2"]);

            Assert.NotNull(reader.ReadRow());
            Assert.Equal("no", reader["Col1"]);
            Assert.Equal("10", reader["Col2"]);

            Assert.Null(reader.ReadRow());
            Assert.Null(reader.ReadRow());
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public void CsvTableReader_RowEnumeration()
        {
            CsvTableReader reader;
            string table =
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

