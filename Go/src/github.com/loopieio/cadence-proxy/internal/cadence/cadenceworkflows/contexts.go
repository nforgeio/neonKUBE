package cadenceworkflows

import (
	"sync"

	"go.uber.org/cadence/workflow"
)

var (
	mu sync.RWMutex

	// contextID is incremented (protected by a mutex) every time
	// a new cadence workflow.Context is created
	contextID int64

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
	// context, the registered workflow function, a context cancel function,
	// and a map of ChildID's to ChildContext.
	// This struct is used as an intermediate for storing worklfow information
	// and state while registering and executing cadence workflows
	WorkflowContext struct {
		ctx           workflow.Context
		workflowFunc  func(ctx workflow.Context, input []byte) ([]byte, error)
		cancelFunc    func()
		childContexts *childContextsMap
	}
)

//----------------------------------------------------------------------------
// contextID methods

// NextContextID increments the global variable
// contextID by 1 and is protected by a mutex lock
func NextContextID() int64 {
	mu.Lock()
	curr := contextID
	contextID = contextID + 1
	mu.Unlock()

	return curr
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
func NewWorkflowContext() *WorkflowContext {
	wectx := new(WorkflowContext)
	wectx.childContexts = new(childContextsMap)
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

// GetCancelFunction gets a WorkflowContext's context cancel function
//
// returns func() -> a cadence workflow context cancel function
func (wectx *WorkflowContext) GetCancelFunction() func() {
	return wectx.cancelFunc
}

// SetCancelFunction sets a WorkflowContext's cancel function
//
// param value func() -> a cadence workflow context cancel function
func (wectx *WorkflowContext) SetCancelFunction(value func()) {
	wectx.cancelFunc = value
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
// childContextsMap at the specified ContextID.
// This method is thread-safe.
//
// param id int64 -> the long id passed to Cadence
// workflow functions. This will be the mapped key
//
// returns *WorkflowContext -> pointer to ChildContext with the specified id
func (wectx *WorkflowContext) GetChildContext(id int64) *ChildContext {
	if v, ok := wectx.childContexts.Load(id); ok {
		if _v, _ok := v.(*ChildContext); _ok {
			return _v
		}
	}

	return nil
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
	wectxs.Store(id, wectx)
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
	wectxs.Delete(id)
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
	if v, ok := wectxs.Load(id); ok {
		if _v, _ok := v.(*WorkflowContext); _ok {
			return _v
		}
	}

	return nil
}
