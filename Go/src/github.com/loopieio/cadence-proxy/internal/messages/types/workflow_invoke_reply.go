package types

import (
	"github.com/loopieio/cadence-proxy/internal/cadenceerrors"
	"github.com/loopieio/cadence-proxy/internal/messages"
)

type (

	// WorkflowInvokeReply is a ProxyReply of MessageType
	// WorkflowInvokeReply.  It holds a reference to a ProxyReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowInvokeReply struct {
		*ProxyReply
	}
)

// NewWorkflowInvokeReply is the default constructor for
// a WorkflowInvokeReply
//
// returns *WorkflowInvokeReply -> a pointer to a newly initialized
// WorkflowInvokeReply in memory
func NewWorkflowInvokeReply() *WorkflowInvokeReply {
	reply := new(WorkflowInvokeReply)
	reply.ProxyReply = NewProxyReply()
	reply.Type = messages.WorkflowInvokeReply

	return reply
}

// GetResult gets the workflow execution result or nil
// from a WorkflowInvokeReply's properties map.
//
// returns []byte -> a []byte representing the result of a workflow execution
func (reply *WorkflowInvokeReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the workflow execution result or nil
// in a WorkflowInvokeReply's properties map.
//
// param value []byte -> []]byte representing the result of a workflow execution
// to be set in the WorkflowInvokeReply's properties map
func (reply *WorkflowInvokeReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowInvokeReply) Clone() IProxyMessage {
	WorkflowInvokeReply := NewWorkflowInvokeReply()
	var messageClone IProxyMessage = WorkflowInvokeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowInvokeReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(*WorkflowInvokeReply); ok {
		v.SetResult(reply.GetResult())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *WorkflowInvokeReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyMessage.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *WorkflowInvokeReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyMessage.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (reply *WorkflowInvokeReply) GetRequestID() int64 {
	return reply.ProxyMessage.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (reply *WorkflowInvokeReply) SetRequestID(value int64) {
	reply.ProxyMessage.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *WorkflowInvokeReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *WorkflowInvokeReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
