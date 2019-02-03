//-----------------------------------------------------------------------------
// FILE:	    KubeTestManager.cs
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

namespace Neon.Xunit.Kube
{
    /// <summary>
    /// Creates a temporary folder and puts <see cref="KubeHelper"/> into test mode
    /// to support <b>neon-cli</b> unit testing.  <see cref="Dispose"/> reverts the 
    /// test mode and deletes the temporary folder.
    /// </summary>
    public sealed class KubeTestManager : IDisposable
    {
        private TempFolder tempFolder;

        /// <summary>
        /// Constructor.
        /// </summary>
        public KubeTestManager()
        {
            tempFolder = new TempFolder();

            KubeHelper.SetTestMode(tempFolder.Path);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (tempFolder != null)
            {
                KubeHelper.ClearTestMode();
                tempFolder.Dispose();

                tempFolder = null;
            }
        }
    }
}
