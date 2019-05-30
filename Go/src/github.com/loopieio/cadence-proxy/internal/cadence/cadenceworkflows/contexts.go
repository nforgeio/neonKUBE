package cadenceworkflows

import (
	"sync"

	"go.uber.org/cadence/workflow"
)

var (
	mu sync.RWMutex

	// ContextID is incremented (protected by a mutex) every time
	// a new cadence workflow.Context is created
	ContextID int64

	// WorkflowContexts maps a int64 ContextId to the cadence
	// Workflow Context passed to the cadence Workflow functions.
	// The cadence-client will use contextIds to refer to specific
	// workflow ocntexts when perfoming workflow actions
	WorkflowContexts = new(WorkflowContextsMap)
)

type (

	// WorkflowContextsMap holds a thread-safe map[interface{}]interface{} of
	// cadence WorkflowContextsMap with their contextID's
	WorkflowContextsMap struct {
		sync.Map
	}

	// WorkflowContext holds a Cadence workflow
	// context as well as the workflow function. This struct is used as
	// an intermediate for storing worklfow information and state while registering
	// and executing cadence workflows
	WorkflowContext struct {
		workflow.Context
		workflowFunc func(ctx workflow.Context, input []byte) ([]byte, error)
	}
)

//----------------------------------------------------------------------------
// ContextID methods

// NextContextID increments the global variable
// ContextID by 1 and is protected by a mutex lock
func NextContextID() int64 {
	mu.Lock()
	curr := ContextID
	ContextID = ContextID + 1
	mu.Unlock()

	return curr
}

// GetContextID gets the value of the global variable
// ContextID and is protected by a mutex Read lock
func GetContextID() int64 {
	mu.RLock()
	defer mu.RUnlock()
	return ContextID
}

//----------------------------------------------------------------------------
// WorkflowContext instance methods

// NewWorkflowContext is the default constructor
// for a WorkflowContext struct
//
// returns *WorkflowContext -> pointer to a newly initialized
// workflow ExecutionContext in memory
func NewWorkflowContext() *WorkflowContext {
	return new(WorkflowContext)
}

// GetContext gets a WorkflowContext's workflow.Context
//
// returns workflow.Context -> a cadence workflow context
func (wectx *WorkflowContext) GetContext() workflow.Context {
	return wectx.Context
}

// SetContext sets a WorkflowContext's workflow.Context
//
// param value workflow.Context -> a cadence workflow context to be
// set as a WorkflowContext's cadence workflow.Context
func (wectx *WorkflowContext) SetContext(value workflow.Context) {
	wectx.Context = value
}

// GetWorkflowFunction gets a WorkflowContext's workflow function
//
// returns func(ctx workflow.Context, input []byte) ([]byte, error) -> a cadence workflow function
func (wectx *WorkflowContext) GetWorkflowFunction() func(ctx workflow.Context, input []byte) ([]byte, error) {
	return wectx.workflowFunc
}

// SetWorkflowFunction sets a WorkflowContext's workflow function
//
// param value func(ctx workflow.Context, input []byte) ([]byte, error) -> a cadence workflow function
func (wectx *WorkflowContext) SetWorkflowFunction(value func(ctx workflow.Context, input []byte) ([]byte, error)) {
	wectx.workflowFunc = value
}

//----------------------------------------------------------------------------
// WorkflowContextsMap instance methods

// Add adds a new cadence context and its corresponding ContextId into
// the WorkflowContextsMap map.  This method is thread-safe.
//
// param contextID int64 -> the long contextID passed to Cadence
// workflow functions.  This will be the mapped key
//
// param wectx *WorkflowContext -> pointer to the new WorkflowContex used to
// execute workflow functions. This will be the mapped value
//
// returns int64 -> long contextID of the new cadence WorkflowContext added to the map
func (wectxs *WorkflowContextsMap) Add(contextID int64, wectx *WorkflowContext) int64 {
	wectxs.Store(contextID, wectx)
	return contextID
}

// Remove removes key/value entry from the WorkflowContextsMap map at the specified
// ContextId.  This is a thread-safe method.
//
// param contextID int64 -> the long contextID passed to Cadence
// workflow functions.  This will be the mapped key
//
// returns int64 -> long contextID of the WorkflowContext removed from the map
func (wectxs *WorkflowContextsMap) Remove(contextID int64) int64 {
	wectxs.Delete(contextID)
	return contextID
}

// Get gets a WorkflowContext from the WorkflowContextsMap at the specified
// ContextID.  This method is thread-safe.
//
// param contextID int64 -> the long contextID passed to Cadence
// workflow functions.  This will be the mapped key
//
// returns *WorkflowContext -> pointer to WorkflowContext with the specified contextID
func (wectxs *WorkflowContextsMap) Get(contextID int64) *WorkflowContext {
	if v, ok := wectxs.Load(contextID); ok {
		if _v, _ok := v.(*WorkflowContext); _ok {
			return _v
		}
	}

	return nil
}
