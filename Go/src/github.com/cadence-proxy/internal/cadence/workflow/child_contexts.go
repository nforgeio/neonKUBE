//-----------------------------------------------------------------------------
// FILE:	    child_contexts.go
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

package workflow

import (
	"sync"

	"go.uber.org/cadence/workflow"
)

var (

	// childID is incremented (protected by a mutex) every
	// time a new cadence workflow.Context is created by a
	// child workflow
	childID int64
)

type (

	// ChildContextsMap holds a thread-safe map[interface{}]interface{} of
	// cadence childContextsMap with their contextID's
	ChildContextsMap struct {
		safeMap sync.Map
	}

	// ChildContext maps a child workflow contexts to a ChildID.
	// It holds a workflow Future and cancellation function
	ChildContext struct {
		future     workflow.ChildWorkflowFuture
		cancelFunc workflow.CancelFunc
	}
)

//----------------------------------------------------------------------------
// childID methods

// NextChildID increments the global variable
// childID by 1 and is protected by a mutex lock
func NextChildID() int64 {
	mu.Lock()
	childID = childID + 1
	defer mu.Unlock()
	return childID
}

// GetChildID gets the value of the global variable
// childID and is protected by a mutex Read lock
func GetChildID() int64 {
	mu.RLock()
	defer mu.RUnlock()
	return childID
}

//----------------------------------------------------------------------------
// ChildContext instance methods

// NewChildContext is the default constructor
// for a ChildContext struct
//
// param future workflow.ChildWorkflowFuture -> the ChildWorkflowFuture associated
// with an instance of a child workflow.
//
// param cancel workflow.CancelFunc -> the child workflow's cancellation function.
//
// returns *ChildContext -> pointer to a newly initialized
// ChildContext in memory
func NewChildContext(future workflow.ChildWorkflowFuture, cancel workflow.CancelFunc) *ChildContext {
	cctx := new(ChildContext)
	cctx.SetFuture(future)
	cctx.SetCancelFunction(cancel)
	return cctx
}

// GetCancelFunction gets a ChildContext's context cancel function
//
// returns workflow.CancelFunc -> a cadence workflow context cancel function
func (cctx *ChildContext) GetCancelFunction() workflow.CancelFunc {
	return cctx.cancelFunc
}

// SetCancelFunction sets a ChildContext's cancel function
//
// param value workflow.CancelFunc -> a cadence workflow context cancel function
func (cctx *ChildContext) SetCancelFunction(value workflow.CancelFunc) {
	cctx.cancelFunc = value
}

// GetFuture gets a ChildContext's workflow.ChildWorkflowFuture
//
// returns workflow.ChildWorkflowFuture -> a cadence workflow.ChildWorkflowFuture
func (cctx *ChildContext) GetFuture() workflow.ChildWorkflowFuture {
	return cctx.future
}

// SetFuture sets a ChildContext's workflow.ChildWorkflowFuture
//
// param value workflow.ChildWorkflowFuture -> a cadence workflow.ChildWorkflowFuture to be
// set as a ChildContext's cadence workflow.ChildWorkflowFuture
func (cctx *ChildContext) SetFuture(value workflow.ChildWorkflowFuture) {
	cctx.future = value
}

//----------------------------------------------------------------------------
// ChildContextsMap instance methods

// Add adds a new cadence context and its corresponding ContextId into
// the ChildContextsMap map.  This method is thread-safe.
//
// param id int64 -> the long id passed to Cadence
// child workflow function.  This will be the mapped key
//
// param cctx *ChildContext -> pointer to the new ChildContex used to
// execute child workflow function. This will be the mapped value
//
// returns int64 -> long id of the new cadence ChildContext added to the map
func (cctxs *ChildContextsMap) Add(id int64, cctx *ChildContext) int64 {
	cctxs.safeMap.Store(id, cctx)
	return id
}

// Remove removes key/value entry from the ChildContextsMap map at the specified
// ContextId.  This is a thread-safe method.
//
// param id int64 -> the long id passed to Cadence
// child workflow function.  This will be the mapped key
//
// returns int64 -> long id of the ChildContext removed from the map
func (cctxs *ChildContextsMap) Remove(id int64) int64 {
	cctxs.safeMap.Delete(id)
	return id
}

// Get gets a ChildContext from the ChildContextsMap at the specified
// ContextID.  This method is thread-safe.
//
// param id int64 -> the long id passed to Cadence
// child workflow function.  This will be the mapped key
//
// returns *ChildContext -> pointer to ChildContext with the specified id
func (cctxs *ChildContextsMap) Get(id int64) *ChildContext {
	if v, ok := cctxs.safeMap.Load(id); ok {
		if _v, _ok := v.(*ChildContext); _ok {
			return _v
		}
	}

	return nil
}
