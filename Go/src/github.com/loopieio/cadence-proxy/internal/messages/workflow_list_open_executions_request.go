package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowListOpenExecutionsRequest is WorkflowRequest of MessageType
	// WorkflowListOpenExecutionsRequest.
	//
	// A WorkflowListOpenExecutionsRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowListOpenExecutionsRequest will pass all of the given data and options
	// necessary to list open cadence workflow executions
	WorkflowListOpenExecutionsRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowListOpenExecutionsRequest is the default constructor for a WorkflowListOpenExecutionsRequest
//
// returns *WorkflowListOpenExecutionsRequest -> a reference to a newly initialized
// WorkflowListOpenExecutionsRequest in memory
func NewWorkflowListOpenExecutionsRequest() *WorkflowListOpenExecutionsRequest {
	request := new(WorkflowListOpenExecutionsRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowListOpenExecutionsRequest)
	request.SetReplyType(messagetypes.WorkflowListOpenExecutionsReply)

	return request
}

// GetMaximumPageSize gets a WorkflowListOpenExecutionsRequest's MaximumPageSize value
// from its properties map.
//
// returns int32 -> int32 holding the value
// of a WorkflowListOpenExecutionsRequest's MaximumPageSize
func (request *WorkflowListOpenExecutionsRequest) GetMaximumPageSize() int32 {
	return request.GetIntProperty("MaximumPageSize")
}

// SetMaximumPageSize sets a WorkflowListOpenExecutionsRequest's MaximumPageSize value
// in its properties map.
//
// param value int32 -> int32 holding the value
// to be set in the properties map
func (request *WorkflowListOpenExecutionsRequest) SetMaximumPageSize(value int32) {
	request.SetIntProperty("MaximumPageSize", value)
}

// GetDomain gets a WorkflowListOpenExecutionsRequest's Domain value
// from its properties map. Optionally overrides the current client domain.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowListOpenExecutionsRequest's Domain
func (request *WorkflowListOpenExecutionsRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets a WorkflowListOpenExecutionsRequest's Domain value
// in its properties map. Optionally overrides the current client domain.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowListOpenExecutionsRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowListOpenExecutionsRequest) Clone() IProxyMessage {
	workflowListOpenExecutionsRequest := NewWorkflowListOpenExecutionsRequest()
	var messageClone IProxyMessage = workflowListOpenExecutionsRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowListOpenExecutionsRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowListOpenExecutionsRequest); ok {
		v.SetMaximumPageSize(request.GetMaximumPageSize())
		v.SetDomain(request.GetDomain())
	}
}

// SetProxyMessage inherits docs from WorkflowRequest.SetProxyMessage()
func (request *WorkflowListOpenExecutionsRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowRequest.GetProxyMessage()
func (request *WorkflowListOpenExecutionsRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowRequest.GetRequestID()
func (request *WorkflowListOpenExecutionsRequest) GetRequestID() int64 {
	return request.WorkflowRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowRequest.SetRequestID()
func (request *WorkflowListOpenExecutionsRequest) SetRequestID(value int64) {
	request.WorkflowRequest.SetRequestID(value)
}

// GetType inherits docs from WorkflowRequest.GetType()
func (request *WorkflowListOpenExecutionsRequest) GetType() messagetypes.MessageType {
	return request.WorkflowRequest.GetType()
}

// SetType inherits docs from WorkflowRequest.SetType()
func (request *WorkflowListOpenExecutionsRequest) SetType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowRequest.GetReplyType()
func (request *WorkflowListOpenExecutionsRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowRequest.SetReplyType()
func (request *WorkflowListOpenExecutionsRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowRequest.GetTimeout()
func (request *WorkflowListOpenExecutionsRequest) GetTimeout() time.Duration {
	return request.WorkflowRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowRequest.SetTimeout()
func (request *WorkflowListOpenExecutionsRequest) SetTimeout(value time.Duration) {
	request.WorkflowRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowListOpenExecutionsRequest) GetContextID() int64 {
	return request.WorkflowRequest.GetContextID()
}

// SetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowListOpenExecutionsRequest) SetContextID(value int64) {
	request.WorkflowRequest.SetContextID(value)
}
