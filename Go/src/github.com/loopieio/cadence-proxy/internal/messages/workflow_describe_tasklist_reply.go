package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowDescribeTaskListReply is a WorkflowReply of MessageType
	// WorkflowDescribeTaskListReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowDescribeTaskListRequest
	WorkflowDescribeTaskListReply struct {
		*WorkflowReply
	}
)

// NewWorkflowDescribeTaskListReply is the default constructor for
// a WorkflowDescribeTaskListReply
//
// returns *WorkflowDescribeTaskListReply -> a pointer to a newly initialized
// WorkflowDescribeTaskListReply in memory
func NewWorkflowDescribeTaskListReply() *WorkflowDescribeTaskListReply {
	reply := new(WorkflowDescribeTaskListReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.Type = messagetypes.WorkflowDescribeTaskListReply

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowDescribeTaskListReply) Clone() IProxyMessage {
	workflowDescribeTaskListReply := NewWorkflowDescribeTaskListReply()
	var messageClone IProxyMessage = workflowDescribeTaskListReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowDescribeTaskListReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
}

// SetProxyMessage inherits docs from WorkflowReply.SetProxyMessage()
func (reply *WorkflowDescribeTaskListReply) SetProxyMessage(value *ProxyMessage) {
	reply.WorkflowReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowReply.GetProxyMessage()
func (reply *WorkflowDescribeTaskListReply) GetProxyMessage() *ProxyMessage {
	return reply.WorkflowReply.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowReply.GetRequestID()
func (reply *WorkflowDescribeTaskListReply) GetRequestID() int64 {
	return reply.WorkflowReply.GetRequestID()
}

// SetRequestID inherits docs from WorkflowReply.SetRequestID()
func (reply *WorkflowDescribeTaskListReply) SetRequestID(value int64) {
	reply.WorkflowReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from WorkflowReply.GetError()
func (reply *WorkflowDescribeTaskListReply) GetError() *cadenceerrors.CadenceError {
	return reply.WorkflowReply.GetError()
}

// SetError inherits docs from WorkflowReply.SetError()
func (reply *WorkflowDescribeTaskListReply) SetError(value *cadenceerrors.CadenceError) {
	reply.WorkflowReply.SetError(value)
}

// -------------------------------------------------------------------------
// IWorkflowReply interface methods for implementing the IWorkflowReply interface

// GetWorkflowContextID inherits docs from WorkflowReply.GetWorkflowContextID()
func (reply *WorkflowDescribeTaskListReply) GetWorkflowContextID() int64 {
	return reply.WorkflowReply.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowReply.GetWorkflowContextID()
func (reply *WorkflowDescribeTaskListReply) SetWorkflowContextID(value int64) {
	reply.WorkflowReply.SetWorkflowContextID(value)
}
