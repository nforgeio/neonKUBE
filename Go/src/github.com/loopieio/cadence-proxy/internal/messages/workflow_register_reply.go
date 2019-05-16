package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowRegisterReply is a WorkflowReply of MessageType
	// WorkflowRegisterReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowRegisterRequest
	WorkflowRegisterReply struct {
		*WorkflowReply
	}
)

// NewWorkflowRegisterReply is the default constructor for
// a WorkflowRegisterReply
//
// returns *WorkflowRegisterReply -> a pointer to a newly initialized
// WorkflowRegisterReply in memory
func NewWorkflowRegisterReply() *WorkflowRegisterReply {
	reply := new(WorkflowRegisterReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.Type = messagetypes.WorkflowRegisterReply

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowRegisterReply) Clone() IProxyMessage {
	workflowRegisterReply := NewWorkflowRegisterReply()
	var messageClone IProxyMessage = workflowRegisterReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowRegisterReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}

// SetProxyMessage inherits docs from WorkflowReply.SetProxyMessage()
func (reply *WorkflowRegisterReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowReply.GetProxyMessage()
func (reply *WorkflowRegisterReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowReply.GetRequestID()
func (reply *WorkflowRegisterReply) GetRequestID() int64 {
	return reply.WorkflowReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowReply.SetRequestID()
func (reply *WorkflowRegisterReply) SetRequestID(value int64) {
	reply.WorkflowReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowReply.GetError()
func (reply *WorkflowRegisterReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowReply.GetError()
}

// SetError inherits docs from WorkflowReply.SetError()
func (reply *WorkflowRegisterReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowReply.SetError(value)
}
