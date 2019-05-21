package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowCancelRequest is WorkflowContextRequest of MessageType
	// WorkflowCancelRequest.
	//
	// A WorkflowCancelRequest contains a reference to a
	// WorkflowContextRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowContextRequest
	//
	// A WorkflowCancelRequest will pass all of the given data and options
	// necessary to cancel a cadence workflow via the cadence client
	WorkflowCancelRequest struct {
		*WorkflowContextRequest
	}
)

// NewWorkflowCancelRequest is the default constructor for a WorkflowCancelRequest
//
// returns *WorkflowCancelRequest -> a reference to a newly initialized
// WorkflowCancelRequest in memory
func NewWorkflowCancelRequest() *WorkflowCancelRequest {
	request := new(WorkflowCancelRequest)
	request.WorkflowContextRequest = NewWorkflowContextRequest()
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

// Clone inherits docs from WorkflowContextRequest.Clone()
func (request *WorkflowCancelRequest) Clone() IProxyMessage {
	workflowCancelRequest := NewWorkflowCancelRequest()
	var messageClone IProxyMessage = workflowCancelRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowContextRequest.CopyTo()
func (request *WorkflowCancelRequest) CopyTo(target IProxyMessage) {
	request.WorkflowContextRequest.CopyTo(target)
	if v, ok := target.(*WorkflowCancelRequest); ok {
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetRunID(request.GetRunID())
		v.SetDomain(request.GetDomain())
	}
}

// SetProxyMessage inherits docs from WorkflowContextRequest.SetProxyMessage()
func (request *WorkflowCancelRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowContextRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowContextRequest.GetProxyMessage()
func (request *WorkflowCancelRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowContextRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowContextRequest.GetRequestID()
func (request *WorkflowCancelRequest) GetRequestID() int64 {
	return request.WorkflowContextRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowContextRequest.SetRequestID()
func (request *WorkflowCancelRequest) SetRequestID(value int64) {
	request.WorkflowContextRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowContextRequest.GetReplyType()
func (request *WorkflowCancelRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowContextRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowContextRequest.SetReplyType()
func (request *WorkflowCancelRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowContextRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowContextRequest.GetTimeout()
func (request *WorkflowCancelRequest) GetTimeout() time.Duration {
	return request.WorkflowContextRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowContextRequest.SetTimeout()
func (request *WorkflowCancelRequest) SetTimeout(value time.Duration) {
	request.WorkflowContextRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowContextRequest interface methods for implementing the IWorkflowContextRequest interface

// GetWorkflowContextID inherits docs from WorkflowContextRequest.GetWorkflowContextID()
func (reply *WorkflowCancelRequest) GetWorkflowContextID() int64 {
	return reply.WorkflowContextRequest.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowContextRequest.GetWorkflowContextID()
func (reply *WorkflowCancelRequest) SetWorkflowContextID(value int64) {
	reply.WorkflowContextRequest.SetWorkflowContextID(value)
}
