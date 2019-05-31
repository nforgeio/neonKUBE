package messages

import (
	worker "go.uber.org/cadence/worker"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// NewWorkerRequest is ProxyRequest of MessageType
	// NewWorkerRequest.
	//
	// A NewWorkerRequest contains a reference to a
	// ProxyRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	NewWorkerRequest struct {
		*ProxyRequest
	}
)

// NewNewWorkerRequest is the default constructor for a NewWorkerRequest
//
// returns *NewWorkerRequest -> a reference to a newly initialized
// NewWorkerRequest in memory
func NewNewWorkerRequest() *NewWorkerRequest {
	request := new(NewWorkerRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(messagetypes.NewWorkerRequest)
	request.SetReplyType(messagetypes.NewWorkerReply)

	return request
}

// GetDomain gets a NewWorkerRequest's Domain value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a NewWorkerRequest's Domain
func (request *NewWorkerRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets a NewWorkerRequest's Domain value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *NewWorkerRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
}

// GetName gets a NewWorkerRequest's Name value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a NewWorkerRequest's Name
func (request *NewWorkerRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a NewWorkerRequest's Name value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *NewWorkerRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// GetIsWorkflow gets a NewWorkerRequest's IsWorkflow value
// from its properties map. Indicates whether we're starting a workflow
// or an activity worker.
//
// returns bool -> bool holding the value
// of a NewWorkerRequest's IsWorkflow
func (request *NewWorkerRequest) GetIsWorkflow() bool {
	return request.GetBoolProperty("IsWorkflow")
}

// SetIsWorkflow sets a NewWorkerRequest's IsWorkflow value
// in its properties map. Indicates whether we're starting a workflow
// or an activity worker.
//
// param value bool -> bool that holds the value
// to be set in the properties map
func (request *NewWorkerRequest) SetIsWorkflow(value bool) {
	request.SetBoolProperty("IsWorkflow", value)
}

// GetTaskList gets a NewWorkerRequest's TaskList value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a NewWorkerRequest's TaskList
func (request *NewWorkerRequest) GetTaskList() *string {
	return request.GetStringProperty("TaskList")
}

// SetTaskList sets a NewWorkerRequest's TaskList value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *NewWorkerRequest) SetTaskList(value *string) {
	request.SetStringProperty("TaskList", value)
}

// GetOptions gets a NewWorkerRequest's start options
// used to execute a cadence workflow via the cadence workflow client
//
// returns *worker.WorkerOptions -> pointer to a cadence worker struct that contains the
// options for creating a new worker
func (request *NewWorkerRequest) GetOptions() *worker.Options {
	opts := new(worker.Options)
	err := request.GetJSONProperty("Options", opts)
	if err != nil {
		return nil
	}

	return opts
}

// SetOptions sets a NewWorkerRequest's start options
// used to execute a cadence workflow via the cadence workflow client
//
// param value client.StartWorkflowOptions -> pointer to a cadence worker struct
// that contains the options for creating a new worker to be set in the NewWorkerRequest's
// properties map
func (request *NewWorkerRequest) SetOptions(value *worker.Options) {
	request.SetJSONProperty("Options", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *NewWorkerRequest) Clone() IProxyMessage {
	newWorkerRequest := NewNewWorkerRequest()
	var messageClone IProxyMessage = newWorkerRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *NewWorkerRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*NewWorkerRequest); ok {
		v.SetDomain(request.GetDomain())
		v.SetName(request.GetName())
		v.SetIsWorkflow(request.GetIsWorkflow())
		v.SetTaskList(request.GetTaskList())
		v.SetOptions(request.GetOptions())
	}
}
