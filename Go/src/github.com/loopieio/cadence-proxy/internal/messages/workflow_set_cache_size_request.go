package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowSetCacheSizeRequest is WorkflowRequest of MessageType
	// WorkflowSetCacheSizeRequest.
	//
	// A WorkflowSetCacheSizeRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowSetCacheSizeRequest sets the maximum number of bytes the client will use
	/// to cache the history of a sticky workflow on a workflow worker as a performance
	/// optimization.  When this is exceeded for a workflow, its full history will
	/// need to be retrieved from the Cadence cluster the next time the workflow
	/// instance is assigned to a worker.
	WorkflowSetCacheSizeRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowSetCacheSizeRequest is the default constructor for a WorkflowSetCacheSizeRequest
//
// returns *WorkflowSetCacheSizeRequest -> a reference to a newly initialized
// WorkflowSetCacheSizeRequest in memory
func NewWorkflowSetCacheSizeRequest() *WorkflowSetCacheSizeRequest {
	request := new(WorkflowSetCacheSizeRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowSetCacheSizeRequest)
	request.SetReplyType(messagetypes.WorkflowSetCacheSizeReply)

	return request
}

// GetSize gets a WorkflowSetCacheSizeRequest's Size value
// from its properties map.  Specifies the maximum number of bytes used for
// caching sticky workflows.
//
// returns int -> int specifying the maximum number of bytes used for caching
// sticky workflows.cache Size
func (request *WorkflowSetCacheSizeRequest) GetSize() int {
	return int(request.GetIntProperty("Size"))
}

// SetSize sets a WorkflowSetCacheSizeRequest's Size value
// in its properties map
//
// param value int -> int specifying the maximum number of bytes used for caching
// sticky workflows.cache Size
func (request *WorkflowSetCacheSizeRequest) SetSize(value int) {
	request.SetIntProperty("Size", int32(value))
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowSetCacheSizeRequest) Clone() IProxyMessage {
	workflowSetCacheSizeRequest := NewWorkflowSetCacheSizeRequest()
	var messageClone IProxyMessage = workflowSetCacheSizeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowSetCacheSizeRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSetCacheSizeRequest); ok {
		v.SetSize(request.GetSize())
	}
}

// SetProxyMessage inherits docs from WorkflowRequest.SetProxyMessage()
func (request *WorkflowSetCacheSizeRequest) SetProxyMessage(value *ProxyMessage) {
	request.WorkflowRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from WorkflowRequest.GetProxyMessage()
func (request *WorkflowSetCacheSizeRequest) GetProxyMessage() *ProxyMessage {
	return request.WorkflowRequest.GetProxyMessage()
}

// GetRequestID inherits docs from WorkflowRequest.GetRequestID()
func (request *WorkflowSetCacheSizeRequest) GetRequestID() int64 {
	return request.WorkflowRequest.GetRequestID()
}

// SetRequestID inherits docs from WorkflowRequest.SetRequestID()
func (request *WorkflowSetCacheSizeRequest) SetRequestID(value int64) {
	request.WorkflowRequest.SetRequestID(value)
}

// GetType inherits docs from WorkflowRequest.GetType()
func (request *WorkflowSetCacheSizeRequest) GetType() messagetypes.MessageType {
	return request.WorkflowRequest.GetType()
}

// SetType inherits docs from WorkflowRequest.SetType()
func (request *WorkflowSetCacheSizeRequest) SetType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from WorkflowRequest.GetReplyType()
func (request *WorkflowSetCacheSizeRequest) GetReplyType() messagetypes.MessageType {
	return request.WorkflowRequest.GetReplyType()
}

// SetReplyType inherits docs from WorkflowRequest.SetReplyType()
func (request *WorkflowSetCacheSizeRequest) SetReplyType(value messagetypes.MessageType) {
	request.WorkflowRequest.SetReplyType(value)
}

// GetTimeout inherits docs from WorkflowRequest.GetTimeout()
func (request *WorkflowSetCacheSizeRequest) GetTimeout() time.Duration {
	return request.WorkflowRequest.GetTimeout()
}

// SetTimeout inherits docs from WorkflowRequest.SetTimeout()
func (request *WorkflowSetCacheSizeRequest) SetTimeout(value time.Duration) {
	request.WorkflowRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetWorkflowContextID inherits docs from WorkflowRequest.GetWorkflowContextID()
func (request *WorkflowSetCacheSizeRequest) GetWorkflowContextID() int64 {
	return request.WorkflowRequest.GetWorkflowContextID()
}

// SetWorkflowContextID inherits docs from WorkflowRequest.GetWorkflowContextID()
func (request *WorkflowSetCacheSizeRequest) SetWorkflowContextID(value int64) {
	request.WorkflowRequest.SetWorkflowContextID(value)
}
