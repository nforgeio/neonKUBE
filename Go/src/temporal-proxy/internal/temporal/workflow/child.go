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

	"go.temporal.io/sdk/workflow"
)

type (

	// ChildMap thread-safe map of
	// ChildContexts to their contextID's
	ChildMap struct {
		sync.Mutex                  // protects read and writes to the map
		contexts   map[int64]*Child // map of childID to ChildContext
	}

	// Child represents a temporal child workflow execution.
	// It holds a workflow Future and cancellation function
	Child struct {
		future     workflow.ChildWorkflowFuture // temporal ChildWorkflowFuture
		cancelFunc workflow.CancelFunc          // temporal workflow CancelFunc for canceling the child
	}
)

//----------------------------------------------------------------------------
// Child instance methods

// NewChild is the default constructor
// for a Child struct
//
// param future workflow.ChildWorkflowFuture -> the ChildWorkflowFuture associated
// with an instance of a child workflow.
//
// param cancel workflow.CancelFunc -> the child workflow's cancellation function.
//
// returns *Child -> pointer to a newly initialized
// Child in memory
func NewChild(future workflow.ChildWorkflowFuture, cancel workflow.CancelFunc) *Child {
	child := new(Child)
	child.SetFuture(future)
	child.SetCancelFunction(cancel)
	return child
}

// GetCancelFunction gets a Child's context cancel function
//
// returns workflow.CancelFunc -> a temporal workflow context cancel function
func (child *Child) GetCancelFunction() workflow.CancelFunc {
	return child.cancelFunc
}

// SetCancelFunction sets a Child's cancel function
//
// param value workflow.CancelFunc -> a temporal workflow context cancel function
func (child *Child) SetCancelFunction(value workflow.CancelFunc) {
	child.cancelFunc = value
}

// GetFuture gets a Child's workflow.ChildWorkflowFuture
//
// returns workflow.ChildWorkflowFuture -> a temporal workflow.ChildWorkflowFuture
func (child *Child) GetFuture() workflow.ChildWorkflowFuture {
	return child.future
}

// SetFuture sets a Child's workflow.ChildWorkflowFuture
//
// param value workflow.ChildWorkflowFuture -> a temporal workflow.ChildWorkflowFuture to be
// set as a Child's temporal workflow.ChildWorkflowFuture
func (child *Child) SetFuture(value workflow.ChildWorkflowFuture) {
	child.future = value
}

//----------------------------------------------------------------------------
// ChildMap instance methods

// NewChildMap is the constructor for an ChildMap
func NewChildMap() *ChildMap {
	o := new(ChildMap)
	o.contexts = make(map[int64]*Child)
	return o
}

// Add adds a new temporal context and its corresponding ContextId into
// the ChildMap map.  This method is thread-safe.
//
// param childID int64 -> the long childID. This will be the mapped key.
//
// param child *Child -> pointer to the new ChildContex used to
// execute child workflow function. This will be the mapped value
//
// returns int64 -> the long childID of the newly added Child.
func (c *ChildMap) Add(childID int64, child *Child) int64 {
	c.Lock()
	defer c.Unlock()
	c.contexts[childID] = child
	return childID
}

// Remove removes key/value entry from the ChildMap map at the specified
// ContextId.  This is a thread-safe method.
//
// param childID int64 -> the long childID. This will be the mapped key.
//
// returns int64 -> the long childID of the removed Child.
func (c *ChildMap) Remove(childID int64) int64 {
	c.Lock()
	defer c.Unlock()
	delete(c.contexts, childID)
	return childID
}

// Get gets a Child from the ChildMap at the specified
// ContextID.  This method is thread-safe.
//
// param childID int64 -> the long childID. This will be the mapped key.
//
// returns *Child -> Child at the specified childID.
func (c *ChildMap) Get(childID int64) *Child {
	c.Lock()
	defer c.Unlock()
	return c.contexts[childID]
}
