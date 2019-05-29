package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowCancelReply is a WorkflowReply of MessageType
	// WorkflowCancelReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowCancelRequest
	WorkflowCancelReply struct {
		*WorkflowReply
	}
)

// NewWorkflowCancelReply is the default constructor for
// a WorkflowCancelReply
//
// returns *WorkflowCancelReply -> a pointer to a newly initialized
// WorkflowCancelReply in memory
func NewWorkflowCancelReply() *WorkflowCancelReply {
	reply := new(WorkflowCancelReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowCancelReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowCancelReply) Clone() IProxyMessage {
	workflowCancelReply := NewWorkflowCancelReply()
	var messageClone IProxyMessage = workflowCancelReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowCancelReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}

// SetProxyMessage inherits docs from WorkflowReply.SetProxyMessage()
func (reply *WorkflowCancelReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowReply.GetProxyMessage()
func (reply *WorkflowCancelReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowReply.GetRequestID()
func (reply *WorkflowCancelReply) GetRequestID() int64 {
	return reply.WorkflowReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowReply.SetRequestID()
func (reply *WorkflowCancelReply) SetRequestID(value int64) {
	reply.WorkflowReply.SetRequestID(value)
}

// GetType inherits docs from WorkflowReply.GetType()
func (reply *WorkflowCancelReply) GetType() messagetypes.MessageType {
	return reply.WorkflowReply.GetType()
}

// SetType inherits docs from WorkflowReply.SetType()
func (reply *WorkflowCancelReply) SetType(value messagetypes.MessageType) {
	reply.WorkflowReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowReply.GetError()
func (reply *WorkflowCancelReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowReply.GetError()
}

// SetError inherits docs from WorkflowReply.SetError()
func (reply *WorkflowCancelReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowReply interface methods for implementing the IWorkflowReply interface

// GetContextID inherits docs from WorkflowReply.GetContextID()
func (reply *WorkflowCancelReply) GetContextID() int64 {
	return reply.WorkflowReply.GetContextID()
}

// SetContextID inherits docs from WorkflowReply.GetContextID()
func (reply *WorkflowCancelReply) SetContextID(value int64) {
	reply.WorkflowReply.SetContextID(value)
}
