package cadenceworkflows

import (
	"sync"

	"go.uber.org/cadence/workflow"
)

var (

	// WorkflowContextsMap maps a int64 ContextId to the cadence
	// Workflow Context passed to the cadence Workflow functions.
	// The cadence-client will use contextIds to refer to specific
	// workflow ocntexts when perfoming workflow actions
	WorkflowContextsMap = new(WorkflowContexts)
)

type (

	// WorkflowContexts holds a thread-safe map[interface{}]interface{} of
	// cadence WorkflowContexts with their contextID's
	WorkflowContexts struct {
		sync.Map
	}
)

// Add adds a new cadence context and its corresponding ContextId into
// the WorkflowContexts map.  This method is thread-safe.
//
// param contextID int64 -> the long contextID passed to Cadence
// workflow functions.  This will be the mapped key
//
// param ctx *workflow.Context -> pointer to the new cadence context used to
// execute workflow functions. This will be the mapped value
//
// returns int64 -> long contextID of the new cadence Workflow Context added to the map
func (workflowContexts *WorkflowContexts) Add(contextID int64, ctx *workflow.Context) int64 {
	WorkflowContextsMap.Map.Store(contextID, ctx)
	return contextID
}

// Delete removes key/value entry from the WorkflowContexts map at the specified
// ContextId.  This is a thread-safe method.
//
// param contextID int64 -> the long contextID passed to Cadence
// workflow functions.  This will be the mapped key
//
// returns int64 -> long contextID of the new cadence Workflow Context added to the map
func (workflowContexts *WorkflowContexts) Delete(contextID int64) int64 {
	WorkflowContextsMap.Map.Delete(contextID)
	return contextID
}

// Get gets a cadence Workflow Context from the WorkflowContextsMap at the specified
// ContextID.  This method is thread-safe.
//
// param contextID int64 -> the long contextID passed to Cadence
// workflow functions.  This will be the mapped key
//
// returns *workflow.Context -> pointer to cadence Workflow context with the specified contextID
func (workflowContexts *WorkflowContexts) Get(contextID int64) *workflow.Context {
	if v, ok := WorkflowContextsMap.Map.Load(contextID); ok {
		if _v, _ok := v.(*workflow.Context); _ok {
			return _v
		}
	}

	return nil
}
