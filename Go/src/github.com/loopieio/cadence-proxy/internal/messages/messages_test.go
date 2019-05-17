package messages_test

import (
	"bytes"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"io/ioutil"
	"net/http"
	"testing"
	"time"

	"github.com/a3linux/amazon-ssm-agent/agent/times"
	domain "github.com/loopieio/cadence-proxy/internal/cadence/cadencedomains"
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	"github.com/loopieio/cadence-proxy/internal/endpoints"
	"github.com/loopieio/cadence-proxy/internal/logger"
	"github.com/loopieio/cadence-proxy/internal/messages"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
	"github.com/loopieio/cadence-proxy/internal/server"
	"github.com/stretchr/testify/suite"

	"go.uber.org/zap"
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

	// create the new server instance,
	// set the routes, and start the server listening
	// on host:port 127.0.0.1:5000
	s.instance = server.NewInstance(_listenAddress)
	s.instance.Logger = zap.L()
	endpoints.Instance = s.instance
	endpoints.SetupRoutes(s.instance.Router)
}

// --------------------------------------------------------------------------
// Test all implemented message types

func (s *UnitTestSuite) echoToConnection(message messages.IProxyMessage) (messages.IProxyMessage, error) {
	proxyMessage := message.GetProxyMessage()
	content, err := proxyMessage.Serialize(false)
	if err != nil {
		return nil, err
	}

	buf := bytes.NewBuffer(content)
	address := fmt.Sprintf("http://%s/echo", _listenAddress)
	req, err := http.NewRequest(http.MethodPut, address, buf)
	if err != nil {
		return nil, err
	}

	// set the request header to specified content type
	req.Header.Set("Content-Type", endpoints.ContentType)

	// initialize the http.Client and send the request
	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	// create an empty []byte and read the
	// request body into it if not nil
	payload, err := ioutil.ReadAll(resp.Body)
	if err != nil {
		return nil, err
	}

	return messages.Deserialize(bytes.NewBuffer(payload), false)
}

func (s *UnitTestSuite) TestInitializeRequest() {

	var message messages.IProxyMessage = messages.NewInitializeRequest()
	if v, ok := message.(*messages.InitializeRequest); ok {
		s.Equal(messagetypes.InitializeReply, v.GetReplyType())
	}

	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.InitializeRequest); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetLibraryAddress())
		s.Equal(int32(0), v.GetLibraryPort())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		str := "1.2.3.4"
		v.SetLibraryAddress(&str)
		s.Equal("1.2.3.4", *v.GetLibraryAddress())

		v.SetLibraryPort(int32(666))
		s.Equal(int32(666), v.GetLibraryPort())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.InitializeRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("1.2.3.4", *v.GetLibraryAddress())
		s.Equal(int32(666), v.GetLibraryPort())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.InitializeRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("1.2.3.4", *v.GetLibraryAddress())
		s.Equal(int32(666), v.GetLibraryPort())
	}
}

func (s *UnitTestSuite) TestInitializeReply() {
	var message messages.IProxyMessage = messages.NewInitializeReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.InitializeReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetError(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.InitializeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.InitializeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}
}
func (s *UnitTestSuite) TestConnectRequest() {

	var message messages.IProxyMessage = messages.NewConnectRequest()
	if v, ok := message.(*messages.ConnectRequest); ok {
		s.Equal(messagetypes.ConnectReply, v.GetReplyType())
	}

	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ConnectRequest); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetEndpoints())
		s.Nil(v.GetIdentity())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		endpointsStr := "1.1.1.1:555,2.2.2.2:5555"
		v.SetEndpoints(&endpointsStr)
		s.Equal("1.1.1.1:555,2.2.2.2:5555", *v.GetEndpoints())

		identityStr := "my-identity"
		v.SetIdentity(&identityStr)
		s.Equal("my-identity", *v.GetIdentity())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ConnectRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("1.1.1.1:555,2.2.2.2:5555", *v.GetEndpoints())
		s.Equal("my-identity", *v.GetIdentity())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ConnectRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("1.1.1.1:555,2.2.2.2:5555", *v.GetEndpoints())
		s.Equal("my-identity", *v.GetIdentity())
	}
}

