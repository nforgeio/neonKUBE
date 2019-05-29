package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowHasLastResultReply is a WorkflowReply of MessageType
	// WorkflowHasLastResultReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowHasLastResultRequest
	WorkflowHasLastResultReply struct {
		*WorkflowReply
	}
)

// NewWorkflowHasLastResultReply is the default constructor for
// a WorkflowHasLastResultReply
//
// returns *WorkflowHasLastResultReply -> a pointer to a newly initialized
// WorkflowHasLastResultReply in memory
func NewWorkflowHasLastResultReply() *WorkflowHasLastResultReply {
	reply := new(WorkflowHasLastResultReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowHasLastResultReply)

	return reply
}

// GetHasResult gets the HasResult property
// from a WorkflowHasLastResultReply's properties map.
// Indicates whether the workflow has a last completion result.
//
// returns bool -> HasResult from the WorkflowHasLastResultReply's
// properties map
func (reply *WorkflowHasLastResultReply) GetHasResult() bool {
	return reply.GetBoolProperty("HasResult")
}

// SetHasResult sets gets the HasResult property
// in a WorkflowHasLastResultReply's properties map.
// Indicates whether the workflow has a last completion result.
//
// param value bool -> HasResult from the WorkflowHasLastResultReply's
// properties map to be set in the
// WorkflowHasLastResultReply's properties map
func (reply *WorkflowHasLastResultReply) SetHasResult(value bool) {
	reply.SetBoolProperty("HasResult", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowHasLastResultReply) Clone() IProxyMessage {
	workflowHasLastResultReply := NewWorkflowHasLastResultReply()
	var messageClone IProxyMessage = workflowHasLastResultReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowHasLastResultReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowHasLastResultReply); ok {
		v.SetHasResult(reply.GetHasResult())
	}
}

// SetProxyMessage inherits docs from WorkflowReply.SetProxyMessage()
func (reply *WorkflowHasLastResultReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowReply.GetProxyMessage()
func (reply *WorkflowHasLastResultReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowReply.GetRequestID()
func (reply *WorkflowHasLastResultReply) GetRequestID() int64 {
	return reply.WorkflowReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowReply.SetRequestID()
func (reply *WorkflowHasLastResultReply) SetRequestID(value int64) {
	reply.WorkflowReply.SetRequestID(value)
}

// GetType inherits docs from WorkflowReply.GetType()
func (reply *WorkflowHasLastResultReply) GetType() messagetypes.MessageType {
	return reply.WorkflowReply.GetType()
}

// SetType inherits docs from WorkflowReply.SetType()
func (reply *WorkflowHasLastResultReply) SetType(value messagetypes.MessageType) {
	reply.WorkflowReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowReply.GetError()
func (reply *WorkflowHasLastResultReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowReply.GetError()
}

// SetError inherits docs from WorkflowReply.SetError()
func (reply *WorkflowHasLastResultReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowReply interface methods for implementing the IWorkflowReply interface

// GetContextID inherits docs from WorkflowReply.GetContextID()
func (reply *WorkflowHasLastResultReply) GetContextID() int64 {
	return reply.WorkflowReply.GetContextID()
}

// SetContextID inherits docs from WorkflowReply.GetContextID()
func (reply *WorkflowHasLastResultReply) SetContextID(value int64) {
	reply.WorkflowReply.SetContextID(value)
}
