package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowCancelRequest is ProxyRequest of MessageType
	// WorkflowCancelRequest.
	//
	// A WorkflowCancelRequest contains a reference to a
	// ProxyRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	//
	// A WorkflowCancelRequest will pass all of the given data and options
	// necessary to cancel a cadence workflow via the cadence client
	WorkflowCancelRequest struct {
		*ProxyRequest
	}
)

// NewWorkflowCancelRequest is the default constructor for a WorkflowCancelRequest
//
// returns *WorkflowCancelRequest -> a reference to a newly initialized
// WorkflowCancelRequest in memory
func NewWorkflowCancelRequest() *WorkflowCancelRequest {
	request := new(WorkflowCancelRequest)
	request.ProxyRequest = NewProxyRequest()
	request.Type = messagetypes.WorkflowCancelRequest
	request.SetReplyType(messagetypes.WorkflowCancelReply)

	return request
}

// GetWorkflowID gets a WorkflowCancelRequest's WorkflowID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowCancelRequest's WorkflowID
func (request *WorkflowCancelRequest) GetWorkflowID() *string {
	return request.GetStringProperty("WorkflowId")
}

// SetWorkflowID sets an WorkflowCancelRequest's WorkflowID value
// in its properties map
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowCancelRequest's WorkflowID
func (request *WorkflowCancelRequest) SetWorkflowID(value *string) {
	request.SetStringProperty("WorkflowId", value)
}

// GetRunID gets a WorkflowCancelRequest's RunID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowCancelRequest's RunID
func (request *WorkflowCancelRequest) GetRunID() *string {
	return request.GetStringProperty("RunId")
}

// SetRunID sets a WorkflowCancelRequest's RunID value
// in its properties map.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowCancelRequest) SetRunID(value *string) {
	request.SetStringProperty("RunId", value)
}

// GetDomain gets a WorkflowCancelRequest's Domain value
// from its properties map. Optionally overrides the current client domain.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowCancelRequest's Domain
func (request *WorkflowCancelRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets a WorkflowCancelRequest's Domain value
// in its properties map. Optionally overrides the current client domain.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowCancelRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *WorkflowCancelRequest) Clone() IProxyMessage {
	workflowCancelRequest := NewWorkflowCancelRequest()
	var messageClone IProxyMessage = workflowCancelRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *WorkflowCancelRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*WorkflowCancelRequest); ok {
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetRunID(request.GetRunID())
		v.SetDomain(request.GetDomain())
	}
}

// SetProxyMessage inherits docs from ProxyRequest.SetProxyMessage()
func (request *WorkflowCancelRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyRequest.GetProxyMessage()
func (request *WorkflowCancelRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyRequest.GetRequestID()
func (request *WorkflowCancelRequest) GetRequestID() int64 {
	return request.ProxyRequest.GetRequestID()
}

// SetRequestID inherits docs from ProxyRequest.SetRequestID()
func (request *WorkflowCancelRequest) SetRequestID(value int64) {
	request.ProxyRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *WorkflowCancelRequest) GetReplyType() messagetypes.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *WorkflowCancelRequest) SetReplyType(value messagetypes.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ProxyRequest.GetTimeout()
func (request *WorkflowCancelRequest) GetTimeout() time.Duration {
	return request.ProxyRequest.GetTimeout()
}

// SetTimeout inherits docs from ProxyRequest.SetTimeout()
func (request *WorkflowCancelRequest) SetTimeout(value time.Duration) {
	request.ProxyRequest.SetTimeout(value)
}