func (s *UnitTestSuite) TestConnectReply() {
	var message messages.IProxyMessage = messages.NewConnectReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ConnectReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetError(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ConnectReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ConnectReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}
}

func (s *UnitTestSuite) TestDomainDescribeRequest() {

	var message messages.IProxyMessage = messages.NewDomainDescribeRequest()
	if v, ok := message.(*messages.DomainDescribeRequest); ok {
		s.Equal(messagetypes.DomainDescribeReply, v.GetReplyType())
	}

	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainDescribeRequest); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetName())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		nameStr := "my-domain"
		v.SetName(&nameStr)
		s.Equal("my-domain", *v.GetName())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainDescribeRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-domain", *v.GetName())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainDescribeRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-domain", *v.GetName())
	}
}

func (s *UnitTestSuite) TestDomainDescribeReply() {
	var message messages.IProxyMessage = messages.NewDomainDescribeReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainDescribeReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())
		s.False(v.GetConfigurationEmitMetrics())
		s.Equal(int32(0), v.GetConfigurationRetentionDays())
		s.Nil(v.GetDomainInfoName())
		s.Nil(v.GetDomainInfoDescription())
		s.Nil(v.GetDomainInfoOwnerEmail())
		s.Nil(v.GetDomainInfoStatus())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetError(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())

		v.SetConfigurationEmitMetrics(true)
		s.True(v.GetConfigurationEmitMetrics())

		v.SetConfigurationRetentionDays(int32(7))
		s.Equal(int32(7), v.GetConfigurationRetentionDays())

		domainInfoNameStr := "my-name"
		v.SetDomainInfoName(&domainInfoNameStr)
		s.Equal("my-name", *v.GetDomainInfoName())

		domainInfoDescriptionStr := "my-description"
		v.SetDomainInfoDescription(&domainInfoDescriptionStr)
		s.Equal("my-description", *v.GetDomainInfoDescription())

		domainStatus := domain.Deprecated
		v.SetDomainInfoStatus(&domainStatus)
		s.Equal(domain.Deprecated, *v.GetDomainInfoStatus())

		domainInfoOwnerEmailStr := "joe@bloe.com"
		v.SetDomainInfoOwnerEmail(&domainInfoOwnerEmailStr)
		s.Equal("joe@bloe.com", *v.GetDomainInfoOwnerEmail())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainDescribeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
		s.Equal("my-name", *v.GetDomainInfoName())
		s.Equal("my-description", *v.GetDomainInfoDescription())
		s.Equal(domain.Deprecated, *v.GetDomainInfoStatus())
		s.Equal("joe@bloe.com", *v.GetDomainInfoOwnerEmail())
		s.Equal(int32(7), v.GetConfigurationRetentionDays())
		s.True(v.GetConfigurationEmitMetrics())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainDescribeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
		s.Equal("my-name", *v.GetDomainInfoName())
		s.Equal("my-description", *v.GetDomainInfoDescription())
		s.Equal(domain.Deprecated, *v.GetDomainInfoStatus())
		s.Equal("joe@bloe.com", *v.GetDomainInfoOwnerEmail())
		s.Equal(int32(7), v.GetConfigurationRetentionDays())
		s.True(v.GetConfigurationEmitMetrics())
	}
}

func (s *UnitTestSuite) TestDomainRegisterRequest() {

	var message messages.IProxyMessage = messages.NewDomainRegisterRequest()
	if v, ok := message.(*messages.DomainRegisterRequest); ok {
		s.Equal(messagetypes.DomainRegisterReply, v.GetReplyType())
	}

	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainRegisterRequest); ok {
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

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainRegisterRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-domain", *v.GetName())
		s.Equal("my-description", *v.GetDescription())
		s.Equal("my-email", *v.GetOwnerEmail())
		s.True(v.GetEmitMetrics())
		s.Equal(int32(14), v.GetRetentionDays())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainRegisterRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-domain", *v.GetName())
		s.Equal("my-description", *v.GetDescription())
		s.Equal("my-email", *v.GetOwnerEmail())
		s.True(v.GetEmitMetrics())
		s.Equal(int32(14), v.GetRetentionDays())
	}
}

