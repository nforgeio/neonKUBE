package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowCancelReply is a ProxyReply of MessageType
	// WorkflowCancelReply.  It holds a reference to a ProxyReply in memory
	// and is the reply type to a WorkflowCancelRequest
	WorkflowCancelReply struct {
		*ProxyReply
	}
)

// NewWorkflowCancelReply is the default constructor for
// a WorkflowCancelReply
//
// returns *WorkflowCancelReply -> a pointer to a newly initialized
// WorkflowCancelReply in memory
func NewWorkflowCancelReply() *WorkflowCancelReply {
	reply := new(WorkflowCancelReply)
	reply.ProxyReply = NewProxyReply()
	reply.Type = messagetypes.WorkflowCancelReply

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *WorkflowCancelReply) Clone() IProxyMessage {
	workflowCancelReply := NewWorkflowCancelReply()
	var messageClone IProxyMessage = workflowCancelReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *WorkflowCancelReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyReply.SetProxyMessage()
func (reply *WorkflowCancelReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyReply.GetProxyMessage()
func (reply *WorkflowCancelReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyReply.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyReply.GetRequestID()
func (reply *WorkflowCancelReply) GetRequestID() int64 {
	return reply.ProxyReply.GetRequestID()
}

// SetRequestID inherits docs from ProxyReply.SetRequestID()
func (reply *WorkflowCancelReply) SetRequestID(value int64) {
	reply.ProxyReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *WorkflowCancelReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *WorkflowCancelReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
