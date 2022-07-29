using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Neon.Tailwind
{
    public partial class HeadlessDisclosureButton : HtmlBase
    {
        [CascadingParameter] public HeadlessDisclosure CascadedDisclosure { get; set; } = default!;

        [Parameter] public bool IsEnabled { get; set; } = true;

        protected HeadlessDisclosure Disclosure { get; set; } = default!;

        [Parameter] public string TagName { get; set; } = "button";

        /// <inheritdoc/>
        protected override void OnInitialized()
        {
            Disclosure.RegisterButton(this);
        }
        public async void HandleClick()
        {
            if (IsEnabled)
               await Disclosure.Toggle();
        }



    }
}
