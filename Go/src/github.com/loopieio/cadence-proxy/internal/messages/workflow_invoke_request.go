package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowInvokeRequest is WorkflowRequest of MessageType
	// WorkflowInvokeRequest.
	//
	// A WorkflowInvokeRequest contains a RequestId and a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowInvokeRequest will pass all of the given information
	// necessary to invoke a cadence workflow via the cadence client
	WorkflowInvokeRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowInvokeRequest is the default constructor for a WorkflowInvokeRequest
//
// returns *WorkflowInvokeRequest -> a reference to a newly initialized
// WorkflowInvokeRequest in memory
func NewWorkflowInvokeRequest() *WorkflowInvokeRequest {
	request := new(WorkflowInvokeRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowInvokeRequest)
	request.SetReplyType(messagetypes.WorkflowInvokeReply)

	return request
}

// GetName gets a WorkflowInvokeRequest's Name value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowInvokeRequest's Name
func (request *WorkflowInvokeRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a WorkflowInvokeRequest's Name value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowInvokeRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// GetArgs gets a WorkflowInvokeRequest's Args field
// from its properties map.  Args is a []byte holding the arguments
// for invoking a specific workflow
//
// returns []byte -> a []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowInvokeRequest) GetArgs() []byte {
	return request.GetBytesProperty("Args")
}

// SetArgs sets an WorkflowInvokeRequest's Args field
// from its properties map.  Args is a []byte holding the arguments
// for invoking a specific workflow
//
// param value []byte -> []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowInvokeRequest) SetArgs(value []byte) {
	request.SetBytesProperty("Args", value)
}

// GetWorkflowID gets a WorkflowInvokeRequest's WorkflowID value
// from its properties map. The original workflow ID.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowInvokeRequest's WorkflowID
func (request *WorkflowInvokeRequest) GetWorkflowID() *string {
	return request.GetStringProperty("WorkflowId")
}

// SetWorkflowID sets an WorkflowInvokeRequest's WorkflowID value
// in its properties map. The original workflow ID.
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowInvokeRequest's WorkflowID
func (request *WorkflowInvokeRequest) SetWorkflowID(value *string) {
	request.SetStringProperty("WorkflowId", value)
}

// GetWorkflowType gets a WorkflowInvokeRequest's WorkflowType value
// from its properties map. The original workflow Type.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowInvokeRequest's WorkflowType
func (request *WorkflowInvokeRequest) GetWorkflowType() *string {
	return request.GetStringProperty("WorkflowType")
}

// SetWorkflowType sets an WorkflowInvokeRequest's WorkflowType value
// in its properties map. The original workflow Type.
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowInvokeRequest's WorkflowType
func (request *WorkflowInvokeRequest) SetWorkflowType(value *string) {
	request.SetStringProperty("WorkflowType", value)
}

// GetRunID gets a WorkflowInvokeRequest's RunID value
// from its properties map. The workflow run ID.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowInvokeRequest's RunID
func (request *WorkflowInvokeRequest) GetRunID() *string {
	return request.GetStringProperty("RunId")
}

// SetRunID sets a WorkflowInvokeRequest's RunID value
// in its properties map. The workflow run ID.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowInvokeRequest) SetRunID(value *string) {
	request.SetStringProperty("RunId", value)
}

// GetDomain gets a WorkflowInvokeRequest's Domain value
// from its properties map. The domain where the workflow is executing.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowInvokeRequest's Domain
func (request *WorkflowInvokeRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets a WorkflowInvokeRequest's Domain value
// in its properties map. The domain where the workflow is executing.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowInvokeRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
}

// GetTaskList gets a WorkflowInvokeRequest's TaskList value
// from its properties map. The tasklist where the workflow is executing.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowInvokeRequest's TaskList
func (request *WorkflowInvokeRequest) GetTaskList() *string {
	return request.GetStringProperty("TaskList")
}

// SetTaskList sets a WorkflowInvokeRequest's TaskList value
// in its properties map. The tasklist where the workflow is executing.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowInvokeRequest) SetTaskList(value *string) {
	request.SetStringProperty("TaskList", value)
}

// GetExecutionStartToCloseTimeout gets a WorkflowInvokeRequest's
// ExecutionStartToCloseTimeout property in its properties map.
// This is the The maximum duration the workflow is allowed to run.
//
// returns time.Duration -> the The maximum duration the workflow is allowed to run
func (request *WorkflowInvokeRequest) GetExecutionStartToCloseTimeout() time.Duration {
	return request.GetTimeSpanProperty("ExecutionStartToCloseTimeout")
}

// SetExecutionStartToCloseTimeout sets a WorkflowInvokeRequest's
// ExecutionStartToCloseTimeout property in its properties map.
// This is the The maximum duration the workflow is allowed to run.
//
// param value time.Duration -> the The maximum duration the workflow is allowed to run
func (request *WorkflowInvokeRequest) SetExecutionStartToCloseTimeout(value time.Duration) {
	request.SetTimeSpanProperty("ExecutionStartToCloseTimeout", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowInvokeRequest) Clone() IProxyMessage {
	workflowInvokeRequest := NewWorkflowInvokeRequest()
	var messageClone IProxyMessage = workflowInvokeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowInvokeRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowInvokeRequest); ok {
		v.SetName(request.GetName())
		v.SetArgs(request.GetArgs())
		v.SetDomain(request.GetDomain())
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetWorkflowType(request.GetWorkflowType())
		v.SetRunID(request.GetRunID())
		v.SetTaskList(request.GetTaskList())
		v.SetExecutionStartToCloseTimeout(request.GetExecutionStartToCloseTimeout())
	}
}

// SetProxyMessage inherits docs from WorkflowRequest.SetProxyMessage()
func (request *WorkflowInvokeRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowRequest.GetProxyMessage()
func (request *WorkflowInvokeRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowRequest.GetRequestID()
func (request *WorkflowInvokeRequest) GetRequestID() int64 {
	return request.WorkflowRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowRequest.SetRequestID()
func (request *WorkflowInvokeRequest) SetRequestID(value int64) {
	request.WorkflowRequest.SetRequestID(value)
}

// GetType inherits docs from WorkflowRequest.GetType()
func (request *WorkflowInvokeRequest) GetType() messagetypes.MessageType {
	return request.WorkflowRequest.GetType()
}

// SetType inherits docs from WorkflowRequest.SetType()
func (request *WorkflowInvokeRequest) SetType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowRequest.GetReplyType()
func (request *WorkflowInvokeRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowRequest.SetReplyType()
func (request *WorkflowInvokeRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowRequest.GetTimeout()
func (request *WorkflowInvokeRequest) GetTimeout() time.Duration {
	return request.WorkflowRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowRequest.SetTimeout()
func (request *WorkflowInvokeRequest) SetTimeout(value time.Duration) {
	request.WorkflowRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetContextID inherits docs from WorkflowRequest.GetContextID()
func (request *WorkflowInvokeRequest) GetContextID() int64 {
	return request.WorkflowRequest.GetContextID()
}

// SetContextID inherits docs from WorkflowRequest.SetContextID()
func (request *WorkflowInvokeRequest) SetContextID(value int64) {
	request.WorkflowRequest.SetContextID(value)
}
