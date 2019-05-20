package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSignalReply is a ProxyReply of MessageType
	// WorkflowSignalReply.  It holds a reference to a ProxyReply in memory
	// and is the reply type to a WorkflowSignalRequest
	WorkflowSignalReply struct {
		*ProxyReply
	}
)

// NewWorkflowSignalReply is the default constructor for
// a WorkflowSignalReply
//
// returns *WorkflowSignalReply -> a pointer to a newly initialized
// WorkflowSignalReply in memory
func NewWorkflowSignalReply() *WorkflowSignalReply {
	reply := new(WorkflowSignalReply)
	reply.ProxyReply = NewProxyReply()
	reply.Type = messagetypes.WorkflowSignalReply

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *WorkflowSignalReply) Clone() IProxyMessage {
	workflowSignalReply := NewWorkflowSignalReply()
	var messageClone IProxyMessage = workflowSignalReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *WorkflowSignalReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyReply.SetProxyMessage()
func (reply *WorkflowSignalReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyReply.GetProxyMessage()
func (reply *WorkflowSignalReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyReply.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyReply.GetRequestID()
func (reply *WorkflowSignalReply) GetRequestID() int64 {
	return reply.ProxyReply.GetRequestID()
}

// SetRequestID inherits docs from ProxyReply.SetRequestID()
func (reply *WorkflowSignalReply) SetRequestID(value int64) {
	reply.ProxyReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *WorkflowSignalReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *WorkflowSignalReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
