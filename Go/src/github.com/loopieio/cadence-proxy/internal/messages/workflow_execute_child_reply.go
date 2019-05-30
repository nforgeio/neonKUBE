package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowExecuteChildReply is a WorkflowReply of MessageType
	// WorkflowExecuteChildReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowExecuteChildReply struct {
		*WorkflowReply
	}
)

// NewWorkflowExecuteChildReply is the default constructor for
// a WorkflowExecuteChildReply
//
// returns *WorkflowExecuteChildReply -> a pointer to a newly initialized
// WorkflowExecuteChildReply in memory
func NewWorkflowExecuteChildReply() *WorkflowExecuteChildReply {
	reply := new(WorkflowExecuteChildReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowExecuteChildReply)

	return reply
}

// GetResult gets the WorkflowExecuteChild result or nil
// from a WorkflowExecuteChildReply's properties map.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowExecuteChildReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the WorkflowExecuteChild result or nil
// in a WorkflowExecuteChildReply's properties map.
//
// param value []byte -> []byte representing the result of a cadence
// child workflow to be set in the WorkflowExecuteChildReply's properties map
func (reply *WorkflowExecuteChildReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowExecuteChildReply) Clone() IProxyMessage {
	workflowExecuteChildReply := NewWorkflowExecuteChildReply()
	var messageClone IProxyMessage = workflowExecuteChildReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowExecuteChildReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowExecuteChildReply); ok {
		v.SetResult(reply.GetResult())
	}
}

// SetProxyMessage inherits docs from WorkflowReply.SetProxyMessage()
func (reply *WorkflowExecuteChildReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowReply.GetProxyMessage()
func (reply *WorkflowExecuteChildReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowReply.GetRequestID()
func (reply *WorkflowExecuteChildReply) GetRequestID() int64 {
	return reply.WorkflowReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowReply.SetRequestID()
func (reply *WorkflowExecuteChildReply) SetRequestID(value int64) {
	reply.WorkflowReply.SetRequestID(value)
}

// GetType inherits docs from WorkflowReply.GetType()
func (reply *WorkflowExecuteChildReply) GetType() messagetypes.MessageType {
	return reply.WorkflowReply.GetType()
}

// SetType inherits docs from WorkflowReply.SetType()
func (reply *WorkflowExecuteChildReply) SetType(value messagetypes.MessageType) {
	reply.WorkflowReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowReply.GetError()
func (reply *WorkflowExecuteChildReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowReply.GetError()
}

// SetError inherits docs from WorkflowReply.SetError()
func (reply *WorkflowExecuteChildReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowReply interface methods for implementing the IWorkflowReply interface

// GetContextID inherits docs from WorkflowReply.GetContextID()
func (reply *WorkflowExecuteChildReply) GetContextID() int64 {
	return reply.WorkflowReply.GetContextID()
}

// SetContextID inherits docs from WorkflowReply.GetContextID()
func (reply *WorkflowExecuteChildReply) SetContextID(value int64) {
	reply.WorkflowReply.SetContextID(value)
}
