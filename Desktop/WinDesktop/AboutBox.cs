//-----------------------------------------------------------------------------
// FILE:	    AboutBox.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Neon;
using Neon.Common;
using Neon.Kube;

namespace WinDesktop
{
    /// <summary>
    /// Displays information about neonKUBE.
    /// </summary>
    partial class AboutBox : Form
    {
        public AboutBox()
        {
            InitializeComponent();

            this.productNameLabel.Text = Build.ProductName;
            this.versionLabel.Text     = Build.ProductVersion;
            this.copyrightLabel.Text   = Build.Copyright;
            this.licenseLink.Text      = "Apache License, Version 2.0";
            this.descriptionBox.Text   = "Kubernetes the easy way!";
        }

        private void licenseLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            NeonHelper.OpenBrowser("http://www.apache.org/licenses/LICENSE-2.0");
        }
    }
}
