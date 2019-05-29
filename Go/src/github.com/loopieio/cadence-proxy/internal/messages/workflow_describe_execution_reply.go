package messages

import (
	cadenceshared "go.uber.org/cadence/.gen/go/shared"

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

// GetContextID inherits docs from WorkflowReply.GetContextID()
func (reply *WorkflowDescribeExecutionReply) GetContextID() int64 {
	return reply.WorkflowReply.GetContextID()
}

// SetContextID inherits docs from WorkflowReply.GetContextID()
func (reply *WorkflowDescribeExecutionReply) SetContextID(value int64) {
	reply.WorkflowReply.SetContextID(value)
}
