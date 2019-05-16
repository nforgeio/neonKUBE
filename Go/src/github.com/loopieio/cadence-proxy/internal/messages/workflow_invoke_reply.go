package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowInvokeReply is a WorkflowReply of MessageType
	// WorkflowInvokeReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowInvokeReply struct {
		*WorkflowReply
	}
)

// NewWorkflowInvokeReply is the default constructor for
// a WorkflowInvokeReply
//
// returns *WorkflowInvokeReply -> a pointer to a newly initialized
// WorkflowInvokeReply in memory
func NewWorkflowInvokeReply() *WorkflowInvokeReply {
	reply := new(WorkflowInvokeReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.Type = messagetypes.WorkflowInvokeReply

	return reply
}

// GetResult gets the workflow execution result or nil
// from a WorkflowInvokeReply's properties map.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowInvokeReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the workflow execution result or nil
// in a WorkflowInvokeReply's properties map.
//
// param value []byte -> []byte representing the result of a workflow execution
// to be set in the WorkflowInvokeReply's properties map
func (reply *WorkflowInvokeReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowInvokeReply) Clone() IProxyMessage {
	workflowInvokeReply := NewWorkflowInvokeReply()
	var messageClone IProxyMessage = workflowInvokeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowInvokeReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowInvokeReply); ok {
		v.SetResult(reply.GetResult())
	}
}

// SetProxyMessage inherits docs from WorkflowReply.SetProxyMessage()
func (reply *WorkflowInvokeReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowReply.GetProxyMessage()
func (reply *WorkflowInvokeReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowReply.GetRequestID()
func (reply *WorkflowInvokeReply) GetRequestID() int64 {
	return reply.WorkflowReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowReply.SetRequestID()
func (reply *WorkflowInvokeReply) SetRequestID(value int64) {
	reply.WorkflowReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowReply.GetError()
func (reply *WorkflowInvokeReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowReply.GetError()
}

// SetError inherits docs from WorkflowReply.SetError()
func (reply *WorkflowInvokeReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowReply.SetError(value)
}
