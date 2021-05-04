//-----------------------------------------------------------------------------
// FILE:        Test_CsvTableWriter.cs
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
using Neon.IO;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    [Trait(TestTrait.Area, TestArea.NeonCommon)]
    public class Test_CsvTableWriter
    {
        [Fact]
        public void CsvTableWriter_Basic()
        {
            using (var tempFolder = new TempFolder())
            {
                string path = Path.Combine(tempFolder.Path, "test.csv");

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
        }

        [Fact]
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

