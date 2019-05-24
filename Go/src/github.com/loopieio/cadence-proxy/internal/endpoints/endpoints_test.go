package endpoints_test

import (
	"bytes"
	"fmt"
	"net/http"
	"testing"
	"time"

	"go.uber.org/cadence/worker"
	"go.uber.org/zap"

	"github.com/stretchr/testify/suite"

	"github.com/loopieio/cadence-proxy/internal/endpoints"
	"github.com/loopieio/cadence-proxy/internal/logger"
	"github.com/loopieio/cadence-proxy/internal/messages"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
	"github.com/loopieio/cadence-proxy/internal/server"
)

var (
	testLogger *zap.Logger
)

type (
	UnitTestSuite struct {
		suite.Suite
		instance *server.Instance
	}
)

const (
	_listenAddress = "127.0.0.1:5000"
)

// --------------------------------------------------------------------------
// Test suite methods.  Set up the test suite and entrypoint for test suite

func TestUnitTestSuite(t *testing.T) {

	// setup the suite
	s := new(UnitTestSuite)
	s.setupTestSuiteServer()

	// start the server as a go routine
	go s.instance.Start()

	// run the tests
	suite.Run(t, s)

	// send the server shutdown signal and wait
	// to exit until the server shuts down gracefully
	s.instance.ShutdownChannel <- true
	time.Sleep(time.Second * 1)
}

func (s *UnitTestSuite) setupTestSuiteServer() {

	// set the logger
	logger.SetLogger("debug", true)
	testLogger = zap.L()

	// create the new server instance,
	// set the routes, and start the server listening
	// on host:port 127.0.0.1:5000
	s.instance = server.NewInstance(_listenAddress)
	s.instance.Logger = zap.L()
	endpoints.Instance = s.instance
	endpoints.SetupRoutes(s.instance.Router)
	endpoints.ReplyAddress = "http://127.0.0.1:5000/"
	endpoints.TestMode = true
}

// --------------------------------------------------------------------------
// Test all implemented message types

func (s *UnitTestSuite) messageToConnection(message messages.IProxyMessage) (sc int, e error) {
	proxyMessage := message.GetProxyMessage()
	content, err := proxyMessage.Serialize(false)
	if err != nil {
		return http.StatusBadRequest, err
	}

	buf := bytes.NewBuffer(content)
	address := fmt.Sprintf("http://%s/", _listenAddress)
	req, err := http.NewRequest(http.MethodPut, address, buf)
	if err != nil {
		return http.StatusInternalServerError, err
	}

	// set the request header to specified content type
	req.Header.Set("Content-Type", endpoints.ContentType)

	// initialize the http.Client and send the request
	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {
		return http.StatusInternalServerError, err
	}
	defer func() {
		err := resp.Body.Close()
		if err != nil {
			sc = http.StatusInternalServerError
			e = err
		}
	}()

	return http.StatusOK, nil
}