func (s *UnitTestSuite) TestDomainRegisterReply() {
	var message messages.IProxyMessage = messages.NewDomainRegisterReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainRegisterReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetError(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainRegisterReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainRegisterReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}
}

func (s *UnitTestSuite) TestDomainUpdateRequest() {

	var message messages.IProxyMessage = messages.NewDomainUpdateRequest()
	if v, ok := message.(*messages.DomainUpdateRequest); ok {
		s.Equal(messagetypes.DomainUpdateReply, v.GetReplyType())
	}

	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainUpdateRequest); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetName())
		s.Nil(v.GetUpdatedInfoDescription())
		s.Nil(v.GetUpdatedInfoOwnerEmail())
		s.False(v.GetConfigurationEmitMetrics())
		s.Equal(int32(0), v.GetConfigurationRetentionDays())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		nameStr := "my-domain"
		v.SetName(&nameStr)
		s.Equal("my-domain", *v.GetName())

		descriptionStr := "my-description"
		v.SetUpdatedInfoDescription(&descriptionStr)
		s.Equal("my-description", *v.GetUpdatedInfoDescription())

		ownerEmailStr := "my-email"
		v.SetUpdatedInfoOwnerEmail(&ownerEmailStr)
		s.Equal("my-email", *v.GetUpdatedInfoOwnerEmail())

		v.SetConfigurationEmitMetrics(true)
		s.True(v.GetConfigurationEmitMetrics())

		v.SetConfigurationRetentionDays(int32(14))
		s.Equal(int32(14), v.GetConfigurationRetentionDays())

	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainUpdateRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-domain", *v.GetName())
		s.Equal("my-description", *v.GetUpdatedInfoDescription())
		s.Equal("my-email", *v.GetUpdatedInfoOwnerEmail())
		s.True(v.GetConfigurationEmitMetrics())
		s.Equal(int32(14), v.GetConfigurationRetentionDays())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainUpdateRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-domain", *v.GetName())
		s.Equal("my-description", *v.GetUpdatedInfoDescription())
		s.Equal("my-email", *v.GetUpdatedInfoOwnerEmail())
		s.True(v.GetConfigurationEmitMetrics())
		s.Equal(int32(14), v.GetConfigurationRetentionDays())
	}
}

func (s *UnitTestSuite) TestDomainUpdateReply() {
	var message messages.IProxyMessage = messages.NewDomainUpdateReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainUpdateReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetError(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainUpdateReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainUpdateReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}
}

func (s *UnitTestSuite) TestTerminateRequest() {

	var message messages.IProxyMessage = messages.NewTerminateRequest()
	if v, ok := message.(*messages.TerminateRequest); ok {
		s.Equal(messagetypes.TerminateReply, v.GetReplyType())
	}

	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.TerminateRequest); ok {
		s.Equal(int64(0), v.GetRequestID())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.TerminateRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.TerminateRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
	}
}

func (s *UnitTestSuite) TestTerminateReply() {
	var message messages.IProxyMessage = messages.NewTerminateReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.TerminateReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetError(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.TerminateReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.TerminateReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}
}

func (s *UnitTestSuite) TestHeartbeatRequest() {

	var message messages.IProxyMessage = messages.NewHeartbeatRequest()
	if v, ok := message.(*messages.HeartbeatRequest); ok {
		s.Equal(messagetypes.HeartbeatReply, v.GetReplyType())
	}

	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.HeartbeatRequest); ok {
		s.Equal(int64(0), v.GetRequestID())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.HeartbeatRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.HeartbeatRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
	}
}

func (s *UnitTestSuite) TestHeartbeatReply() {
	var message messages.IProxyMessage = messages.NewHeartbeatReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.HeartbeatReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetError(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.HeartbeatReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.HeartbeatReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
	}
}

func (s *UnitTestSuite) TestCancelRequest() {

	var message messages.IProxyMessage = messages.NewCancelRequest()
	if v, ok := message.(*messages.CancelRequest); ok {
		s.Equal(messagetypes.CancelReply, v.GetReplyType())
	}

	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.CancelRequest); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(int64(0), v.GetTargetRequestID())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetTargetRequestID(int64(666))
		s.Equal(int64(666), v.GetTargetRequestID())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.CancelRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(int64(666), v.GetTargetRequestID())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.CancelRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(int64(666), v.GetTargetRequestID())
	}
}

