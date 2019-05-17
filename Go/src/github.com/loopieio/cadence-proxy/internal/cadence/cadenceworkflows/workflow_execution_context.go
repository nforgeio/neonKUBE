package cadenceworkflows

import (
	"go.uber.org/cadence/workflow"
)

type (

	// WorkflowExecutionContext holds a Cadence workflow
	// context as well as promise/future that will complete
	// when the workflow execution finishes.  This is used
	// as an intermediate for holding worklfow information and
	// state while registering and executing cadence
	// workflow
	WorkflowExecutionContext struct {
		workflow.Context
		workflow.Future
	}
)

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
func (wectx *WorkflowExecutionContext) SetContext(value WorkflowExecutionContext) {
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
