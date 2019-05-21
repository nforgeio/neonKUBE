package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowRegisterReply is a WorkflowContextReply of MessageType
	// WorkflowRegisterReply.  It holds a reference to a WorkflowContextReply in memory
	// and is the reply type to a WorkflowRegisterRequest
	WorkflowRegisterReply struct {
		*WorkflowContextReply
	}
)

// NewWorkflowRegisterReply is the default constructor for
// a WorkflowRegisterReply
//
// returns *WorkflowRegisterReply -> a pointer to a newly initialized
// WorkflowRegisterReply in memory
func NewWorkflowRegisterReply() *WorkflowRegisterReply {
	reply := new(WorkflowRegisterReply)
	reply.WorkflowContextReply = NewWorkflowContextReply()
	reply.Type = messagetypes.WorkflowRegisterReply

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowContextReply.Clone()
func (reply *WorkflowRegisterReply) Clone() IProxyMessage {
	workflowRegisterReply := NewWorkflowRegisterReply()
	var messageClone IProxyMessage = workflowRegisterReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowContextReply.CopyTo()
func (reply *WorkflowRegisterReply) CopyTo(target IProxyMessage) {
	reply.WorkflowContextReply.CopyTo(target)
}

// SetProxyMessage inherits docs from WorkflowContextReply.SetProxyMessage()
func (reply *WorkflowRegisterReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowContextReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowContextReply.GetProxyMessage()
func (reply *WorkflowRegisterReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowContextReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowContextReply.GetRequestID()
func (reply *WorkflowRegisterReply) GetRequestID() int64 {
	return reply.WorkflowContextReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowContextReply.SetRequestID()
func (reply *WorkflowRegisterReply) SetRequestID(value int64) {
	reply.WorkflowContextReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowContextReply.GetError()
func (reply *WorkflowRegisterReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowContextReply.GetError()
}

// SetError inherits docs from WorkflowContextReply.SetError()
func (reply *WorkflowRegisterReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowContextReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowContextReply interface methods for implementing the IWorkflowContextReply interface

// GetWorkflowContextID inherits docs from WorkflowContextReply.GetWorkflowContextID()
func (reply *WorkflowRegisterReply) GetWorkflowContextID() int64 {
	return reply.WorkflowContextReply.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowContextReply.GetWorkflowContextID()
func (reply *WorkflowRegisterReply) SetWorkflowContextID(value int64) {
	reply.WorkflowContextReply.SetWorkflowContextID(value)
}
