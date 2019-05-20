package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowRegisterRequest is WorkflowContextRequest of MessageType
	// WorkflowRegisterRequest.
	//
	// A WorkflowRegisterRequest contains a reference to a
	// WorkflowContextRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowContextRequest
	//
	// A WorkflowRegisterRequest will pass all of the given information
	// necessary to register a workflow function with the cadence server
	// via the cadence client
	WorkflowRegisterRequest struct {
		*WorkflowContextRequest
	}
)

// NewWorkflowRegisterRequest is the default constructor for a WorkflowRegisterRequest
//
// returns *WorkflowRegisterRequest -> a reference to a newly initialized
// WorkflowRegisterRequest in memory
func NewWorkflowRegisterRequest() *WorkflowRegisterRequest {
	request := new(WorkflowRegisterRequest)
	request.WorkflowContextRequest = NewWorkflowContextRequest()
	request.Type = messagetypes.WorkflowRegisterRequest
	request.SetReplyType(messagetypes.WorkflowRegisterReply)

	return request
}

// GetName gets a WorkflowRegisterRequest's Name value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowRegisterRequest's Name
func (request *WorkflowRegisterRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a WorkflowRegisterRequest's Name value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowRegisterRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowContextRequest.Clone()
func (request *WorkflowRegisterRequest) Clone() IProxyMessage {
	workflowRegisterRequest := NewWorkflowRegisterRequest()
	var messageClone IProxyMessage = workflowRegisterRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowContextRequest.CopyTo()
func (request *WorkflowRegisterRequest) CopyTo(target IProxyMessage) {
	request.WorkflowContextRequest.CopyTo(target)
	if v, ok := target.(*WorkflowRegisterRequest); ok {
		v.SetName(request.GetName())
	}
}

// SetProxyMessage inherits docs from WorkflowContextRequest.SetProxyMessage()
func (request *WorkflowRegisterRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowContextRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowContextRequest.GetProxyMessage()
func (request *WorkflowRegisterRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowContextRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowContextRequest.GetRequestID()
func (request *WorkflowRegisterRequest) GetRequestID() int64 {
	return request.WorkflowContextRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowContextRequest.SetRequestID()
func (request *WorkflowRegisterRequest) SetRequestID(value int64) {
	request.WorkflowContextRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowContextRequest.GetReplyType()
func (request *WorkflowRegisterRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowContextRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowContextRequest.SetReplyType()
func (request *WorkflowRegisterRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowContextRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowContextRequest.GetTimeout()
func (request *WorkflowRegisterRequest) GetTimeout() time.Duration {
	return request.WorkflowContextRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowContextRequest.SetTimeout()
func (request *WorkflowRegisterRequest) SetTimeout(value time.Duration) {
	request.WorkflowContextRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowContextRequest interface methods for implementing the IWorkflowContextRequest interface

// GetWorkflowContextID inherits docs from WorkflowContextRequest.GetWorkflowContextID()
func (request *WorkflowRegisterRequest) GetWorkflowContextID() int64 {
	return request.WorkflowContextRequest.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowContextRequest.SetWorkflowContextID()
func (request *WorkflowRegisterRequest) SetWorkflowContextID(value int64) {
	request.WorkflowContextRequest.SetWorkflowContextID(value)
}
