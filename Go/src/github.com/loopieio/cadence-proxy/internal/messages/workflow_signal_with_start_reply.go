package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
	"go.uber.org/cadence/workflow"
)

type (

	// WorkflowSignalWithStartReply is a WorkflowReply of MessageType
	// WorkflowSignalWithStartReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSignalWithStartRequest
	WorkflowSignalWithStartReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSignalWithStartReply is the default constructor for
// a WorkflowSignalWithStartReply
//
// returns *WorkflowSignalWithStartReply -> a pointer to a newly initialized
// WorkflowSignalWithStartReply in memory
func NewWorkflowSignalWithStartReply() *WorkflowSignalWithStartReply {
	reply := new(WorkflowSignalWithStartReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowSignalWithStartReply)

	return reply
}

// GetExecution gets the cadence workflow.Execution or nil
// from a WorkflowSignalWithStartReply's properties map.
//
// returns *workflow.Execution -> pointer to a cadence workflow.Execution
// struct housing the result of a workflow execution
func (reply *WorkflowSignalWithStartReply) GetExecution() *workflow.Execution {
	exe := new(workflow.Execution)
	err := reply.GetJSONProperty("Execution", exe)
	if err != nil {
		return nil
	}

	return exe
}

// SetExecution sets the cadence workflow.Execution or nil
// in a WorkflowSignalWithStartReply's properties map.
//
// param value *workflow.Execution -> pointer to a cadence workflow execution
// struct housing the result of a started workflow, to be set in the
// WorkflowSignalWithStartReply's properties map
func (reply *WorkflowSignalWithStartReply) SetExecution(value *workflow.Execution) {
	reply.SetJSONProperty("Execution", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSignalWithStartReply) Clone() IProxyMessage {
	workflowSignalWithStartReply := NewWorkflowSignalWithStartReply()
	var messageClone IProxyMessage = workflowSignalWithStartReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSignalWithStartReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowSignalWithStartReply); ok {
		v.SetExecution(reply.GetExecution())
	}
}
