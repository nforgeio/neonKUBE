package messages

import (
	cadenceshared "go.uber.org/cadence/.gen/go/shared"

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

// GetDetails gets the WorkflowDescribeExecutionReply's Details property from its
// properties map.  Details is the cadence are the DescribeWorkflowExecutionResponse
// to a DescribeWorkflowExecutionRequest
//
// returns *workflow.DescribeWorkflowExecutionResponse -> the response to the cadence workflow
// describe execution request
func (reply *WorkflowDescribeExecutionReply) GetDetails() *cadenceshared.DescribeWorkflowExecutionResponse {
	resp := new(cadenceshared.DescribeWorkflowExecutionResponse)
	err := reply.GetJSONProperty("Details", resp)
	if err != nil {
		return nil
	}

	return resp
}

// SetDetails sets the WorkflowDescribeExecutionReply's Details property in its
// properties map.  Details is the cadence are the DescribeWorkflowExecutionResponse
// to a DescribeWorkflowExecutionRequest
//
// param value *workflow.DescribeWorkflowExecutionResponse -> the response to the cadence workflow
// describe execution request to be set in the WorkflowDescribeExecutionReply's properties map
func (reply *WorkflowDescribeExecutionReply) SetDetails(value *cadenceshared.DescribeWorkflowExecutionResponse) {
	reply.SetJSONProperty("Details", value)
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
	if v, ok := target.(*WorkflowDescribeExecutionReply); ok {
		v.SetDetails(reply.GetDetails())
	}
}
