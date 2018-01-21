//-----------------------------------------------------------------------------
// FILE:	    TestDatabase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase;
using Couchbase.Lite;
using Couchbase.Lite.Auth;

using Neon.Common;

using Xunit;

namespace TestLiteExtensions
{
    /// <summary>
    /// Creates a temporary test database.
    /// </summary>
    public sealed class TestDatabase : IDisposable
    {
        private bool        isDisposed = false;
        private string      folder;
        private Manager     manager;

        public TestDatabase()
        {
            folder  = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            manager = new Manager(new DirectoryInfo(folder), new ManagerOptions())
            {
                // ForestDB works only for x64 builds.

                StorageType = NeonHelper.Is64Bit ? StorageEngineTypes.ForestDB : StorageEngineTypes.SQLite
            };

            Database = manager.GetEntityDatabase("test");
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            Database.Dispose();
            Database = null;

            manager.Close();
            manager = null;

            isDisposed = false;

            Directory.Delete(folder, recursive: true);
        }

        public EntityDatabase Database { get; private set; }
    }
}
