package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowTerminateRequest is WorkflowRequest of MessageType
	// WorkflowTerminateRequest.
	//
	// A WorkflowTerminateRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowTerminateRequest will pass all of the given data and options
	// necessary to termainte a cadence workflow via the cadence client
	WorkflowTerminateRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowTerminateRequest is the default constructor for a WorkflowTerminateRequest
//
// returns *WorkflowTerminateRequest -> a reference to a newly initialized
// WorkflowTerminateRequest in memory
func NewWorkflowTerminateRequest() *WorkflowTerminateRequest {
	request := new(WorkflowTerminateRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.Type = messagetypes.WorkflowTerminateRequest
	request.SetReplyType(messagetypes.WorkflowTerminateReply)

	return request
}

// GetWorkflowID gets a WorkflowTerminateRequest's WorkflowID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowTerminateRequest's WorkflowID
func (request *WorkflowTerminateRequest) GetWorkflowID() *string {
	return request.GetStringProperty("WorkflowId")
}

// SetWorkflowID sets an WorkflowTerminateRequest's WorkflowID value
// in its properties map
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowTerminateRequest's WorkflowID
func (request *WorkflowTerminateRequest) SetWorkflowID(value *string) {
	request.SetStringProperty("WorkflowId", value)
}

// GetRunID gets a WorkflowTerminateRequest's RunID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowTerminateRequest's RunID
func (request *WorkflowTerminateRequest) GetRunID() *string {
	return request.GetStringProperty("RunId")
}

// SetRunID sets a WorkflowTerminateRequest's RunID value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowTerminateRequest) SetRunID(value *string) {
	request.SetStringProperty("RunId", value)
}

// GetReason gets a WorkflowTerminateRequest's Reason value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowTerminateRequest's Reason
func (request *WorkflowTerminateRequest) GetReason() *string {
	return request.GetStringProperty("Reason")
}

// SetReason sets a WorkflowTerminateRequest's Reason value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowTerminateRequest) SetReason(value *string) {
	request.SetStringProperty("Reason", value)
}

// GetDetails gets a WorkflowTerminateRequest's Details field
// from its properties map.  Details is a []byte holding the details for
// terminating a workflow
//
// returns []byte -> a []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowTerminateRequest) GetDetails() []byte {
	return request.GetBytesProperty("Details")
}

// SetDetails sets an WorkflowTerminateRequest's Details field
// from its properties map.  Details is a []byte holding the details for
// terminating a workflow
//
// param value []byte -> []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowTerminateRequest) SetDetails(value []byte) {
	request.SetBytesProperty("Details", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowTerminateRequest) Clone() IProxyMessage {
	workflowTerminateRequest := NewWorkflowTerminateRequest()
	var messageClone IProxyMessage = workflowTerminateRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowTerminateRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowTerminateRequest); ok {
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetRunID(request.GetRunID())
		v.SetReason(request.GetReason())
		v.SetDetails(request.GetDetails())
	}
}

// SetProxyMessage inherits docs from WorkflowRequest.SetProxyMessage()
func (request *WorkflowTerminateRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowRequest.GetProxyMessage()
func (request *WorkflowTerminateRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowRequest.GetRequestID()
func (request *WorkflowTerminateRequest) GetRequestID() int64 {
	return request.WorkflowRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowRequest.SetRequestID()
func (request *WorkflowTerminateRequest) SetRequestID(value int64) {
	request.WorkflowRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowRequest.GetReplyType()
func (request *WorkflowTerminateRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowRequest.SetReplyType()
func (request *WorkflowTerminateRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowRequest.GetTimeout()
func (request *WorkflowTerminateRequest) GetTimeout() time.Duration {
	return request.WorkflowRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowRequest.SetTimeout()
func (request *WorkflowTerminateRequest) SetTimeout(value time.Duration) {
	request.WorkflowRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetWorkflowContextID inherits docs from WorkflowRequest.GetWorkflowContextID()
func (request *WorkflowTerminateRequest) GetWorkflowContextID() int64 {
	return request.WorkflowRequest.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowRequest.GetWorkflowContextID()
func (request *WorkflowTerminateRequest) SetWorkflowContextID(value int64) {
	request.WorkflowRequest.SetWorkflowContextID(value)
}
