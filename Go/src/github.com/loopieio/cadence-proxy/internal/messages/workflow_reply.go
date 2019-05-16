package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowReply is base type for all workflow replies.
	// All workflow replies will inherit from WorkflowReply
	//
	// A WorkflowReply contains a reference to a
	// ProxyReply struct in memory
	WorkflowReply struct {
		*ProxyReply
	}

	// IWorkflowReply is the interface that all workflow message replies
	// implement.
	IWorkflowReply interface{}
)

// NewWorkflowReply is the default constructor for WorkflowReply.
// It creates a new WorkflowReply in memory and then creates and sets
// a reference to a new ProxyReply in the WorkflowReply.
//
// returns *WorkflowReply -> a pointer to a new WorkflowReply in memory
func NewWorkflowReply() *WorkflowReply {
	reply := new(WorkflowReply)
	reply.ProxyReply = NewProxyReply()
	reply.Type = messagetypes.Unspecified

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowReply) Clone() IProxyMessage {
	workflowReply := NewWorkflowReply()
	var messageClone IProxyMessage = workflowReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *WorkflowReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *WorkflowReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyReply.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (reply *WorkflowReply) GetRequestID() int64 {
	return reply.ProxyReply.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (reply *WorkflowReply) SetRequestID(value int64) {
	reply.ProxyReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from IProxyReply.GetError()
func (reply *WorkflowReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from IProxyReply.SetError()
func (reply *WorkflowReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
