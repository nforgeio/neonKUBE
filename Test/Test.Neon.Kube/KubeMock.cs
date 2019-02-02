//-----------------------------------------------------------------------------
// FILE:	    TestHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Kube;

using Xunit;

namespace TestKube
{
    /// <summary>
    /// Creates a temporary folder and puts <see cref="KubeHelper"/> into mock mode.
    /// <see cref="Dispose"/> revers mock mode and deletes the folder.
    /// </summary>
    public sealed class KubeMock : IDisposable
    {
        private TempFolder tempFolder;

        /// <summary>
        /// Constructor.
        /// </summary>
        public KubeMock()
        {
            tempFolder = new TempFolder();

            KubeHelper.SetMockMode(tempFolder.Path);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (tempFolder != null)
            {
                KubeHelper.ClearMockMode();
                tempFolder.Dispose();

                tempFolder = null;
            }
        }
    }
}
