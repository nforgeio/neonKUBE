using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Neon.Tailwind
{
    public partial class HeadlessDisclosureButton : HtmlBase, IDisposable
    {
        [CascadingParameter] public HeadlessDisclosure CascadedDisclosure { get; set; } = default!;

        [Parameter] public bool IsEnabled { get; set; } = true;

        protected HeadlessDisclosure Disclosure { get; set; } = default!;

        [Parameter] public string TagName { get; set; } = "button";


        /// <inheritdoc/>

        protected override async Task OnInitializedAsync()
        {
            await Disclosure.RegisterButton(this);
        }
        public async void HandleClick()
        {
            if (IsEnabled)
               await Disclosure.Toggle();
        }
        /// <inheritdoc/>
        public void Dispose()
        {
            _ = Disclosure.UnregisterButton(this);
        }

        /// <inheritdoc/>
        public override Task SetParametersAsync(ParameterView parameters)
        {
            //This is here to follow the pattern/example as implmented in Microsoft's InputBase component
            //https://github.com/dotnet/aspnetcore/blob/main/src/Components/Web/src/Forms/InputBase.cs

            parameters.SetParameterProperties(this);

            if (Disclosure == null)
            {
                if (CascadedDisclosure == null)
                    throw new InvalidOperationException($"You must use {nameof(HeadlessDisclosureButton)} inside an {nameof(HeadlessDisclosure)}.");

                Disclosure = CascadedDisclosure;
            }
            else if (CascadedDisclosure != Disclosure)
            {
                throw new InvalidOperationException($"{nameof(HeadlessDisclosure)} does not support changing the {nameof(HeadlessDisclosureButton)} dynamically.");
            }

            return base.SetParametersAsync(ParameterView.Empty);
        }

    }
}