func (s *UnitTestSuite) TestCancelReply() {
	var message messages.IProxyMessage = messages.NewCancelReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.CancelReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())
		s.False(v.GetWasCancelled())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetError(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())

		v.SetWasCancelled(true)
		s.True(v.GetWasCancelled())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.CancelReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
		s.True(v.GetWasCancelled())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.CancelReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom, "bar"), v.GetError())
		s.True(v.GetWasCancelled())
	}
}

// --------------------------------------------------------------------------
// Test the messages.ProxyMessage helper methods

func (s *UnitTestSuite) TestPropertyHelpers() {

	// verify that the property helper methods work as expected
	message := messages.NewProxyMessage()

	// verify that non-existant property values return the default for the requested type
	s.Nil(message.GetStringProperty("foo"))
	s.Equal(int32(0), message.GetIntProperty("foo"))
	s.Equal(int64(0), message.GetLongProperty("foo"))
	s.False(message.GetBoolProperty("foo"))
	s.Equal(0.0, message.GetDoubleProperty("foo"))
	s.Equal(times.ParseIso8601UTC(times.ToIso8601UTC(time.Time{})), message.GetDateTimeProperty("foo"))
	s.Equal(time.Duration(0)*time.Nanosecond, message.GetTimeSpanProperty("foo"))

	// Verify that we can override default values for non-existant properties.

	s.Equal(int32(123), message.GetIntProperty("foo", int32(123)))
	s.Equal(int64(456), message.GetLongProperty("foo", int64(456)))
	s.True(message.GetBoolProperty("foo", true))
	s.Equal(float64(123.456), message.GetDoubleProperty("foo", float64(123.456)))
	s.Equal(time.Date(2019, time.April, 14, 0, 0, 0, 0, time.UTC), message.GetDateTimeProperty("foo", time.Date(2019, time.April, 14, 0, 0, 0, 0, time.UTC)))
	s.Equal(time.Second*123, message.GetTimeSpanProperty("foo", time.Second*123))

	// verify that we can write and then read properties
	str := "bar"
	message.SetStringProperty("foo", &str)
	s.Equal("bar", *message.GetStringProperty("foo"))

	message.SetIntProperty("foo", int32(123))
	s.Equal(int32(123), message.GetIntProperty("foo"))

	message.SetLongProperty("foo", int64(456))
	s.Equal(int64(456), message.GetLongProperty("foo"))

	message.SetBoolProperty("foo", true)
	s.True(message.GetBoolProperty("foo"))

	message.SetDoubleProperty("foo", 123.456)
	s.Equal(123.456, message.GetDoubleProperty("foo"))

	date := time.Date(2019, time.April, 14, 0, 0, 0, 0, time.UTC)
	message.SetDateTimeProperty("foo", date)
	s.Equal(date, message.GetDateTimeProperty("foo"))

	message.SetTimeSpanProperty("foo", time.Second*123)
	s.Equal(time.Second*123, message.GetTimeSpanProperty("foo"))

	jsonStr := "{\"String\":\"john\",\"Details\":\"22\",\"Type\":\"mca\"}"
	cadenceError := cadenceerrors.NewCadenceErrorEmpty()
	cadenceErrorCheck := cadenceerrors.NewCadenceErrorEmpty()
	err := json.Unmarshal([]byte(jsonStr), cadenceError)
	if err != nil {
		panic(err)
	}

	message.SetJSONProperty("foo", cadenceError)
	message.GetJSONProperty("foo", cadenceErrorCheck)
	s.Equal(cadenceError, cadenceErrorCheck)

	b, err := base64.StdEncoding.DecodeString("c29tZSBkYXRhIHdpdGggACBhbmQg77u/")
	s.NoError(err)
	message.SetBytesProperty("foo", b)
	s.Equal(b, message.GetBytesProperty("foo"))
}

// --------------------------------------------------------------------------
// Test the base messages (messages.ProxyMessage, messages.ProxyRequest, messages.ProxyReply)

