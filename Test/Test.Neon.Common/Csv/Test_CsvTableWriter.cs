//-----------------------------------------------------------------------------
// FILE:        Test_CsvTableWriter.cs
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
    public class Test_CsvTableWriter
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void CsvTableWriter_Basic()
        {
            string path = Path.GetTempFileName();

            try
            {
                using (var writer = new CsvTableWriter(new string[] { "Col0", "Col1", "Col2" }, new FileStream(path, FileMode.Create), Encoding.UTF8))
                {
                    Assert.Equal(0, writer.GetColumnIndex("Col0"));
                    Assert.Equal(1, writer.GetColumnIndex("Col1"));
                    Assert.Equal(2, writer.GetColumnIndex("Col2"));
                    Assert.Equal(-1, writer.GetColumnIndex("Col3"));

                    writer.Set("Col0", "(0,0)");
                    writer.Set("Col1", "(1,0)");
                    writer.Set("Col2", "(2,0)");
                    writer.WriteRow();

                    writer.Set("Col0", "(0,1)");
                    writer.Set("Col1", "(1,1)");
                    writer.Set("Col2", "(2,1)");
                    writer.WriteRow();
                }

                using (var reader = new CsvTableReader(new FileStream(path, FileMode.Open), Encoding.UTF8))
                {
                    Assert.True(reader.ColumnMap.ContainsKey("Col0"));
                    Assert.True(reader.ColumnMap.ContainsKey("Col1"));
                    Assert.True(reader.ColumnMap.ContainsKey("Col2"));

                    Assert.NotNull(reader.ReadRow());
                    Assert.Equal("(0,0)", reader["Col0"]);
                    Assert.Equal("(1,0)", reader["Col1"]);
                    Assert.Equal("(2,0)", reader["Col2"]);

                    Assert.NotNull(reader.ReadRow());
                    Assert.Equal("(0,1)", reader["Col0"]);
                    Assert.Equal("(1,1)", reader["Col1"]);
                    Assert.Equal("(2,1)", reader["Col2"]);

                    Assert.Null(reader.ReadRow());
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void CsvTableWriter_NullColumns()
        {
            string path = Path.GetTempFileName();

            try
            {
                using (var writer = new CsvTableWriter(new string[] { "Col0", "Col1", "Col2" }, new FileStream(path, FileMode.Create), Encoding.UTF8))
                {
                    writer.Set("Col0", "(0,0)");
                    writer.Set("Col2", "(2,0)");
                    writer.WriteRow();

                    writer.Set("Col0", "(0,1)");
                    writer.Set("Col2", "(2,1)");
                    writer.WriteRow();
                }

                using (var reader = new CsvTableReader(new FileStream(path, FileMode.Open), Encoding.UTF8))
                {
                    Assert.True(reader.ColumnMap.ContainsKey("Col0"));
                    Assert.True(reader.ColumnMap.ContainsKey("Col1"));
                    Assert.True(reader.ColumnMap.ContainsKey("Col2"));

                    Assert.NotNull(reader.ReadRow());
                    Assert.Equal("(0,0)", reader["Col0"]);
                    Assert.Equal("", reader["Col1"]);
                    Assert.Equal("(2,0)", reader["Col2"]);

                    Assert.NotNull(reader.ReadRow());
                    Assert.Equal("(0,1)", reader["Col0"]);
                    Assert.Equal("", reader["Col1"]);
                    Assert.Equal("(2,1)", reader["Col2"]);

                    Assert.Null(reader.ReadRow());
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void CsvTableWriter_MissingColumns()
        {
            string path = Path.GetTempFileName();

            try
            {
                using (var writer = new CsvTableWriter(new string[] { "Col0", "Col1", "Col2" }, new FileStream(path, FileMode.Create), Encoding.UTF8))
                {
                    writer.Set("Col0", "(0,0)");
                    writer.Set("Col1", "(1,0)");
                    writer.Set("Col2", "(2,0)");
                    writer.Set("XXXX", "YYYY");
                    writer.WriteRow();

                    writer.Set("Col0", "(0,1)");
                    writer.Set("Col1", "(1,1)");
                    writer.Set("Col2", "(2,1)");
                    writer.Set("XXXX", "YYYY");
                    writer.WriteRow();
                }

                using (var reader = new CsvTableReader(new FileStream(path, FileMode.Open), Encoding.UTF8))
                {
                    Assert.True(reader.ColumnMap.ContainsKey("Col0"));
                    Assert.True(reader.ColumnMap.ContainsKey("Col1"));
                    Assert.True(reader.ColumnMap.ContainsKey("Col2"));

                    Assert.NotNull(reader.ReadRow());
                    Assert.Equal("(0,0)", reader["Col0"]);
                    Assert.Equal("(1,0)", reader["Col1"]);
                    Assert.Equal("(2,0)", reader["Col2"]);

                    Assert.NotNull(reader.ReadRow());
                    Assert.Equal("(0,1)", reader["Col0"]);
                    Assert.Equal("(1,1)", reader["Col1"]);
                    Assert.Equal("(2,1)", reader["Col2"]);

                    Assert.Null(reader.ReadRow());
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void CsvTableWriter_BlankRow()
        {
            string path = Path.GetTempFileName();

            try
            {
                using (var writer = new CsvTableWriter(new string[] { "Col0", "Col1", "Col2" }, new FileStream(path, FileMode.Create), Encoding.UTF8))
                {
                    writer.Set("Col0", "(0,0)");
                    writer.Set("Col1", "(1,0)");
                    writer.Set("Col2", "(2,0)");
                    writer.WriteRow();

                    writer.WriteRow();

                    writer.Set("Col0", "(0,2)");
                    writer.Set("Col1", "(1,2)");
                    writer.Set("Col2", "(2,2)");
                    writer.WriteRow();
                }

                using (var reader = new CsvTableReader(new FileStream(path, FileMode.Open), Encoding.UTF8))
                {
                    Assert.True(reader.ColumnMap.ContainsKey("Col0"));
                    Assert.True(reader.ColumnMap.ContainsKey("Col1"));
                    Assert.True(reader.ColumnMap.ContainsKey("Col2"));

                    Assert.NotNull(reader.ReadRow());
                    Assert.Equal("(0,0)", reader["Col0"]);
                    Assert.Equal("(1,0)", reader["Col1"]);
                    Assert.Equal("(2,0)", reader["Col2"]);

                    Assert.NotNull(reader.ReadRow());
                    Assert.Equal("", reader["Col0"]);
                    Assert.Equal("", reader["Col1"]);
                    Assert.Equal("", reader["Col2"]);

                    Assert.NotNull(reader.ReadRow());
                    Assert.Equal("(0,2)", reader["Col0"]);
                    Assert.Equal("(1,2)", reader["Col1"]);
                    Assert.Equal("(2,2)", reader["Col2"]);

                    Assert.Null(reader.ReadRow());
                }
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}

