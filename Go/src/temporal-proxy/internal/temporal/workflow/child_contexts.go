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

	"go.temporal.io/temporal/workflow"
)

type (

	// ChildContextsMap thread-safe map of
	// ChildContexts to their contextID's
	ChildContextsMap struct {
		sync.Mutex                         // protects read and writes to the map
		contexts   map[int64]*ChildContext // map of childID to ChildContext
	}

	// ChildContext represents a temporal child workflow execution.
	// It holds a workflow Future and cancellation function
	ChildContext struct {
		future     workflow.ChildWorkflowFuture // temporal ChildWorkflowFuture
		cancelFunc workflow.CancelFunc          // temporal workflow CancelFunc for canceling the child
	}
)

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
// returns workflow.CancelFunc -> a temporal workflow context cancel function
func (cctx *ChildContext) GetCancelFunction() workflow.CancelFunc {
	return cctx.cancelFunc
}

// SetCancelFunction sets a ChildContext's cancel function
//
// param value workflow.CancelFunc -> a temporal workflow context cancel function
func (cctx *ChildContext) SetCancelFunction(value workflow.CancelFunc) {
	cctx.cancelFunc = value
}

// GetFuture gets a ChildContext's workflow.ChildWorkflowFuture
//
// returns workflow.ChildWorkflowFuture -> a temporal workflow.ChildWorkflowFuture
func (cctx *ChildContext) GetFuture() workflow.ChildWorkflowFuture {
	return cctx.future
}

// SetFuture sets a ChildContext's workflow.ChildWorkflowFuture
//
// param value workflow.ChildWorkflowFuture -> a temporal workflow.ChildWorkflowFuture to be
// set as a ChildContext's temporal workflow.ChildWorkflowFuture
func (cctx *ChildContext) SetFuture(value workflow.ChildWorkflowFuture) {
	cctx.future = value
}

//----------------------------------------------------------------------------
// ChildContextsMap instance methods

// NewChildContextsMap is the constructor for an ChildContextsMap
func NewChildContextsMap() *ChildContextsMap {
	o := new(ChildContextsMap)
	o.contexts = make(map[int64]*ChildContext)
	return o
}

// Add adds a new temporal context and its corresponding ContextId into
// the ChildContextsMap map.  This method is thread-safe.
//
// param childID int64 -> the long childID. This will be the mapped key.
//
// param cctx *ChildContext -> pointer to the new ChildContex used to
// execute child workflow function. This will be the mapped value
//
// returns int64 -> the long childID of the newly added ChildContext.
func (c *ChildContextsMap) Add(childID int64, cctx *ChildContext) int64 {
	c.Lock()
	defer c.Unlock()
	c.contexts[childID] = cctx
	return childID
}

// Remove removes key/value entry from the ChildContextsMap map at the specified
// ContextId.  This is a thread-safe method.
//
// param childID int64 -> the long childID. This will be the mapped key.
//
// returns int64 -> the long childID of the removed ChildContext.
func (c *ChildContextsMap) Remove(childID int64) int64 {
	c.Lock()
	defer c.Unlock()
	delete(c.contexts, childID)
	return childID
}

// Get gets a ChildContext from the ChildContextsMap at the specified
// ContextID.  This method is thread-safe.
//
// param childID int64 -> the long childID. This will be the mapped key.
//
// returns *ChildContext -> ChildContext at the specified childID.
func (c *ChildContextsMap) Get(childID int64) *ChildContext {
	c.Lock()
	defer c.Unlock()
	return c.contexts[childID]
}
