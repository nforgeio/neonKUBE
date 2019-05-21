package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowMutableReply is a WorkflowReply of MessageType
	// WorkflowMutableReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowMutableReply struct {
		*WorkflowReply
	}
)

// NewWorkflowMutableReply is the default constructor for
// a WorkflowMutableReply
//
// returns *WorkflowMutableReply -> a pointer to a newly initialized
// WorkflowMutableReply in memory
func NewWorkflowMutableReply() *WorkflowMutableReply {
	reply := new(WorkflowMutableReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.Type = messagetypes.WorkflowMutableReply

	return reply
}

// GetResult gets the workflow execution result or nil
// from a WorkflowMutableReply's properties map.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowMutableReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the workflow execution result or nil
// in a WorkflowMutableReply's properties map.
//
// param value []byte -> []byte representing the result of a workflow execution
// to be set in the WorkflowMutableReply's properties map
func (reply *WorkflowMutableReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowMutableReply) Clone() IProxyMessage {
	workflowMutableReply := NewWorkflowMutableReply()
	var messageClone IProxyMessage = workflowMutableReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowMutableReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowMutableReply); ok {
		v.SetResult(reply.GetResult())
	}
}

// SetProxyMessage inherits docs from WorkflowReply.SetProxyMessage()
func (reply *WorkflowMutableReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowReply.GetProxyMessage()
func (reply *WorkflowMutableReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowReply.GetRequestID()
func (reply *WorkflowMutableReply) GetRequestID() int64 {
	return reply.WorkflowReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowReply.SetRequestID()
func (reply *WorkflowMutableReply) SetRequestID(value int64) {
	reply.WorkflowReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowReply.GetError()
func (reply *WorkflowMutableReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowReply.GetError()
}

// SetError inherits docs from WorkflowReply.SetError()
func (reply *WorkflowMutableReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowReply interface methods for implementing the IWorkflowReply interface

// GetWorkflowContextID inherits docs from WorkflowReply.GetWorkflowContextID()
func (reply *WorkflowMutableReply) GetWorkflowContextID() int64 {
	return reply.WorkflowReply.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowReply.GetWorkflowContextID()
func (reply *WorkflowMutableReply) SetWorkflowContextID(value int64) {
	reply.WorkflowReply.SetWorkflowContextID(value)
}
