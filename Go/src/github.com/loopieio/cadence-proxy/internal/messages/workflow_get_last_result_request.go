package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowGetLastResultRequest is WorkflowRequest of MessageType
	// WorkflowGetLastResultRequest.
	//
	// A WorkflowGetLastResultRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowGetLastResultRequest returns the result from the last execution of the workflow.
	///  This can be used by CRON workflows to retrieve state from the last workflow run.
	WorkflowGetLastResultRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowGetLastResultRequest is the default constructor for a WorkflowGetLastResultRequest
//
// returns *WorkflowGetLastResultRequest -> a reference to a newly initialized
// WorkflowGetLastResultRequest in memory
func NewWorkflowGetLastResultRequest() *WorkflowGetLastResultRequest {
	request := new(WorkflowGetLastResultRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowGetLastResultRequest)
	request.SetReplyType(messagetypes.WorkflowGetLastResultReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowGetLastResultRequest) Clone() IProxyMessage {
	workflowGetLastResultRequest := NewWorkflowGetLastResultRequest()
	var messageClone IProxyMessage = workflowGetLastResultRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowGetLastResultRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
}

// SetProxyMessage inherits docs from WorkflowRequest.SetProxyMessage()
func (request *WorkflowGetLastResultRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowRequest.GetProxyMessage()
func (request *WorkflowGetLastResultRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowRequest.GetRequestID()
func (request *WorkflowGetLastResultRequest) GetRequestID() int64 {
	return request.WorkflowRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowRequest.SetRequestID()
func (request *WorkflowGetLastResultRequest) SetRequestID(value int64) {
	request.WorkflowRequest.SetRequestID(value)
}

// GetType inherits docs from WorkflowRequest.GetType()
func (request *WorkflowGetLastResultRequest) GetType() messagetypes.MessageType {
	return request.WorkflowRequest.GetType()
}

// SetType inherits docs from WorkflowRequest.SetType()
func (request *WorkflowGetLastResultRequest) SetType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowRequest.GetReplyType()
func (request *WorkflowGetLastResultRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowRequest.SetReplyType()
func (request *WorkflowGetLastResultRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowRequest.GetTimeout()
func (request *WorkflowGetLastResultRequest) GetTimeout() time.Duration {
	return request.WorkflowRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowRequest.SetTimeout()
func (request *WorkflowGetLastResultRequest) SetTimeout(value time.Duration) {
	request.WorkflowRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowGetLastResultRequest) GetContextID() int64 {
	return request.WorkflowRequest.GetContextID()
}

// SetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowGetLastResultRequest) SetContextID(value int64) {
	request.WorkflowRequest.SetContextID(value)
}
