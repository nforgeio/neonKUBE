//-----------------------------------------------------------------------------
// FILE:	    clickoffhandler.js
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:  	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

export function makeHandler(component, handlerMethodName) {
    return new ClickOffHandler(component, handlerMethodName);
}

class ClickOffHandler {
    constructor(component, handlerMethodName) {
        this.component = component;
        this.handlerMethodName = handlerMethodName;
        this.mount();
    }

    debugEnabled = false;

    elements = [];

    registerElement = elementRef => {
        if (elementRef === null) return;
        for (let i = 0; i < this.elements.length; i++) {
            if (this.elements[i] == elementRef) {
                return;
            }
        }
        this.logDebugMessage("Click off handler: Registering element", elementRef);
        this.elements.push(elementRef);
    }

    unregisterElement = elementRef => {
        if (elementRef === null) return;
        for (let i = 0; i < this.elements.length; i++) {
            if (this.elements[i] == elementRef) {
                this.unregisterElementByIndex(i);
                return;
            }
        }
    }

    unregisterElementByIndex = index => {
        this.logDebugMessage("Click off handler: Unregistering element", this.elements[index]);
        this.elements.splice(index, 1);
        this.logDebugMessage("Elements After:", this.elements);
        return;
    }

    handleWindowOnClick = e => {
        if (this.elements.length == 0) return;
        this.logDebugMessage("Target: ", e.target);
        let isClickOff = true;
        for (let i = this.elements.length - 1; i >= 0; i--) {
            this.logDebugMessage(this.elements[i]);
            if (this.elements[i].contains(event.target)) {
                this.logDebugMessage("FOUND");
                isClickOff = false;
            }
            if (!document.contains(this.elements[i])) {
                this.unregisterElementByIndex(i);
            }
        }
        if (isClickOff) this.onClickOff();
    }

    onClickOff = () => {
        this.logDebugMessage("Click off handler: clicked off", this.elements);
        this.component.invokeMethodAsync(this.handlerMethodName);
    }

    mount = () => {
        this.logDebugMessage("Mounting click off handler");
        window.addEventListener('click', this.handleWindowOnClick);
    }

    unmount = () => {
        this.logDebugMessage("Unmounting click off handler");
        window.removeEventListener('click', this.handleWindowOnClick);
        for (let i = this.elements.length - 1; i >= 0; i--) {
            this.unregisterElementByIndex(i);
        }
    }

    logDebugMessage = (message, ...data) => {
        if (this.debugEnabled)
            if (data.length > 0)
                console.log(message, data)
            else
                console.log(message)
    }
}
