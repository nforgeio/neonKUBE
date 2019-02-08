//-----------------------------------------------------------------------------
// FILE:	    MainForm.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Drawing;
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
    /// The main application form.  Note that this form will always be hidden.
    /// </summary>
    public partial class MainForm : Form
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public MainForm()
        {
            InitializeComponent();

            Load += MainForm_Load;
        }

        /// <summary>
        /// Handles form initialization.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void MainForm_Load(object sender, EventArgs args)
        {
            // Set the text labels on the main form.  Nobody should ever see
            // this because the form should remain hidden but we'll put something
            // here just in case.

            productNameLabel.Text = $"{Build.ProductName}  (v{Build.ProductVersion})";
            copyrightLabel.Text   = Build.Copyright;
            licenseLinkLabel.Text = Build.ProductLicense;

            // Configure the notify icon.

            notifyIcon.Text    = Build.ProductName;
            notifyIcon.Visible = true;
        }

        /// <summary>
        /// Handles license link clicks.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void licenseLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs args)
        {
            NeonHelper.OpenBrowser(Build.ProductLicenseUrl);
        }

        /// <summary>
        /// Intercept the window close event an minimize it instead.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs args)
        {
            // The main form should always be hidden but we'll 
            // implement this just in case.

            args.Cancel = true;
        }
    }
}