func (s *UnitTestSuite) TestRegisterWorkflow() {

	// ConnectRequest
	var connectRequest messages.IProxyMessage = messages.NewConnectRequest()
	if v, ok := connectRequest.(*messages.ConnectRequest); ok {
		s.Equal(messagetypes.ConnectReply, v.GetReplyType())
	}

	proxyMessage := connectRequest.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	connectRequest, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(connectRequest)

	if v, ok := connectRequest.(*messages.ConnectRequest); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetEndpoints())
		s.Nil(v.GetIdentity())
		s.Equal(time.Second*30, v.GetClientTimeout())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		endpointsStr := "127.0.0.1:7933"
		v.SetEndpoints(&endpointsStr)
		s.Equal("127.0.0.1:7933", *v.GetEndpoints())

		identityStr := "my-identity"
		v.SetIdentity(&identityStr)
		s.Equal("my-identity", *v.GetIdentity())

		v.SetClientTimeout(time.Second * 30)
		s.Equal(time.Second*30, v.GetClientTimeout())
	}

	proxyMessage = connectRequest.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	connectRequest, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(connectRequest)

	if v, ok := connectRequest.(*messages.ConnectRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("127.0.0.1:7933", *v.GetEndpoints())
		s.Equal("my-identity", *v.GetIdentity())
		s.Equal(time.Second*30, v.GetClientTimeout())
	}

	statusCode, err := s.messageToConnection(connectRequest)
	reply := <-endpoints.TestEndpointsMap[connectRequest.GetRequestID()]
	s.NotNil(reply)
	s.Equal(http.StatusOK, statusCode)
	s.NoError(err)

	if v, ok := reply.(*messages.ConnectReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Nil(v.GetError())
	}

	// DomainRegisterRequest
	var domainRegisterRequest messages.IProxyMessage = messages.NewDomainRegisterRequest()
	if v, ok := domainRegisterRequest.(*messages.DomainRegisterRequest); ok {
		s.Equal(messagetypes.DomainRegisterReply, v.GetReplyType())
	}

	proxyMessage = domainRegisterRequest.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	domainRegisterRequest, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(domainRegisterRequest)

	if v, ok := domainRegisterRequest.(*messages.DomainRegisterRequest); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetName())
		s.Nil(v.GetDescription())
		s.Nil(v.GetOwnerEmail())
		s.False(v.GetEmitMetrics())
		s.Equal(int32(0), v.GetRetentionDays())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		nameStr := "my-domain"
		v.SetName(&nameStr)
		s.Equal("my-domain", *v.GetName())

		descriptionStr := "my-description"
		v.SetDescription(&descriptionStr)
		s.Equal("my-description", *v.GetDescription())

		ownerEmailStr := "my-email"
		v.SetOwnerEmail(&ownerEmailStr)
		s.Equal("my-email", *v.GetOwnerEmail())

		v.SetEmitMetrics(true)
		s.True(v.GetEmitMetrics())

		v.SetRetentionDays(int32(14))
		s.Equal(int32(14), v.GetRetentionDays())

	}

	proxyMessage = domainRegisterRequest.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	domainRegisterRequest, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(domainRegisterRequest)

	if v, ok := domainRegisterRequest.(*messages.DomainRegisterRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-domain", *v.GetName())
		s.Equal("my-description", *v.GetDescription())
		s.Equal("my-email", *v.GetOwnerEmail())
		s.True(v.GetEmitMetrics())
		s.Equal(int32(14), v.GetRetentionDays())
	}

	statusCode, err = s.messageToConnection(domainRegisterRequest)
	reply = <-endpoints.TestEndpointsMap[domainRegisterRequest.GetRequestID()]
	s.Equal(http.StatusOK, statusCode)
	s.NotNil(reply)
	s.NoError(err)

	if v, ok := reply.(*messages.DomainRegisterReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		if err := v.GetError(); err != nil {
			s.Equal("DomainAlreadyExistsError{Message: Domain already exists.}", err.ToString())
		} else {
			s.Nil(v.GetError())
		}
	}

	// WorkflowRegisterRequest
	var workflowRegisterRequest messages.IProxyMessage = messages.NewWorkflowRegisterRequest()
	proxyMessage = workflowRegisterRequest.GetProxyMessage()

	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	workflowRegisterRequest, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(workflowRegisterRequest)

	if v, ok := workflowRegisterRequest.(*messages.WorkflowRegisterRequest); ok {
		s.Equal(messagetypes.WorkflowRegisterReply, v.ReplyType)
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetName())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		name := "my-workflow"
		v.SetName(&name)
		s.Equal("my-workflow", *v.GetName())
	}

	proxyMessage = workflowRegisterRequest.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	workflowRegisterRequest, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(workflowRegisterRequest)

	if v, ok := workflowRegisterRequest.(*messages.WorkflowRegisterRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-workflow", *v.GetName())
	}

	statusCode, err = s.messageToConnection(workflowRegisterRequest)
	reply = <-endpoints.TestEndpointsMap[workflowRegisterRequest.GetRequestID()]
	s.Equal(http.StatusOK, statusCode)
	s.NotNil(reply)
	s.NoError(err)

	if v, ok := reply.(*messages.WorkflowRegisterReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Nil(v.GetError())
		s.Equal(int64(0), v.GetWorkflowContextID())
	}

	// NewWorkerRequest
	var newWorkerRequest messages.IProxyMessage = messages.NewNewWorkerRequest()
	proxyMessage = newWorkerRequest.GetProxyMessage()

	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	newWorkerRequest, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(newWorkerRequest)

	if v, ok := newWorkerRequest.(*messages.NewWorkerRequest); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetName())
		s.Nil(v.GetDomain())
		s.Nil(v.GetTaskList())
		s.Nil(v.GetOptions())
		s.False(v.GetIsWorkflow())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		name := "my-workflow"
		v.SetName(&name)
		s.Equal("my-workflow", *v.GetName())

		domain := "my-domain"
		v.SetDomain(&domain)
		s.Equal("my-domain", *v.GetDomain())

		tasks := "my-tasks"
		v.SetTaskList(&tasks)
		s.Equal("my-tasks", *v.GetTaskList())

		opts := worker.Options{Identity: "my-identity", MaxConcurrentActivityExecutionSize: 1234}
		v.SetOptions(&opts)
		s.Equal(1234, v.GetOptions().MaxConcurrentActivityExecutionSize)
	}

	proxyMessage = newWorkerRequest.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	newWorkerRequest, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(newWorkerRequest)

	if v, ok := newWorkerRequest.(*messages.NewWorkerRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-workflow", *v.GetName())
		s.Equal("my-domain", *v.GetDomain())
		s.Equal("my-tasks", *v.GetTaskList())
		s.Equal(1234, v.GetOptions().MaxConcurrentActivityExecutionSize)
	}

	statusCode, err = s.messageToConnection(newWorkerRequest)
	reply = <-endpoints.TestEndpointsMap[newWorkerRequest.GetRequestID()]
	s.Equal(http.StatusOK, statusCode)
	s.NotNil(reply)
	s.NoError(err)

	if v, ok := reply.(*messages.NewWorkerReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Nil(v.GetError())
		s.Equal(int64(0), v.GetWorkerID())
	}
}
