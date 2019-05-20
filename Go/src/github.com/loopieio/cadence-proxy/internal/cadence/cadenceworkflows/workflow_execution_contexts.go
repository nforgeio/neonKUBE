package cadenceworkflows

import (
	"sync"

	"go.uber.org/cadence/workflow"
)

var (
	mu sync.RWMutex

	// NextWorkflowContextID is incremented (protected by a mutex) every time
	// a new cadence workflow.Context is created
	WorkflowContextID int64

	// WorkflowExecutionContextsMap maps a int64 ContextId to the cadence
	// Workflow Context passed to the cadence Workflow functions.
	// The cadence-client will use contextIds to refer to specific
	// workflow ocntexts when perfoming workflow actions
	WorkflowExecutionContextsMap = new(WorkflowExecutionContexts)
)

type (

	// WorkflowExecutionContexts holds a thread-safe map[interface{}]interface{} of
	// cadence WorkflowExecutionContexts with their contextID's
	WorkflowExecutionContexts struct {
		sync.Map
	}

	// WorkflowExecutionContext holds a Cadence workflow
	// context as well as promise/future that will complete
	// when the workflow execution finishes.  It also holds
	// a cadence Settable to set values or errors on a
	// future.  This struct is used as an intermediate for
	// holding worklfow information and state while registering
	// and executing cadence workflows
	WorkflowExecutionContext struct {
		workflow.Context
		workflow.Future
		workflow.Settable
	}
)

//----------------------------------------------------------------------------
// WorkflowContextID methods

// NextWorkflowContextID increments the global variable
// WorkflowContextID by 1 and is protected by a mutex lock
func NextWorkflowContextID() int64 {
	mu.Lock()
	curr := WorkflowContextID
	WorkflowContextID = WorkflowContextID + 1
	mu.Unlock()

	return curr
}

// GetWorkflowContextID gets the value of the global variable
// WorkflowContextID and is protected by a mutex Read lock
func GetWorkflowContextID() int64 {
	mu.RLock()
	defer mu.RUnlock()
	return WorkflowContextID
}

//----------------------------------------------------------------------------
// WorkflowExecutionContext instance methods

// NewWorkflowExecutionContext is the default constructor
// for a WorkflowExecutionContext struct
//
// returns *WorkflowExecutionContext -> pointer to a newly initialized
// workflow ExecutionContext in memory
func NewWorkflowExecutionContext() *WorkflowExecutionContext {
	return new(WorkflowExecutionContext)
}

// GetContext gets a WorkflowExecutionContext's workflow.Context
//
// returns workflow.Context -> a cadence workflow context
func (wectx *WorkflowExecutionContext) GetContext() workflow.Context {
	return wectx.Context
}

// SetContext sets a WorkflowExecutionContext's workflow.Context
//
// param value workflow.Context -> a cadence workflow context to be
// set as a WorkflowExecutionContext's cadence workflow.Context
func (wectx *WorkflowExecutionContext) SetContext(value workflow.Context) {
	wectx.Context = value
}

// GetFuture gets a WorkflowExecutionContext's workflow.Future
//
// returns workflow.Future -> a cadence workflow.Future
func (wectx *WorkflowExecutionContext) GetFuture() workflow.Future {
	return wectx.Future
}

// SetFuture sets a WorkflowExecutionContext's workflow.Future
//
// param value workflow.Future -> a cadence workflow.Future to be
// set as a WorkflowExecutionContext's cadence workflow.Future
func (wectx *WorkflowExecutionContext) SetFuture(value workflow.Future) {
	wectx.Future = value
}

// GetSettable gets a WorkflowExecutionContext's workflow.Settable
//
// returns workflow.Settable -> a cadence workflow.Settable
func (wectx *WorkflowExecutionContext) GetSettable() workflow.Settable {
	return wectx.Settable
}

// SetSettable sets a WorkflowExecutionContext's workflow.Settable
//
// param value workflow.Settable -> a cadence workflow.Settable to be
// set as a WorkflowExecutionContext's cadence workflow.Settable
func (wectx *WorkflowExecutionContext) SetSettable(value workflow.Settable) {
	wectx.Settable = value
}

// Add adds a new cadence context and its corresponding ContextId into
// the WorkflowExecutionContexts map.  This method is thread-safe.
//
// param contextID int64 -> the long contextID passed to Cadence
// workflow functions.  This will be the mapped key
//
// param wectx *WorkflowExecutionContext -> pointer to the new WorkflowExecutionContex used to
// execute workflow functions. This will be the mapped value
//
// returns int64 -> long contextID of the new cadence WorkflowExecutionContext added to the map
func (workflowContexts *WorkflowExecutionContexts) Add(contextID int64, wectx *WorkflowExecutionContext) int64 {
	WorkflowExecutionContextsMap.Map.Store(contextID, wectx)
	return contextID
}

// Delete removes key/value entry from the WorkflowExecutionContexts map at the specified
// ContextId.  This is a thread-safe method.
//
// param contextID int64 -> the long contextID passed to Cadence
// workflow functions.  This will be the mapped key
//
// returns int64 -> long contextID of the WorkflowExecutionContext removed from the map
func (workflowContexts *WorkflowExecutionContexts) Delete(contextID int64) int64 {
	WorkflowExecutionContextsMap.Map.Delete(contextID)
	return contextID
}

// Get gets a WorkflowExecutionContext from the WorkflowExecutionContextsMap at the specified
// ContextID.  This method is thread-safe.
//
// param contextID int64 -> the long contextID passed to Cadence
// workflow functions.  This will be the mapped key
//
// returns *WorkflowExecutionContext -> pointer to WorkflowExecutionContext with the specified contextID
func (workflowContexts *WorkflowExecutionContexts) Get(contextID int64) *WorkflowExecutionContext {
	if v, ok := WorkflowExecutionContextsMap.Map.Load(contextID); ok {
		if _v, _ok := v.(*WorkflowExecutionContext); _ok {
			return _v
		}
	}

	return nil
}
