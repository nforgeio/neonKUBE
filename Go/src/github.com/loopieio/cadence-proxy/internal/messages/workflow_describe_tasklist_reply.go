package messages

import (
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
	reply.SetType(messagetypes.WorkflowDescribeTaskListReply)

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
