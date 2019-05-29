package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowMutableRequest is WorkflowRequest of MessageType
	// WorkflowMutableRequest.
	//
	// A WorkflowMutableRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowMutableRequest will pass all of the given data
	// necessary to invoke a cadence workflow instance via the cadence client
	WorkflowMutableRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowMutableRequest is the default constructor for a WorkflowMutableRequest
//
// returns *WorkflowMutableRequest -> a reference to a newly initialized
// WorkflowMutableRequest in memory
func NewWorkflowMutableRequest() *WorkflowMutableRequest {
	request := new(WorkflowMutableRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowMutableRequest)
	request.SetReplyType(messagetypes.WorkflowMutableReply)

	return request
}

// GetMutableID gets a WorkflowMutableRequest's MutableID value
// from its properties map. Identifies the mutable value to be returned.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowMutableRequest's MutableID
func (request *WorkflowMutableRequest) GetMutableID() *string {
	return request.GetStringProperty("MutableId")
}

// SetMutableID sets an WorkflowMutableRequest's MutableID value
// in its properties map. Identifies the mutable value to be returned.
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowMutableRequest's MutableID
func (request *WorkflowMutableRequest) SetMutableID(value *string) {
	request.SetStringProperty("MutableId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowMutableRequest) Clone() IProxyMessage {
	workflowMutableRequest := NewWorkflowMutableRequest()
	var messageClone IProxyMessage = workflowMutableRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowMutableRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowMutableRequest); ok {
		v.SetMutableID(request.GetMutableID())
	}
}

// SetProxyMessage inherits docs from WorkflowRequest.SetProxyMessage()
func (request *WorkflowMutableRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowRequest.GetProxyMessage()
func (request *WorkflowMutableRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowRequest.GetRequestID()
func (request *WorkflowMutableRequest) GetRequestID() int64 {
	return request.WorkflowRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowRequest.SetRequestID()
func (request *WorkflowMutableRequest) SetRequestID(value int64) {
	request.WorkflowRequest.SetRequestID(value)
}

// GetType inherits docs from WorkflowRequest.GetType()
func (request *WorkflowMutableRequest) GetType() messagetypes.MessageType {
	return request.WorkflowRequest.GetType()
}

// SetType inherits docs from WorkflowRequest.SetType()
func (request *WorkflowMutableRequest) SetType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowRequest.GetReplyType()
func (request *WorkflowMutableRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowRequest.SetReplyType()
func (request *WorkflowMutableRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowRequest.GetTimeout()
func (request *WorkflowMutableRequest) GetTimeout() time.Duration {
	return request.WorkflowRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowRequest.SetTimeout()
func (request *WorkflowMutableRequest) SetTimeout(value time.Duration) {
	request.WorkflowRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowMutableRequest) GetContextID() int64 {
	return request.WorkflowRequest.GetContextID()
}

// SetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowMutableRequest) SetContextID(value int64) {
	request.WorkflowRequest.SetContextID(value)
}
