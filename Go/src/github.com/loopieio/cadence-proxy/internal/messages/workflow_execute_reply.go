package messages

import (
	"go.uber.org/cadence/workflow"

	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
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

// SetProxyMessage inherits docs from WorkflowReply.SetProxyMessage()
func (reply *WorkflowExecuteReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowReply.GetProxyMessage()
func (reply *WorkflowExecuteReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowReply.GetRequestID()
func (reply *WorkflowExecuteReply) GetRequestID() int64 {
	return reply.WorkflowReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowReply.SetRequestID()
func (reply *WorkflowExecuteReply) SetRequestID(value int64) {
	reply.WorkflowReply.SetRequestID(value)
}

// GetType inherits docs from WorkflowReply.GetType()
func (reply *WorkflowExecuteReply) GetType() messagetypes.MessageType {
	return reply.WorkflowReply.GetType()
}

// SetType inherits docs from WorkflowReply.SetType()
func (reply *WorkflowExecuteReply) SetType(value messagetypes.MessageType) {
	reply.WorkflowReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowReply.GetError()
func (reply *WorkflowExecuteReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowReply.GetError()
}

// SetError inherits docs from WorkflowReply.SetError()
func (reply *WorkflowExecuteReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowReply interface methods for implementing the IWorkflowReply interface

// GetWorkflowContextID inherits docs from WorkflowReply.GetWorkflowContextID()
func (reply *WorkflowExecuteReply) GetWorkflowContextID() int64 {
	return reply.WorkflowReply.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowReply.GetWorkflowContextID()
func (reply *WorkflowExecuteReply) SetWorkflowContextID(value int64) {
	reply.WorkflowReply.SetWorkflowContextID(value)
}
