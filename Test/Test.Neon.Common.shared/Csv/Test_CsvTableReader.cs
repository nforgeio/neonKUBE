//-----------------------------------------------------------------------------
// FILE:        Test_CsvTableReader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Neon.Csv;

using Xunit;
using Xunit.Neon;

namespace LillTek.Common.Test
{
    public class Test_CsvTableReader
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
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
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
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
    }
}

