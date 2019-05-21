package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowQueryReply is a WorkflowContextReply of MessageType
	// WorkflowQueryReply.  It holds a reference to a WorkflowContextReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowQueryReply struct {
		*WorkflowContextReply
	}
)

// NewWorkflowQueryReply is the default constructor for
// a WorkflowQueryReply
//
// returns *WorkflowQueryReply -> a pointer to a newly initialized
// WorkflowQueryReply in memory
func NewWorkflowQueryReply() *WorkflowQueryReply {
	reply := new(WorkflowQueryReply)
	reply.WorkflowContextReply = NewWorkflowContextReply()
	reply.Type = messagetypes.WorkflowQueryReply

	return reply
}

// GetResult gets the workflow execution result or nil
// from a WorkflowQueryReply's properties map.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowQueryReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the workflow execution result or nil
// in a WorkflowQueryReply's properties map.
//
// param value []byte -> []byte representing the result of a workflow execution
// to be set in the WorkflowQueryReply's properties map
func (reply *WorkflowQueryReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowQueryReply) Clone() IProxyMessage {
	workflowQueryReply := NewWorkflowQueryReply()
	var messageClone IProxyMessage = workflowQueryReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowQueryReply) CopyTo(target IProxyMessage) {
	reply.WorkflowContextReply.CopyTo(target)
	if v, ok := target.(*WorkflowQueryReply); ok {
		v.SetResult(reply.GetResult())
	}
}

// SetProxyMessage inherits docs from WorkflowContextReply.SetProxyMessage()
func (reply *WorkflowQueryReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowContextReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowContextReply.GetProxyMessage()
func (reply *WorkflowQueryReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowContextReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowContextReply.GetRequestID()
func (reply *WorkflowQueryReply) GetRequestID() int64 {
	return reply.WorkflowContextReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowContextReply.SetRequestID()
func (reply *WorkflowQueryReply) SetRequestID(value int64) {
	reply.WorkflowContextReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowContextReply.GetError()
func (reply *WorkflowQueryReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowContextReply.GetError()
}

// SetError inherits docs from WorkflowContextReply.SetError()
func (reply *WorkflowQueryReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowContextReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowContextReply interface methods for implementing the IWorkflowContextReply interface

// GetWorkflowContextID inherits docs from WorkflowContextReply.GetWorkflowContextID()
func (reply *WorkflowQueryReply) GetWorkflowContextID() int64 {
	return reply.WorkflowContextReply.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowContextReply.GetWorkflowContextID()
func (reply *WorkflowQueryReply) SetWorkflowContextID(value int64) {
	reply.WorkflowContextReply.SetWorkflowContextID(value)
}
