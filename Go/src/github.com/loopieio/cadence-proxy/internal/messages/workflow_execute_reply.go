package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
	"go.uber.org/cadence/workflow"
)

type (

	// WorkflowExecuteReply is a WorkflowContextReply of MessageType
	// WorkflowExecuteReply.  It holds a reference to a WorkflowContextReply in memory
	// and is the reply type to a WorkflowExecuteRequest
	WorkflowExecuteReply struct {
		*WorkflowContextReply
	}
)

// NewWorkflowExecuteReply is the default constructor for
// a WorkflowExecuteReply
//
// returns *WorkflowExecuteReply -> a pointer to a newly initialized
// WorkflowExecuteReply in memory
func NewWorkflowExecuteReply() *WorkflowExecuteReply {
	reply := new(WorkflowExecuteReply)
	reply.WorkflowContextReply = NewWorkflowContextReply()
	reply.Type = messagetypes.WorkflowExecuteReply

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

// Clone inherits docs from WorkflowContextReply.Clone()
func (reply *WorkflowExecuteReply) Clone() IProxyMessage {
	workflowExecuteReply := NewWorkflowExecuteReply()
	var messageClone IProxyMessage = workflowExecuteReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowContextReply.CopyTo()
func (reply *WorkflowExecuteReply) CopyTo(target IProxyMessage) {
	reply.WorkflowContextReply.CopyTo(target)
	if v, ok := target.(*WorkflowExecuteReply); ok {
		v.SetExecution(reply.GetExecution())
	}
}

// SetProxyMessage inherits docs from WorkflowContextReply.SetProxyMessage()
func (reply *WorkflowExecuteReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowContextReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowContextReply.GetProxyMessage()
func (reply *WorkflowExecuteReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowContextReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowContextReply.GetRequestID()
func (reply *WorkflowExecuteReply) GetRequestID() int64 {
	return reply.WorkflowContextReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowContextReply.SetRequestID()
func (reply *WorkflowExecuteReply) SetRequestID(value int64) {
	reply.WorkflowContextReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowContextReply.GetError()
func (reply *WorkflowExecuteReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowContextReply.GetError()
}

// SetError inherits docs from WorkflowContextReply.SetError()
func (reply *WorkflowExecuteReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowContextReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowContextReply interface methods for implementing the IWorkflowContextReply interface

// GetWorkflowContextID inherits docs from WorkflowContextReply.GetWorkflowContextID()
func (reply *WorkflowExecuteReply) GetWorkflowContextID() int64 {
	return reply.WorkflowContextReply.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowContextReply.GetWorkflowContextID()
func (reply *WorkflowExecuteReply) SetWorkflowContextID(value int64) {
	reply.WorkflowContextReply.SetWorkflowContextID(value)
}
