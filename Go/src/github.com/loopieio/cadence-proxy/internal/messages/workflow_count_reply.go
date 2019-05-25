package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowCountReply is a WorkflowReply of MessageType
	// WorkflowCountReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowCountRequest
	WorkflowCountReply struct {
		*WorkflowReply
	}
)

// NewWorkflowCountReply is the default constructor for
// a WorkflowCountReply
//
// returns *WorkflowCountReply -> a pointer to a newly initialized
// WorkflowCountReply in memory
func NewWorkflowCountReply() *WorkflowCountReply {
	reply := new(WorkflowCountReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowCountReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowCountReply) Clone() IProxyMessage {
	workflowCountReply := NewWorkflowCountReply()
	var messageClone IProxyMessage = workflowCountReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowCountReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}

// SetProxyMessage inherits docs from WorkflowReply.SetProxyMessage()
func (reply *WorkflowCountReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowReply.GetProxyMessage()
func (reply *WorkflowCountReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowReply.GetRequestID()
func (reply *WorkflowCountReply) GetRequestID() int64 {
	return reply.WorkflowReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowReply.SetRequestID()
func (reply *WorkflowCountReply) SetRequestID(value int64) {
	reply.WorkflowReply.SetRequestID(value)
}

// GetType inherits docs from WorkflowReply.GetType()
func (reply *WorkflowCountReply) GetType() messagetypes.MessageType {
	return reply.WorkflowReply.GetType()
}

// SetType inherits docs from WorkflowReply.SetType()
func (reply *WorkflowCountReply) SetType(value messagetypes.MessageType) {
	reply.WorkflowReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowReply.GetError()
func (reply *WorkflowCountReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowReply.GetError()
}

// SetError inherits docs from WorkflowReply.SetError()
func (reply *WorkflowCountReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowReply interface methods for implementing the IWorkflowReply interface

// GetWorkflowContextID inherits docs from WorkflowReply.GetWorkflowContextID()
func (reply *WorkflowCountReply) GetWorkflowContextID() int64 {
	return reply.WorkflowReply.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowReply.GetWorkflowContextID()
func (reply *WorkflowCountReply) SetWorkflowContextID(value int64) {
	reply.WorkflowReply.SetWorkflowContextID(value)
}
