package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSignalSubscribeReply is a WorkflowReply of MessageType
	// WorkflowSignalSubscribeReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSignalSubscribeRequest
	WorkflowSignalSubscribeReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSignalSubscribeReply is the default constructor for
// a WorkflowSignalSubscribeReply
//
// returns *WorkflowSignalSubscribeReply -> a pointer to a newly initialized
// WorkflowSignalSubscribeReply in memory
func NewWorkflowSignalSubscribeReply() *WorkflowSignalSubscribeReply {
	reply := new(WorkflowSignalSubscribeReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowSignalSubscribeReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSignalSubscribeReply) Clone() IProxyMessage {
	workflowSignalSubscribeReply := NewWorkflowSignalSubscribeReply()
	var messageClone IProxyMessage = workflowSignalSubscribeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSignalSubscribeReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}

// SetProxyMessage inherits docs from WorkflowReply.SetProxyMessage()
func (reply *WorkflowSignalSubscribeReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowReply.GetProxyMessage()
func (reply *WorkflowSignalSubscribeReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowReply.GetRequestID()
func (reply *WorkflowSignalSubscribeReply) GetRequestID() int64 {
	return reply.WorkflowReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowReply.SetRequestID()
func (reply *WorkflowSignalSubscribeReply) SetRequestID(value int64) {
	reply.WorkflowReply.SetRequestID(value)
}

// GetType inherits docs from WorkflowReply.GetType()
func (reply *WorkflowSignalSubscribeReply) GetType() messagetypes.MessageType {
	return reply.WorkflowReply.GetType()
}

// SetType inherits docs from WorkflowReply.SetType()
func (reply *WorkflowSignalSubscribeReply) SetType(value messagetypes.MessageType) {
	reply.WorkflowReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowReply.GetError()
func (reply *WorkflowSignalSubscribeReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowReply.GetError()
}

// SetError inherits docs from WorkflowReply.SetError()
func (reply *WorkflowSignalSubscribeReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowReply interface methods for implementing the IWorkflowReply interface

// GetContextID inherits docs from WorkflowReply.GetContextID()
func (reply *WorkflowSignalSubscribeReply) GetContextID() int64 {
	return reply.WorkflowReply.GetContextID()
}

// SetContextID inherits docs from WorkflowReply.GetContextID()
func (reply *WorkflowSignalSubscribeReply) SetContextID(value int64) {
	reply.WorkflowReply.SetContextID(value)
}
