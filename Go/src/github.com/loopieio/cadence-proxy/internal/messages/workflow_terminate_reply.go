package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowTerminateReply is a ProxyReply of MessageType
	// WorkflowTerminateReply.  It holds a reference to a ProxyReply in memory
	// and is the reply type to a WorkflowTerminateRequest
	WorkflowTerminateReply struct {
		*ProxyReply
	}
)

// NewWorkflowTerminateReply is the default constructor for
// a WorkflowTerminateReply
//
// returns *WorkflowTerminateReply -> a pointer to a newly initialized
// WorkflowTerminateReply in memory
func NewWorkflowTerminateReply() *WorkflowTerminateReply {
	reply := new(WorkflowTerminateReply)
	reply.ProxyReply = NewProxyReply()
	reply.Type = messagetypes.WorkflowTerminateReply

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *WorkflowTerminateReply) Clone() IProxyMessage {
	workflowTerminateReply := NewWorkflowTerminateReply()
	var messageClone IProxyMessage = workflowTerminateReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *WorkflowTerminateReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyReply.SetProxyMessage()
func (reply *WorkflowTerminateReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyReply.GetProxyMessage()
func (reply *WorkflowTerminateReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyReply.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyReply.GetRequestID()
func (reply *WorkflowTerminateReply) GetRequestID() int64 {
	return reply.ProxyReply.GetRequestID()
}

// SetRequestID inherits docs from ProxyReply.SetRequestID()
func (reply *WorkflowTerminateReply) SetRequestID(value int64) {
	reply.ProxyReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *WorkflowTerminateReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *WorkflowTerminateReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
