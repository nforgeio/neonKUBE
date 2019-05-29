package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowHasLastResultRequest is WorkflowRequest of MessageType
	// WorkflowHasLastResultRequest.
	//
	// A WorkflowHasLastResultRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowHasLastResultRequest determines whether the last execution of the workflow has
	// a completion result.  This can be used by CRON workflows to determine whether the
	// last run returned a result.
	WorkflowHasLastResultRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowHasLastResultRequest is the default constructor for a WorkflowHasLastResultRequest
//
// returns *WorkflowHasLastResultRequest -> a reference to a newly initialized
// WorkflowHasLastResultRequest in memory
func NewWorkflowHasLastResultRequest() *WorkflowHasLastResultRequest {
	request := new(WorkflowHasLastResultRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowHasLastResultRequest)
	request.SetReplyType(messagetypes.WorkflowHasLastResultReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowHasLastResultRequest) Clone() IProxyMessage {
	workflowHasLastResultRequest := NewWorkflowHasLastResultRequest()
	var messageClone IProxyMessage = workflowHasLastResultRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowHasLastResultRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
}

// SetProxyMessage inherits docs from WorkflowRequest.SetProxyMessage()
func (request *WorkflowHasLastResultRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowRequest.GetProxyMessage()
func (request *WorkflowHasLastResultRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowRequest.GetRequestID()
func (request *WorkflowHasLastResultRequest) GetRequestID() int64 {
	return request.WorkflowRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowRequest.SetRequestID()
func (request *WorkflowHasLastResultRequest) SetRequestID(value int64) {
	request.WorkflowRequest.SetRequestID(value)
}

// GetType inherits docs from WorkflowRequest.GetType()
func (request *WorkflowHasLastResultRequest) GetType() messagetypes.MessageType {
	return request.WorkflowRequest.GetType()
}

// SetType inherits docs from WorkflowRequest.SetType()
func (request *WorkflowHasLastResultRequest) SetType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowRequest.GetReplyType()
func (request *WorkflowHasLastResultRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowRequest.SetReplyType()
func (request *WorkflowHasLastResultRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowRequest.GetTimeout()
func (request *WorkflowHasLastResultRequest) GetTimeout() time.Duration {
	return request.WorkflowRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowRequest.SetTimeout()
func (request *WorkflowHasLastResultRequest) SetTimeout(value time.Duration) {
	request.WorkflowRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowHasLastResultRequest) GetContextID() int64 {
	return request.WorkflowRequest.GetContextID()
}

// SetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowHasLastResultRequest) SetContextID(value int64) {
	request.WorkflowRequest.SetContextID(value)
}
