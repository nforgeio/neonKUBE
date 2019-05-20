package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowInvokeReply is a WorkflowContextReply of MessageType
	// WorkflowInvokeReply.  It holds a reference to a WorkflowContextReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowInvokeReply struct {
		*WorkflowContextReply
	}
)

// NewWorkflowInvokeReply is the default constructor for
// a WorkflowInvokeReply
//
// returns *WorkflowInvokeReply -> a pointer to a newly initialized
// WorkflowInvokeReply in memory
func NewWorkflowInvokeReply() *WorkflowInvokeReply {
	reply := new(WorkflowInvokeReply)
	reply.WorkflowContextReply = NewWorkflowContextReply()
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
	reply.WorkflowContextReply.CopyTo(target)
	if v, ok := target.(*WorkflowInvokeReply); ok {
		v.SetResult(reply.GetResult())
	}
}

// SetProxyMessage inherits docs from WorkflowContextReply.SetProxyMessage()
func (reply *WorkflowInvokeReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowContextReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowContextReply.GetProxyMessage()
func (reply *WorkflowInvokeReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowContextReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowContextReply.GetRequestID()
func (reply *WorkflowInvokeReply) GetRequestID() int64 {
	return reply.WorkflowContextReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowContextReply.SetRequestID()
func (reply *WorkflowInvokeReply) SetRequestID(value int64) {
	reply.WorkflowContextReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowContextReply.GetError()
func (reply *WorkflowInvokeReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowContextReply.GetError()
}

// SetError inherits docs from WorkflowContextReply.SetError()
func (reply *WorkflowInvokeReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowContextReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowContextReply interface methods for implementing the IWorkflowContextReply interface

// GetWorkflowContextID inherits docs from WorkflowContextReply.GetWorkflowContextID()
func (request *WorkflowInvokeReply) GetWorkflowContextID() int64 {
	return request.WorkflowContextReply.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowContextReply.SetWorkflowContextID()
func (request *WorkflowInvokeReply) SetWorkflowContextID(value int64) {
	request.WorkflowContextReply.SetWorkflowContextID(value)
}
