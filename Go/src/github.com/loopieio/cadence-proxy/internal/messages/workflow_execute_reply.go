package messages

import (
	"go.uber.org/cadence/workflow"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowExecuteReply is a WorkflowReply of MessageType
	// WorkflowExecuteReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowExecuteRequest
	WorkflowExecuteReply struct {
		*WorkflowReply
	}
)

// NewWorkflowExecuteReply is the default constructor for
// a WorkflowExecuteReply
//
// returns *WorkflowExecuteReply -> a pointer to a newly initialized
// WorkflowExecuteReply in memory
func NewWorkflowExecuteReply() *WorkflowExecuteReply {
	reply := new(WorkflowExecuteReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowExecuteReply)

	return reply
}

// GetExecution gets the workflow execution or nil
// from a WorkflowExecuteReply's properties map.
//
// returns *workflow.Execution -> pointer to a cadence workflow execution
// struct housing the result of a workflow execution
func (reply *WorkflowExecuteReply) GetExecution() *workflow.Execution {
	exe := new(workflow.Execution)
	err := reply.GetJSONProperty("Execution", exe)
	if err != nil {
		return nil
	}

	return exe
}

// SetExecution sets the workflow execution or nil
// in a WorkflowExecuteReply's properties map.
//
// param value *workflow.Execution -> pointer to a cadence workflow execution
// struct housing the result of a workflow execution, to be set in the
// WorkflowExecuteReply's properties map
func (reply *WorkflowExecuteReply) SetExecution(value *workflow.Execution) {
	reply.SetJSONProperty("Execution", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowExecuteReply) Clone() IProxyMessage {
	workflowExecuteReply := NewWorkflowExecuteReply()
	var messageClone IProxyMessage = workflowExecuteReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowExecuteReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowExecuteReply); ok {
		v.SetExecution(reply.GetExecution())
	}
}
