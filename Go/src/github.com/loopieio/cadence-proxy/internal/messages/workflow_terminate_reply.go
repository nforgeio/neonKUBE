package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowTerminateReply is a WorkflowContextReply of MessageType
	// WorkflowTerminateReply.  It holds a reference to a WorkflowContextReply in memory
	// and is the reply type to a WorkflowTerminateRequest
	WorkflowTerminateReply struct {
		*WorkflowContextReply
	}
)

// NewWorkflowTerminateReply is the default constructor for
// a WorkflowTerminateReply
//
// returns *WorkflowTerminateReply -> a pointer to a newly initialized
// WorkflowTerminateReply in memory
func NewWorkflowTerminateReply() *WorkflowTerminateReply {
	reply := new(WorkflowTerminateReply)
	reply.WorkflowContextReply = NewWorkflowContextReply()
	reply.Type = messagetypes.WorkflowTerminateReply

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowContextReply.Clone()
func (reply *WorkflowTerminateReply) Clone() IProxyMessage {
	workflowTerminateReply := NewWorkflowTerminateReply()
	var messageClone IProxyMessage = workflowTerminateReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowContextReply.CopyTo()
func (reply *WorkflowTerminateReply) CopyTo(target IProxyMessage) {
	reply.WorkflowContextReply.CopyTo(target)
}

// SetProxyMessage inherits docs from WorkflowContextReply.SetProxyMessage()
func (reply *WorkflowTerminateReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowContextReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowContextReply.GetProxyMessage()
func (reply *WorkflowTerminateReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowContextReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowContextReply.GetRequestID()
func (reply *WorkflowTerminateReply) GetRequestID() int64 {
	return reply.WorkflowContextReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowContextReply.SetRequestID()
func (reply *WorkflowTerminateReply) SetRequestID(value int64) {
	reply.WorkflowContextReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowContextReply.GetError()
func (reply *WorkflowTerminateReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowContextReply.GetError()
}

// SetError inherits docs from WorkflowContextReply.SetError()
func (reply *WorkflowTerminateReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowContextReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowContextReply interface methods for implementing the IWorkflowContextReply interface

// GetWorkflowContextID inherits docs from WorkflowContextReply.GetWorkflowContextID()
func (reply *WorkflowTerminateReply) GetWorkflowContextID() int64 {
	return reply.WorkflowContextReply.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowContextReply.GetWorkflowContextID()
func (reply *WorkflowTerminateReply) SetWorkflowContextID(value int64) {
	reply.WorkflowContextReply.SetWorkflowContextID(value)
}
