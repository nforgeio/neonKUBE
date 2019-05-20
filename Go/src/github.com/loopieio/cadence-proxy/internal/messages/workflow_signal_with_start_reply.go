package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSignalWithStartReply is a ProxyReply of MessageType
	// WorkflowSignalWithStartReply.  It holds a reference to a ProxyReply in memory
	// and is the reply type to a WorkflowSignalWithStartRequest
	WorkflowSignalWithStartReply struct {
		*ProxyReply
	}
)

// NewWorkflowSignalWithStartReply is the default constructor for
// a WorkflowSignalWithStartReply
//
// returns *WorkflowSignalWithStartReply -> a pointer to a newly initialized
// WorkflowSignalWithStartReply in memory
func NewWorkflowSignalWithStartReply() *WorkflowSignalWithStartReply {
	reply := new(WorkflowSignalWithStartReply)
	reply.ProxyReply = NewProxyReply()
	reply.Type = messagetypes.WorkflowSignalWithStartReply

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *WorkflowSignalWithStartReply) Clone() IProxyMessage {
	workflowSignalWithStartReply := NewWorkflowSignalWithStartReply()
	var messageClone IProxyMessage = workflowSignalWithStartReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *WorkflowSignalWithStartReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyReply.SetProxyMessage()
func (reply *WorkflowSignalWithStartReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyReply.GetProxyMessage()
func (reply *WorkflowSignalWithStartReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyReply.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyReply.GetRequestID()
func (reply *WorkflowSignalWithStartReply) GetRequestID() int64 {
	return reply.ProxyReply.GetRequestID()
}

// SetRequestID inherits docs from ProxyReply.SetRequestID()
func (reply *WorkflowSignalWithStartReply) SetRequestID(value int64) {
	reply.ProxyReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *WorkflowSignalWithStartReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *WorkflowSignalWithStartReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
