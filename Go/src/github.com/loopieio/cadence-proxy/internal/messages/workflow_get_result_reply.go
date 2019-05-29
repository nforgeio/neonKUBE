package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowGetResultReply is a WorkflowReply of MessageType
	// WorkflowGetResultReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowGetResultRequest
	WorkflowGetResultReply struct {
		*WorkflowReply
	}
)

// NewWorkflowGetResultReply is the default constructor for
// a WorkflowGetResultReply
//
// returns *WorkflowGetResultReply -> a pointer to a newly initialized
// WorkflowGetResultReply in memory
func NewWorkflowGetResultReply() *WorkflowGetResultReply {
	reply := new(WorkflowGetResultReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowGetResultReply)

	return reply
}

// GetResult gets the workflow execution result or nil
// from a WorkflowGetResultReply's properties map.
//
// returns []byte -> []byte representing the result of a workflow execution
func (reply *WorkflowGetResultReply) GetResult() []byte {
	return reply.GetBytesProperty("Result")
}

// SetResult sets the workflow execution result or nil
// in a WorkflowGetResultReply's properties map.
//
// param value []byte -> []byte representing the result of a workflow execution
// to be set in the WorkflowGetResultReply's properties map
func (reply *WorkflowGetResultReply) SetResult(value []byte) {
	reply.SetBytesProperty("Result", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowGetResultReply) Clone() IProxyMessage {
	workflowGetResultReply := NewWorkflowGetResultReply()
	var messageClone IProxyMessage = workflowGetResultReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowGetResultReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowGetResultReply); ok {
		v.SetResult(reply.GetResult())
	}
}

// SetProxyMessage inherits docs from WorkflowReply.SetProxyMessage()
func (reply *WorkflowGetResultReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowReply.GetProxyMessage()
func (reply *WorkflowGetResultReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowReply.GetRequestID()
func (reply *WorkflowGetResultReply) GetRequestID() int64 {
	return reply.WorkflowReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowReply.SetRequestID()
func (reply *WorkflowGetResultReply) SetRequestID(value int64) {
	reply.WorkflowReply.SetRequestID(value)
}

// GetType inherits docs from WorkflowReply.GetType()
func (reply *WorkflowGetResultReply) GetType() messagetypes.MessageType {
	return reply.WorkflowReply.GetType()
}

// SetType inherits docs from WorkflowReply.SetType()
func (reply *WorkflowGetResultReply) SetType(value messagetypes.MessageType) {
	reply.WorkflowReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowReply.GetError()
func (reply *WorkflowGetResultReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowReply.GetError()
}

// SetError inherits docs from WorkflowReply.SetError()
func (reply *WorkflowGetResultReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowReply interface methods for implementing the IWorkflowReply interface

// GetContextID inherits docs from WorkflowReply.GetContextID()
func (reply *WorkflowGetResultReply) GetContextID() int64 {
	return reply.WorkflowReply.GetContextID()
}

// SetContextID inherits docs from WorkflowReply.GetContextID()
func (reply *WorkflowGetResultReply) SetContextID(value int64) {
	reply.WorkflowReply.SetContextID(value)
}
