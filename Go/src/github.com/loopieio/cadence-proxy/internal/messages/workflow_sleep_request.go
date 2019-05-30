package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSleepRequest is WorkflowRequest of MessageType
	// WorkflowSleepRequest.
	//
	// A WorkflowSleepRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Commands the workflow to sleep for a period of time.
	WorkflowSleepRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowSleepRequest is the default constructor for a WorkflowSleepRequest
//
// returns *WorkflowSleepRequest -> a reference to a newly initialized
// WorkflowSleepRequest in memory
func NewWorkflowSleepRequest() *WorkflowSleepRequest {
	request := new(WorkflowSleepRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowSleepRequest)
	request.SetReplyType(messagetypes.WorkflowSleepReply)

	return request
}

// GetDuration gets the Duration property from the WorkflowSleepRequest's
// properties map. Duration specifies the time to sleep.
//
// returns time.Duration -> the value of the Duration property from
// the WorkflowSleepRequest's properties map.
func (request *WorkflowSleepRequest) GetDuration() time.Duration {
	return request.GetTimeSpanProperty("Duration")
}

// SetDuration sets the Duration property in the WorkflowSleepRequest's
// properties map. Duration specifies the time to sleep.
//
// param value time.Duration -> the time.Duration to be set in the
// WorkflowSleepRequest's properties map.
func (request *WorkflowSleepRequest) SetDuration(value time.Duration) {
	request.SetTimeSpanProperty("Duration", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowSleepRequest) Clone() IProxyMessage {
	workflowSleepRequest := NewWorkflowSleepRequest()
	var messageClone IProxyMessage = workflowSleepRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowSleepRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSleepRequest); ok {
		v.SetDuration(request.GetDuration())
	}
}

// SetProxyMessage inherits docs from WorkflowRequest.SetProxyMessage()
func (request *WorkflowSleepRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowRequest.GetProxyMessage()
func (request *WorkflowSleepRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowRequest.GetRequestID()
func (request *WorkflowSleepRequest) GetRequestID() int64 {
	return request.WorkflowRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowRequest.SetRequestID()
func (request *WorkflowSleepRequest) SetRequestID(value int64) {
	request.WorkflowRequest.SetRequestID(value)
}

// GetType inherits docs from WorkflowRequest.GetType()
func (request *WorkflowSleepRequest) GetType() messagetypes.MessageType {
	return request.WorkflowRequest.GetType()
}

// SetType inherits docs from WorkflowRequest.SetType()
func (request *WorkflowSleepRequest) SetType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowRequest.GetReplyType()
func (request *WorkflowSleepRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowRequest.SetReplyType()
func (request *WorkflowSleepRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowRequest.GetTimeout()
func (request *WorkflowSleepRequest) GetTimeout() time.Duration {
	return request.WorkflowRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowRequest.SetTimeout()
func (request *WorkflowSleepRequest) SetTimeout(value time.Duration) {
	request.WorkflowRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowSleepRequest) GetContextID() int64 {
	return request.WorkflowRequest.GetContextID()
}

// SetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowSleepRequest) SetContextID(value int64) {
	request.WorkflowRequest.SetContextID(value)
}
