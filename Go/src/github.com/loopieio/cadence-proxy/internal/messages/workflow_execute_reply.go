package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
	"go.uber.org/cadence/workflow"
)

type (

	// WorkflowExecuteReply is a ProxyReply of MessageType
	// WorkflowExecuteReply.  It holds a reference to a ProxyReply in memory
	// and is the reply type to a WorkflowExecuteRequest
	WorkflowExecuteReply struct {
		*ProxyReply
	}
)

// NewWorkflowExecuteReply is the default constructor for
// a WorkflowExecuteReply
//
// returns *WorkflowExecuteReply -> a pointer to a newly initialized
// WorkflowExecuteReply in memory
func NewWorkflowExecuteReply() *WorkflowExecuteReply {
	reply := new(WorkflowExecuteReply)
	reply.ProxyReply = NewProxyReply()
	reply.Type = messagetypes.WorkflowExecuteReply

	return reply
}

// GetExecution gets the workflow execution or nil
// from a WorkflowExecuteReply's properties map.
//
// returns *workflow.Execution -> pointer to a cadence workflow execution
// struct housing the result of a workflow execution
func (request *WorkflowExecuteReply) GetExecution() *workflow.Execution {
	exe := new(workflow.Execution)
	err := request.GetJSONProperty("Execution", exe)
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

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowExecuteReply) Clone() IProxyMessage {
	workflowExecuteReply := NewWorkflowExecuteReply()
	var messageClone IProxyMessage = workflowExecuteReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowExecuteReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(*WorkflowExecuteReply); ok {
		v.SetExecution(reply.GetExecution())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *WorkflowExecuteReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *WorkflowExecuteReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyReply.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (reply *WorkflowExecuteReply) GetRequestID() int64 {
	return reply.ProxyReply.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (reply *WorkflowExecuteReply) SetRequestID(value int64) {
	reply.ProxyReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *WorkflowExecuteReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *WorkflowExecuteReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
