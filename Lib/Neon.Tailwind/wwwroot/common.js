//-----------------------------------------------------------------------------
// FILE:	    common.js
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

export function preventDefaultKeyBehaviorOnKeys(element, arrayOfKeyStrings, remove = false) {    
    var preventDefaultOnKeysFunction = function (e) {        
        if (arrayOfKeyStrings.includes(e.key)) {
            e.preventDefault();
            return false;
        }
    }
    if (remove) {
        element.removeEventListener('keydown', preventDefaultOnKeysFunction, false);
        element.addEventListener('keyup', preventDefaultOnKeysFunction, false);
    }
    else {
        element.addEventListener('keydown', preventDefaultOnKeysFunction, false);
        element.addEventListener('keyup', preventDefaultOnKeysFunction, false);
    }
}