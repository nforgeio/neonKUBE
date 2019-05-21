package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowQueryRequest is WorkflowContextRequest of MessageType
	// WorkflowQueryRequest.
	//
	// A WorkflowQueryRequest contains a reference to a
	// WorkflowContextRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowContextRequest
	//
	// A WorkflowQueryRequest will pass all of the given data and options
	// necessary to query a cadence workflow via the cadence client
	WorkflowQueryRequest struct {
		*WorkflowContextRequest
	}
)

// NewWorkflowQueryRequest is the default constructor for a WorkflowQueryRequest
//
// returns *WorkflowQueryRequest -> a reference to a newly initialized
// WorkflowQueryRequest in memory
func NewWorkflowQueryRequest() *WorkflowQueryRequest {
	request := new(WorkflowQueryRequest)
	request.WorkflowContextRequest = NewWorkflowContextRequest()
	request.Type = messagetypes.WorkflowQueryRequest
	request.SetReplyType(messagetypes.WorkflowQueryReply)

	return request
}

// GetWorkflowID gets a WorkflowQueryRequest's WorkflowID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowQueryRequest's WorkflowID
func (request *WorkflowQueryRequest) GetWorkflowID() *string {
	return request.GetStringProperty("WorkflowId")
}

// SetWorkflowID sets an WorkflowQueryRequest's WorkflowID value
// in its properties map
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowQueryRequest's WorkflowID
func (request *WorkflowQueryRequest) SetWorkflowID(value *string) {
	request.SetStringProperty("WorkflowId", value)
}

// GetRunID gets a WorkflowQueryRequest's RunID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowQueryRequest's RunID
func (request *WorkflowQueryRequest) GetRunID() *string {
	return request.GetStringProperty("RunId")
}

// SetRunID sets a WorkflowQueryRequest's RunID value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowQueryRequest) SetRunID(value *string) {
	request.SetStringProperty("RunId", value)
}

// GetQueryName gets a WorkflowQueryRequest's QueryName value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowQueryRequest's QueryName
func (request *WorkflowQueryRequest) GetQueryName() *string {
	return request.GetStringProperty("QueryName")
}

// SetQueryName sets a WorkflowQueryRequest's QueryName value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowQueryRequest) SetQueryName(value *string) {
	request.SetStringProperty("QueryName", value)
}

// GetQueryArgs gets a WorkflowQueryRequest's QueryArgs field
// from its properties map.  QueryArgs is a []byte holding the arguments
// for querying a specific workflow
//
// returns []byte -> a []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowQueryRequest) GetQueryArgs() []byte {
	return request.GetBytesProperty("QueryArgs")
}

// SetQueryArgs sets an WorkflowQueryRequest's QueryArgs field
// from its properties map.  QueryArgs is a []byte holding the arguments
// for querying a specific workflow
//
// param value []byte -> []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowQueryRequest) SetQueryArgs(value []byte) {
	request.SetBytesProperty("QueryArgs", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowContextRequest.Clone()
func (request *WorkflowQueryRequest) Clone() IProxyMessage {
	workflowQueryRequest := NewWorkflowQueryRequest()
	var messageClone IProxyMessage = workflowQueryRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowContextRequest.CopyTo()
func (request *WorkflowQueryRequest) CopyTo(target IProxyMessage) {
	request.WorkflowContextRequest.CopyTo(target)
	if v, ok := target.(*WorkflowQueryRequest); ok {
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetRunID(request.GetRunID())
		v.SetQueryName(request.GetQueryName())
		v.SetQueryArgs(request.GetQueryArgs())
	}
}

// SetProxyMessage inherits docs from WorkflowContextRequest.SetProxyMessage()
func (request *WorkflowQueryRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowContextRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowContextRequest.GetProxyMessage()
func (request *WorkflowQueryRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowContextRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowContextRequest.GetRequestID()
func (request *WorkflowQueryRequest) GetRequestID() int64 {
	return request.WorkflowContextRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowContextRequest.SetRequestID()
func (request *WorkflowQueryRequest) SetRequestID(value int64) {
	request.WorkflowContextRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowContextRequest.GetReplyType()
func (request *WorkflowQueryRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowContextRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowContextRequest.SetReplyType()
func (request *WorkflowQueryRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowContextRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowContextRequest.GetTimeout()
func (request *WorkflowQueryRequest) GetTimeout() time.Duration {
	return request.WorkflowContextRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowContextRequest.SetTimeout()
func (request *WorkflowQueryRequest) SetTimeout(value time.Duration) {
	request.WorkflowContextRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowContextRequest interface methods for implementing the IWorkflowContextRequest interface

// GetWorkflowContextID inherits docs from WorkflowContextRequest.GetWorkflowContextID()
func (reply *WorkflowQueryRequest) GetWorkflowContextID() int64 {
	return reply.WorkflowContextRequest.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowContextRequest.GetWorkflowContextID()
func (reply *WorkflowQueryRequest) SetWorkflowContextID(value int64) {
	reply.WorkflowContextRequest.SetWorkflowContextID(value)
}
