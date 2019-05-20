package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowRegisterReply is a ProxyReply of MessageType
	// WorkflowRegisterReply.  It holds a reference to a ProxyReply in memory
	// and is the reply type to a WorkflowRegisterRequest
	WorkflowRegisterReply struct {
		*ProxyReply
	}
)

// NewWorkflowRegisterReply is the default constructor for
// a WorkflowRegisterReply
//
// returns *WorkflowRegisterReply -> a pointer to a newly initialized
// WorkflowRegisterReply in memory
func NewWorkflowRegisterReply() *WorkflowRegisterReply {
	reply := new(WorkflowRegisterReply)
	reply.ProxyReply = NewProxyReply()
	reply.Type = messagetypes.WorkflowRegisterReply

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *WorkflowRegisterReply) Clone() IProxyMessage {
	workflowRegisterReply := NewWorkflowRegisterReply()
	var messageClone IProxyMessage = workflowRegisterReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *WorkflowRegisterReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyReply.SetProxyMessage()
func (reply *WorkflowRegisterReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyReply.GetProxyMessage()
func (reply *WorkflowRegisterReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyReply.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyReply.GetRequestID()
func (reply *WorkflowRegisterReply) GetRequestID() int64 {
	return reply.ProxyReply.GetRequestID()
}

// SetRequestID inherits docs from ProxyReply.SetRequestID()
func (reply *WorkflowRegisterReply) SetRequestID(value int64) {
	reply.ProxyReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *WorkflowRegisterReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *WorkflowRegisterReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
