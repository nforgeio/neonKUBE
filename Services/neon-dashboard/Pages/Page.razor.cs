using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;

using Neon.Tailwind.HeadlessUI;

namespace NeonDashboard.Pages
{
    public partial class Page : PageBase
    {
        private HeadlessDialog headlessDialog;
        private bool showModal = true;

        public Page()
        {
        }
    }
}
