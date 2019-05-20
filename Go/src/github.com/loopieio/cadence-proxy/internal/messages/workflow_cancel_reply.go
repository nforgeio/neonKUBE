package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowCancelReply is a WorkflowContextReply of MessageType
	// WorkflowCancelReply.  It holds a reference to a WorkflowContextReply in memory
	// and is the reply type to a WorkflowCancelRequest
	WorkflowCancelReply struct {
		*WorkflowContextReply
	}
)

// NewWorkflowCancelReply is the default constructor for
// a WorkflowCancelReply
//
// returns *WorkflowCancelReply -> a pointer to a newly initialized
// WorkflowCancelReply in memory
func NewWorkflowCancelReply() *WorkflowCancelReply {
	reply := new(WorkflowCancelReply)
	reply.WorkflowContextReply = NewWorkflowContextReply()
	reply.Type = messagetypes.WorkflowCancelReply

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowContextReply.Clone()
func (reply *WorkflowCancelReply) Clone() IProxyMessage {
	workflowCancelReply := NewWorkflowCancelReply()
	var messageClone IProxyMessage = workflowCancelReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowContextReply.CopyTo()
func (reply *WorkflowCancelReply) CopyTo(target IProxyMessage) {
	reply.WorkflowContextReply.CopyTo(target)
}

// SetProxyMessage inherits docs from WorkflowContextReply.SetProxyMessage()
func (reply *WorkflowCancelReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowContextReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowContextReply.GetProxyMessage()
func (reply *WorkflowCancelReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowContextReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowContextReply.GetRequestID()
func (reply *WorkflowCancelReply) GetRequestID() int64 {
	return reply.WorkflowContextReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowContextReply.SetRequestID()
func (reply *WorkflowCancelReply) SetRequestID(value int64) {
	reply.WorkflowContextReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowContextReply.GetError()
func (reply *WorkflowCancelReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowContextReply.GetError()
}

// SetError inherits docs from WorkflowContextReply.SetError()
func (reply *WorkflowCancelReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowContextReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowContextReply interface methods for implementing the IWorkflowContextReply interface

// GetContextID inherits docs from WorkflowContextReply.GetContextID()
func (request *WorkflowCancelReply) GetContextID() int64 {
	return request.GetLongProperty("ContextId")
}

// SetContextID inherits docs from WorkflowContextReply.SetContextID()
func (request *WorkflowCancelReply) SetContextID(value int64) {
	request.SetLongProperty("ContextId", value)
}
