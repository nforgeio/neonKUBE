package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowDescribeExecutionReply is a WorkflowReply of MessageType
	// WorkflowDescribeExecutionReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowDescribeExecutionRequest
	WorkflowDescribeExecutionReply struct {
		*WorkflowReply
	}
)

// NewWorkflowDescribeExecutionReply is the default constructor for
// a WorkflowDescribeExecutionReply
//
// returns *WorkflowDescribeExecutionReply -> a pointer to a newly initialized
// WorkflowDescribeExecutionReply in memory
func NewWorkflowDescribeExecutionReply() *WorkflowDescribeExecutionReply {
	reply := new(WorkflowDescribeExecutionReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(messagetypes.WorkflowDescribeExecutionReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowDescribeExecutionReply) Clone() IProxyMessage {
	workflowDescribeExecutionReply := NewWorkflowDescribeExecutionReply()
	var messageClone IProxyMessage = workflowDescribeExecutionReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowDescribeExecutionReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}

// SetProxyMessage inherits docs from WorkflowReply.SetProxyMessage()
func (reply *WorkflowDescribeExecutionReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowReply.GetProxyMessage()
func (reply *WorkflowDescribeExecutionReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowReply.GetRequestID()
func (reply *WorkflowDescribeExecutionReply) GetRequestID() int64 {
	return reply.WorkflowReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowReply.SetRequestID()
func (reply *WorkflowDescribeExecutionReply) SetRequestID(value int64) {
	reply.WorkflowReply.SetRequestID(value)
}

// GetType inherits docs from WorkflowReply.GetType()
func (reply *WorkflowDescribeExecutionReply) GetType() messagetypes.MessageType {
	return reply.WorkflowReply.GetType()
}

// SetType inherits docs from WorkflowReply.SetType()
func (reply *WorkflowDescribeExecutionReply) SetType(value messagetypes.MessageType) {
	reply.WorkflowReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowReply.GetError()
func (reply *WorkflowDescribeExecutionReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowReply.GetError()
}

// SetError inherits docs from WorkflowReply.SetError()
func (reply *WorkflowDescribeExecutionReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowReply interface methods for implementing the IWorkflowReply interface

// GetWorkflowContextID inherits docs from WorkflowReply.GetWorkflowContextID()
func (reply *WorkflowDescribeExecutionReply) GetWorkflowContextID() int64 {
	return reply.WorkflowReply.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowReply.GetWorkflowContextID()
func (reply *WorkflowDescribeExecutionReply) SetWorkflowContextID(value int64) {
	reply.WorkflowReply.SetWorkflowContextID(value)
}
