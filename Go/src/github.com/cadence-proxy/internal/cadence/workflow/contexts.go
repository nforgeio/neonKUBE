//-----------------------------------------------------------------------------
// FILE:		contexts.go
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
	mu sync.RWMutex

	// contextID is incremented (protected by a mutex) every time
	// a new cadence workflow.Context is created
	contextID int64
)

type (

	// WorkflowContextsMap holds a thread-safe map[interface{}]interface{} of
	// cadence WorkflowContextsMap with their contextID's
	WorkflowContextsMap struct {
		safeMap sync.Map
	}

	// WorkflowContext holds a Cadence workflow
	// context, the registered workflow function, a context cancel function,
	// and a map of ChildID's to ChildContext.
	// This struct is used as an intermediate for storing worklfow information
	// and state while registering and executing cadence workflows
	WorkflowContext struct {
		workflowName  *string
		ctx           workflow.Context
		cancelFunc    workflow.CancelFunc
		childContexts *ChildContextsMap
	}
)

//----------------------------------------------------------------------------
// contextID methods

// NextContextID increments the global variable
// contextID by 1 and is protected by a mutex lock
func NextContextID() int64 {
	mu.Lock()
	contextID = contextID + 1
	defer mu.Unlock()
	return contextID
}

// GetContextID gets the value of the global variable
// contextID and is protected by a mutex Read lock
func GetContextID() int64 {
	mu.RLock()
	defer mu.RUnlock()
	return contextID
}

//----------------------------------------------------------------------------
// WorkflowContext instance methods

// NewWorkflowContext is the default constructor
// for a WorkflowContext struct
//
// returns *WorkflowContext -> pointer to a newly initialized
// workflow ExecutionContext in memory
func NewWorkflowContext(ctx workflow.Context) *WorkflowContext {
	wectx := new(WorkflowContext)
	wectx.childContexts = new(ChildContextsMap)
	wectx.SetContext(ctx)
	return wectx
}

// GetContext gets a WorkflowContext's workflow.Context
//
// returns workflow.Context -> a cadence workflow context
func (wectx *WorkflowContext) GetContext() workflow.Context {
	return wectx.ctx
}

// SetContext sets a WorkflowContext's workflow.Context
//
// param value workflow.Context -> a cadence workflow context to be
// set as a WorkflowContext's cadence workflow.Context
func (wectx *WorkflowContext) SetContext(value workflow.Context) {
	wectx.ctx = value
}

// GetWorkflowName gets a WorkflowContext's workflow function name
//
// returns *string -> a cadence workflow function name
func (wectx *WorkflowContext) GetWorkflowName() *string {
	return wectx.workflowName
}

// SetWorkflowName sets a WorkflowContext's workflow function name
//
// param value *string -> a cadence workflow function name
func (wectx *WorkflowContext) SetWorkflowName(value *string) {
	wectx.workflowName = value
}

// GetCancelFunction gets a WorkflowContext's context cancel function
//
// returns workflow.CancelFunc -> a cadence workflow context cancel function
func (wectx *WorkflowContext) GetCancelFunction() workflow.CancelFunc {
	return wectx.cancelFunc
}

// SetCancelFunction sets a WorkflowContext's cancel function
//
// param value workflow.CancelFunc -> a cadence workflow context cancel function
func (wectx *WorkflowContext) SetCancelFunction(value workflow.CancelFunc) {
	wectx.cancelFunc = value
}

// GetChildContexts gets a WorkflowContext's child contexts map
//
// returns *ChildContextsMap -> a cadence workflow child contexts map
func (wectx *WorkflowContext) GetChildContexts() *ChildContextsMap {
	return wectx.childContexts
}

// SetChildContexts sets a WorkflowContext's cancel function
//
// param value *ChildContextsMap -> a cadence workflow child contexts map
func (wectx *WorkflowContext) SetChildContexts(value *ChildContextsMap) {
	wectx.childContexts = value
}

// AddChildContext adds a new cadence context and its corresponding ContextId into
// the WorkflowContext's childContexts map.  This method is thread-safe.
//
// param id int64 -> the long id passed to Cadence
// workflow functions.  This will be the mapped key
//
// param cctx *ChildContext -> pointer to the new WorkflowContex used to
// execute workflow functions. This will be the mapped value
//
// returns int64 -> long id of the new ChildContext added to the map
func (wectx *WorkflowContext) AddChildContext(id int64, cctx *ChildContext) int64 {
	return wectx.childContexts.Add(id, cctx)
}

// RemoveChildContext removes key/value entry from the WorkflowContext's
// childContexts map at the specified
// ContextId.  This is a thread-safe method.
//
// param id int64 -> the long id passed to Cadence
// workflow functions.  This will be the mapped key
//
// returns int64 -> long id of the ChildContext removed from the map
func (wectx *WorkflowContext) RemoveChildContext(id int64) int64 {
	return wectx.childContexts.Remove(id)
}

// GetChildContext gets a childContext from the WorkflowContext's
// ChildContextsMap at the specified ContextID.
// This method is thread-safe.
//
// param id int64 -> the long id passed to Cadence
// workflow functions. This will be the mapped key
//
// returns *WorkflowContext -> pointer to ChildContext with the specified id
func (wectx *WorkflowContext) GetChildContext(id int64) *ChildContext {
	return wectx.childContexts.Get(id)
}

//----------------------------------------------------------------------------
// WorkflowContextsMap instance methods

// Add adds a new cadence context and its corresponding ContextId into
// the WorkflowContextsMap map.  This method is thread-safe.
//
// param id int64 -> the long id passed to Cadence
// workflow functions.  This will be the mapped key
//
// param wectx *WorkflowContext -> pointer to the new WorkflowContex used to
// execute workflow functions. This will be the mapped value
//
// returns int64 -> long id of the new cadence WorkflowContext added to the map
func (wectxs *WorkflowContextsMap) Add(id int64, wectx *WorkflowContext) int64 {
	wectxs.safeMap.Store(id, wectx)
	return id
}

// Remove removes key/value entry from the WorkflowContextsMap map at the specified
// ContextId.  This is a thread-safe method.
//
// param id int64 -> the long id passed to Cadence
// workflow functions.  This will be the mapped key
//
// returns int64 -> long id of the WorkflowContext removed from the map
func (wectxs *WorkflowContextsMap) Remove(id int64) int64 {
	wectxs.safeMap.Delete(id)
	return id
}

// Get gets a WorkflowContext from the WorkflowContextsMap at the specified
// ContextID.  This method is thread-safe.
//
// param id int64 -> the long id passed to Cadence
// workflow functions.  This will be the mapped key
//
// returns *WorkflowContext -> pointer to WorkflowContext with the specified id
func (wectxs *WorkflowContextsMap) Get(id int64) *WorkflowContext {
	if v, ok := wectxs.safeMap.Load(id); ok {
		if _v, _ok := v.(*WorkflowContext); _ok {
			return _v
		}
	}

	return nil
}
