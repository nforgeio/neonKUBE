//-----------------------------------------------------------------------------
// FILE:		operation.go
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

package messages

import (
	"errors"
	"sync"

	"github.com/cadence-proxy/internal"
	proxyerror "github.com/cadence-proxy/internal/cadence/error"
)

var (
	mu sync.RWMutex

	// requestID is incremented (protected by a mutex) every time
	// a new request message is sent
	requestID int64
)

type (

	// OperationsMap maps an Operation to an int64
	// Id.
	OperationsMap struct {
		safeMap sync.Map
	}

	// Operation is used to track pending Neon.Cadence library calls
	Operation struct {
		requestID   int64
		contextID   int64
		request     IProxyRequest
		isCancelled bool
		channel     chan interface{}
	}
)

// NewOperation is the default constructor for an Operation
func NewOperation(requestID int64, request IProxyRequest) *Operation {
	op := new(Operation)
	op.isCancelled = false
	op.request = request
	op.requestID = requestID
	return op
}

//----------------------------------------------------------------------------
// RequestID thread-safe methods

// NextRequestID increments the package variable
// requestID by 1 and is protected by a mutex lock
func NextRequestID() int64 {
	mu.Lock()
	requestID = requestID + 1
	defer mu.Unlock()
	return requestID
}

// GetRequestID gets the value of the global variable
// requestID and is protected by a mutex Read lock
func GetRequestID() int64 {
	mu.RLock()
	defer mu.RUnlock()
	return requestID
}

//----------------------------------------------------------------------------
// Operation instance methods

// GetIsCancelled gets isCancelled
func (op *Operation) GetIsCancelled() bool {
	return op.isCancelled
}

// SetIsCancelled sets isCancelled
func (op *Operation) SetIsCancelled(value bool) {
	op.isCancelled = value
}

// GetRequestID gets the requestID
func (op *Operation) GetRequestID() int64 {
	return op.requestID
}

// SetRequestID sets the requestID
func (op *Operation) SetRequestID(value int64) {
	op.requestID = value
}

// GetContextID gets the contextID
func (op *Operation) GetContextID() int64 {
	return op.contextID
}

// SetContextID sets the contextID
func (op *Operation) SetContextID(value int64) {
	op.contextID = value
}

// GetRequest gets the request
func (op *Operation) GetRequest() IProxyRequest {
	return op.request
}

// SetRequest sets the request
func (op *Operation) SetRequest(value IProxyRequest) {
	op.request = value
}

// SetCancelled signals the awaiting task that the Operation has
// been canceled
func (op *Operation) SetCancelled() {
	op.isCancelled = true
}

// GetChannel gets the Operation channel
func (op *Operation) GetChannel() chan interface{} {
	return op.channel
}

// SetChannel sets the Operation channel
func (op *Operation) SetChannel(value chan interface{}) {
	op.channel = value
}

// SendChannel sends an interface{} value over the
// Operation's channel
func (op *Operation) SendChannel(result interface{}, cadenceError *proxyerror.CadenceError) error {
	if op.channel == nil {
		return internal.ErrArgumentNil
	}

	defer close(op.channel)
	if cadenceError != nil {
		op.channel <- errors.New(cadenceError.ToString())
	} else {
		op.channel <- result
	}

	return nil
}

//----------------------------------------------------------------------------
// OperationsMap instance methods

// Add adds a new Operation and its corresponding requestID into
// the OperationsMap.  This method is thread-safe.
//
// param requestID int64 -> the requestID of the request sent to
// the Neon.Cadence lib client.  This will be the mapped key
//
// param value *Operation -> pointer to Operation to be set in the map.
// This will be the mapped value
//
// returns int64 -> requestID of the request being added
// in the Operation at the specified requestID
func (opMap *OperationsMap) Add(requestID int64, value *Operation) int64 {
	opMap.safeMap.Store(requestID, value)
	return requestID
}

// Remove removes key/value entry from the OperationsMap at the specified
// requestID.  This is a thread-safe method.
//
// param requestID int64 -> the requestID of the request sent to
// the Neon.Cadence lib client.  This will be the mapped key
//
// returns int64 -> requestID of the request being removed in the
// Operation at the specified requestID
func (opMap *OperationsMap) Remove(requestID int64) int64 {
	opMap.safeMap.Delete(requestID)
	return requestID
}

// Get gets a Operation from the OperationsMap at the specified
// requestID.  This method is thread-safe.
//
// param requestID int64 -> the requestID of the request sent to
// the Neon.Cadence lib client.  This will be the mapped key
//
// returns *Operation -> pointer to Operation at the specified requestID
// in the map.
func (opMap *OperationsMap) Get(requestID int64) *Operation {
	if v, ok := opMap.safeMap.Load(requestID); ok {
		if _v, _ok := v.(*Operation); _ok {
			return _v
		}
	}

	return nil
}
