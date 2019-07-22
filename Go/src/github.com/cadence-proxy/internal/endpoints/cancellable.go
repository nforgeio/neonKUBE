//-----------------------------------------------------------------------------
// FILE:		cancellable.go
// CONTRIBUTOR: John C Burns
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

package endpoints

import (
	"context"
	"sync"
)

type (
	cancellablesMap struct {
		safeMap sync.Map
	}
)

//----------------------------------------------------------------------------
// cancellablesMap instance methods

// Add adds a new Cancellable and its corresponding requestID into
// the cancellablesMap.  This method is thread-safe.
//
// param requestID int64 -> the requestID of the request sent to
// the Neon.Cadence lib client.  This will be the mapped key
//
// param value context.CancelFunc -> cancel function to be added to the
// map. Thiss will be the mapped value
//
// returns int64 -> requestID of the request being added
// in the CancellablesMap
func (canc *cancellablesMap) Add(requestID int64, value context.CancelFunc) int64 {
	canc.safeMap.Store(requestID, value)
	return requestID
}

// Remove removes key/value entry from the cancellablesMap at the specified
// requestID.  This is a thread-safe method.
//
// param requestID int64 -> the requestID of the request sent to
// the Neon.Cadence lib client.  This will be the mapped key
//
// returns int64 -> requestID of the request being removed in the
// Cancellables map
func (canc *cancellablesMap) Remove(requestID int64) int64 {
	canc.safeMap.Delete(requestID)
	return requestID
}

// Get gets a Cancellable from the cancellablesMap at the specified
// requestID.  This method is thread-safe.
//
// param requestID int64 -> the requestID of the request sent to
// the Neon.Cadence lib client.  This will be the mapped key
//
// returns context.CancelFunc -> cancel function to be gotten from the
// map at the specified requestID
func (canc *cancellablesMap) Get(requestID int64) context.CancelFunc {
	if v, ok := canc.safeMap.Load(requestID); ok {
		if _v, _ok := v.(context.CancelFunc); _ok {
			return _v
		}
	}

	return nil
}
