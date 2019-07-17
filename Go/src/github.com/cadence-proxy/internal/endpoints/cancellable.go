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

	// Cancellable is used to track every cancellable
	// request from the Neon.Cadence client
	Cancellable struct {
		cancelFunc func()
		ctx        context.Context
	}
)

// NewCancellable is the default constructor for an Cancellable
func NewCancellable(ctx context.Context, cancel func()) *Cancellable {
	c := new(Cancellable)
	c.cancelFunc = cancel
	c.ctx = ctx

	return c
}

//----------------------------------------------------------------------------
// Cancellable instance methods

// GetContext gets a Cancellable's context.Context
//
// returns context.Context -> a cadence context context
func (c *Cancellable) GetContext() context.Context {
	return c.ctx
}

// SetContext sets a Cancellable's context.Context
//
// param value context.Context -> a context to be
// set as a Cancellable's cadence context.Context
func (c *Cancellable) SetContext(value context.Context) {
	c.ctx = value
}

// GetCancelFunction gets a Cancellable's cancel function
//
// returns func() -> a golang cancel function
func (c *Cancellable) GetCancelFunction() func() {
	return c.cancelFunc
}

// SetCancelFunction sets a Cancellable's cancel function
//
// param value func() -> a golang cancel function
func (c *Cancellable) SetCancelFunction(value func()) {
	c.cancelFunc = value
}

//----------------------------------------------------------------------------
// cancellablesMap instance methods

// Add adds a new Cancellable and its corresponding requestID into
// the cancellablesMap.  This method is thread-safe.
//
// param requestID int64 -> the requestID of the request sent to
// the Neon.Cadence lib client.  This will be the mapped key
//
// param value *Cancellable -> pointer to Cancellable to be set in the map.
// This will be the mapped value
//
// returns int64 -> requestID of the request being added
// in the Cancellable at the specified requestID
func (canc *cancellablesMap) Add(requestID int64, value *Cancellable) int64 {
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
// Cancellable at the specified requestID
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
// returns *Cancellable -> pointer to Cancellable at the specified requestID
// in the map.
func (canc *cancellablesMap) Get(requestID int64) *Cancellable {
	if v, ok := canc.safeMap.Load(requestID); ok {
		if _v, _ok := v.(*Cancellable); _ok {
			return _v
		}
	}

	return nil
}