// TestProxyMessage ensures that we can
// serializate and deserialize a base messages.ProxyMessage
func (s *UnitTestSuite) TestProxyMessage() {

	// empty buffer to create empty proxy message
	buf := bytes.NewBuffer(make([]byte, 0))
	message, err := messages.Deserialize(buf, true)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ProxyMessage); ok {
		s.Equal(messagetypes.Unspecified, v.Type)
		s.Empty(v.Properties)
		s.Empty(v.Attachments)
	}

	// new proxy message to fill
	message = messages.NewProxyMessage()

	if v, ok := message.(*messages.ProxyMessage); ok {

		// fill the properties map
		p1 := "1"
		p2 := "2"
		p3 := ""
		v.Properties["One"] = &p1
		v.Properties["Two"] = &p2
		v.Properties["Empty"] = &p3
		v.Properties["Nil"] = nil

		v.SetJSONProperty("Error", cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom))

		b, err := base64.StdEncoding.DecodeString("c29tZSBkYXRhIHdpdGggACBhbmQg77u/")
		s.NoError(err)
		v.SetBytesProperty("Bytes", b)

		// fill the attachments map
		v.Attachments = append(v.Attachments, []byte{0, 1, 2, 3, 4})
		v.Attachments = append(v.Attachments, make([]byte, 0))
		v.Attachments = append(v.Attachments, nil)

		// serialize the new message
		serializedMessage, err := v.Serialize(true)
		s.NoError(err)

		// byte buffer to deserialize
		buf = bytes.NewBuffer(serializedMessage)
	}

	// deserialize
	message, err = messages.Deserialize(buf, true)
	s.NoError(err)

	// check that the values are the same
	if v, ok := message.(*messages.ProxyMessage); ok {

		// type and property values
		s.Equal(messagetypes.Unspecified, v.Type)
		s.Equal(6, len(v.Properties))
		s.Equal("1", *v.Properties["One"])
		s.Equal("2", *v.Properties["Two"])
		s.Empty(v.Properties["Empty"])
		s.Nil(v.Properties["Nil"])
		s.Equal("c29tZSBkYXRhIHdpdGggACBhbmQg77u/", *v.Properties["Bytes"])

		cadenceError := cadenceerrors.NewCadenceErrorEmpty()
		v.GetJSONProperty("Error", cadenceError)
		s.Equal("foo", *cadenceError.String)
		s.Equal(cadenceerrors.Custom, cadenceError.GetType())

		// attachment values
		s.Equal(3, len(v.Attachments))
		s.Equal([]byte{0, 1, 2, 3, 4}, v.Attachments[0])
		s.Empty(v.Attachments[1])
		s.Nil(v.Attachments[2])
	}
}
func (s *UnitTestSuite) TestProxyRequest() {

	// Ensure that we can serialize and deserialize request messages

	buf := bytes.NewBuffer(make([]byte, 0))
	message, err := messages.Deserialize(buf, true, "messages.ProxyRequest")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ProxyRequest); ok {
		s.Equal(int64(0), v.GetRequestID())

		// Round-trip
		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		// serialize the new message
		serializedMessage, err := v.Serialize(true)
		s.NoError(err)

		// byte buffer to deserialize
		buf = bytes.NewBuffer(serializedMessage)
	}

	message, err = messages.Deserialize(buf, true, "messages.ProxyRequest")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ProxyRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
	}
}

func (s *UnitTestSuite) TestProxyReply() {

	// Ensure that we can serialize and deserialize reply messages

	buf := bytes.NewBuffer(make([]byte, 0))
	message, err := messages.Deserialize(buf, true, "messages.ProxyReply")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ProxyReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())

		// Round-trip
		v.SetRequestID(int64(555))
		v.SetError(cadenceerrors.NewCadenceError("MyError"))

		s.Equal(int64(555), v.GetRequestID())
		s.Nil(v.GetError().Type)
		s.Panics(func() { v.GetError().GetType() })
		s.Equal("MyError", *v.GetError().String)

		// serialize the new message
		serializedMessage, err := v.Serialize(true)
		s.NoError(err)

		// byte buffer to deserialize
		buf = bytes.NewBuffer(serializedMessage)
	}

	message, err = messages.Deserialize(buf, true, "messages.ProxyReply")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ProxyReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Nil(v.GetError().Type)
		s.Panics(func() { v.GetError().GetType() })
		s.Equal("MyError", *v.GetError().String)
	}
}
