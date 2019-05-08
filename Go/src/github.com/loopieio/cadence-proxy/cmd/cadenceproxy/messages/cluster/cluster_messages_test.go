package cluster_test

import (
	"bytes"
	"fmt"
	"io/ioutil"
	"net/http"
	"os"
	"testing"
	"time"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/cluster"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/endpoints"
	"go.uber.org/zap"
	"go.uber.org/zap/zapcore"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/server"
	"github.com/stretchr/testify/suite"
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
	s.setupTestSuiteMessagesMap()
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

	// new *zap.Logger
	// new zapcore.EncoderConfig for the logger
	var logger *zap.Logger
	var encoderCfg zapcore.EncoderConfig

	// new AtomicLevel for dynamic logging level
	atom := zap.NewAtomicLevel()

	// set the log level
	atom.SetLevel(zap.DebugLevel)

	// set Debug in endpoints
	endpoints.Debug = true

	// create the logger
	encoderCfg = zap.NewDevelopmentEncoderConfig()
	encoderCfg.TimeKey = "Time"
	encoderCfg.LevelKey = "Level"
	encoderCfg.MessageKey = "Debug Message"
	logger = zap.New(zapcore.NewCore(
		zapcore.NewJSONEncoder(encoderCfg),
		zapcore.Lock(os.Stdout),
		atom,
	))
	defer logger.Sync()

	// set the global logger
	_ = zap.ReplaceGlobals(logger)

	// create the new server instance,
	// set the routes, and start the server listening
	// on host:port 127.0.0.1:5000
	s.instance = server.NewInstance(_listenAddress)
	endpoints.Instance = s.instance
	endpoints.SetupRoutes(s.instance.Router)
}

func (s *UnitTestSuite) setupTestSuiteMessagesMap() {
	cluster.FillMessageTypeStructMap()
}

// --------------------------------------------------------------------------
// Test all implemented message types

func (s *UnitTestSuite) echoToConnection(message base.IProxyMessage) (base.IProxyMessage, error) {
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

	return base.Deserialize(bytes.NewBuffer(payload), false)
}

func (s *UnitTestSuite) TestInitializeRequest() {

	var message base.IProxyMessage = cluster.NewInitializeRequest()
	if v, ok := message.(*cluster.InitializeRequest); ok {
		s.Equal(messages.InitializeReply, v.GetReplyType())
	}

	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.InitializeRequest); ok {
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

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.InitializeRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("1.2.3.4", *v.GetLibraryAddress())
		s.Equal(int32(666), v.GetLibraryPort())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.InitializeRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("1.2.3.4", *v.GetLibraryAddress())
		s.Equal(int32(666), v.GetLibraryPort())
	}
}

func (s *UnitTestSuite) TestInitializeReply() {
	var message base.IProxyMessage = cluster.NewInitializeReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.InitializeReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(messages.None, v.GetErrorType())
		s.Nil(v.GetError())
		s.Nil(v.GetErrorDetails())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetErrorType(messages.Custom)
		s.Equal(messages.Custom, v.GetErrorType())

		errStr := "MyError"
		v.SetError(&errStr)
		s.Equal("MyError", *v.GetError())

		errDetailsStr := "MyError Details"
		v.SetErrorDetails(&errDetailsStr)
		s.Equal("MyError Details", *v.GetErrorDetails())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.InitializeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.InitializeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
	}
}
func (s *UnitTestSuite) TestConnectRequest() {

	var message base.IProxyMessage = cluster.NewConnectRequest()
	if v, ok := message.(*cluster.ConnectRequest); ok {
		s.Equal(messages.ConnectReply, v.GetReplyType())
	}

	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.ConnectRequest); ok {
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

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.ConnectRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("1.1.1.1:555,2.2.2.2:5555", *v.GetEndpoints())
		s.Equal("my-identity", *v.GetIdentity())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.ConnectRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("1.1.1.1:555,2.2.2.2:5555", *v.GetEndpoints())
		s.Equal("my-identity", *v.GetIdentity())
	}
}

func (s *UnitTestSuite) TestConnectReply() {
	var message base.IProxyMessage = cluster.NewConnectReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.ConnectReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(messages.None, v.GetErrorType())
		s.Nil(v.GetError())
		s.Nil(v.GetErrorDetails())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetErrorType(messages.Custom)
		s.Equal(messages.Custom, v.GetErrorType())

		errStr := "MyError"
		v.SetError(&errStr)
		s.Equal("MyError", *v.GetError())

		errDetailsStr := "MyError Details"
		v.SetErrorDetails(&errDetailsStr)
		s.Equal("MyError Details", *v.GetErrorDetails())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.ConnectReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.ConnectReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
	}
}

func (s *UnitTestSuite) TestDomainDescribeRequest() {

	var message base.IProxyMessage = cluster.NewDomainDescribeRequest()
	if v, ok := message.(*cluster.DomainDescribeRequest); ok {
		s.Equal(messages.DomainDescribeReply, v.GetReplyType())
	}

	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.DomainDescribeRequest); ok {
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

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.DomainDescribeRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-domain", *v.GetName())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.DomainDescribeRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-domain", *v.GetName())
	}
}

func (s *UnitTestSuite) TestDomainDescribeReply() {
	var message base.IProxyMessage = cluster.NewDomainDescribeReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.DomainDescribeReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(messages.None, v.GetErrorType())
		s.Nil(v.GetError())
		s.Nil(v.GetErrorDetails())
		s.False(v.GetConfigurationEmitMetrics())
		s.Equal(int32(0), v.GetConfigurationRetentionDays())
		s.Nil(v.GetDomainInfoName())
		s.Nil(v.GetDomainInfoDescription())
		s.Nil(v.GetDomainInfoOwnerEmail())
		s.Equal(messages.StatusUnspecified, v.GetDomainInfoStatus())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetErrorType(messages.Custom)
		s.Equal(messages.Custom, v.GetErrorType())

		errStr := "MyError"
		v.SetError(&errStr)
		s.Equal("MyError", *v.GetError())

		errDetailsStr := "MyError Details"
		v.SetErrorDetails(&errDetailsStr)
		s.Equal("MyError Details", *v.GetErrorDetails())

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

		v.SetDomainInfoStatus(messages.Deprecated)
		s.Equal(messages.Deprecated, v.GetDomainInfoStatus())

		domainInfoOwnerEmailStr := "joe@bloe.com"
		v.SetDomainInfoOwnerEmail(&domainInfoOwnerEmailStr)
		s.Equal("joe@bloe.com", *v.GetDomainInfoOwnerEmail())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.DomainDescribeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
		s.Equal("my-name", *v.GetDomainInfoName())
		s.Equal("my-description", *v.GetDomainInfoDescription())
		s.Equal(messages.Deprecated, v.GetDomainInfoStatus())
		s.Equal("joe@bloe.com", *v.GetDomainInfoOwnerEmail())
		s.Equal(int32(7), v.GetConfigurationRetentionDays())
		s.True(v.GetConfigurationEmitMetrics())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.DomainDescribeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
		s.Equal("my-name", *v.GetDomainInfoName())
		s.Equal("my-description", *v.GetDomainInfoDescription())
		s.Equal(messages.Deprecated, v.GetDomainInfoStatus())
		s.Equal("joe@bloe.com", *v.GetDomainInfoOwnerEmail())
		s.Equal(int32(7), v.GetConfigurationRetentionDays())
		s.True(v.GetConfigurationEmitMetrics())
	}
}

func (s *UnitTestSuite) TestDomainRegisterRequest() {

	var message base.IProxyMessage = cluster.NewDomainRegisterRequest()
	if v, ok := message.(*cluster.DomainRegisterRequest); ok {
		s.Equal(messages.DomainRegisterReply, v.GetReplyType())
	}

	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.DomainRegisterRequest); ok {
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

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.DomainRegisterRequest); ok {
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

	if v, ok := message.(*cluster.DomainRegisterRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-domain", *v.GetName())
		s.Equal("my-description", *v.GetDescription())
		s.Equal("my-email", *v.GetOwnerEmail())
		s.True(v.GetEmitMetrics())
		s.Equal(int32(14), v.GetRetentionDays())
	}
}

func (s *UnitTestSuite) TestDomainRegisterReply() {
	var message base.IProxyMessage = cluster.NewDomainRegisterReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.DomainRegisterReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(messages.None, v.GetErrorType())
		s.Nil(v.GetError())
		s.Nil(v.GetErrorDetails())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetErrorType(messages.Custom)
		s.Equal(messages.Custom, v.GetErrorType())

		errStr := "MyError"
		v.SetError(&errStr)
		s.Equal("MyError", *v.GetError())

		errDetailsStr := "MyError Details"
		v.SetErrorDetails(&errDetailsStr)
		s.Equal("MyError Details", *v.GetErrorDetails())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.DomainRegisterReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.DomainRegisterReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
	}
}

func (s *UnitTestSuite) TestDomainUpdateRequest() {

	var message base.IProxyMessage = cluster.NewDomainUpdateRequest()
	if v, ok := message.(*cluster.DomainUpdateRequest); ok {
		s.Equal(messages.DomainUpdateReply, v.GetReplyType())
	}

	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.DomainUpdateRequest); ok {
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

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.DomainUpdateRequest); ok {
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

	if v, ok := message.(*cluster.DomainUpdateRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-domain", *v.GetName())
		s.Equal("my-description", *v.GetUpdatedInfoDescription())
		s.Equal("my-email", *v.GetUpdatedInfoOwnerEmail())
		s.True(v.GetConfigurationEmitMetrics())
		s.Equal(int32(14), v.GetConfigurationRetentionDays())
	}
}

func (s *UnitTestSuite) TestDomainUpdateReply() {
	var message base.IProxyMessage = cluster.NewDomainUpdateReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.DomainUpdateReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(messages.None, v.GetErrorType())
		s.Nil(v.GetError())
		s.Nil(v.GetErrorDetails())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetErrorType(messages.Custom)
		s.Equal(messages.Custom, v.GetErrorType())

		errStr := "MyError"
		v.SetError(&errStr)
		s.Equal("MyError", *v.GetError())

		errDetailsStr := "MyError Details"
		v.SetErrorDetails(&errDetailsStr)
		s.Equal("MyError Details", *v.GetErrorDetails())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.DomainUpdateReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.DomainUpdateReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
	}
}

func (s *UnitTestSuite) TestTerminateRequest() {

	var message base.IProxyMessage = cluster.NewTerminateRequest()
	if v, ok := message.(*cluster.TerminateRequest); ok {
		s.Equal(messages.TerminateReply, v.GetReplyType())
	}

	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.TerminateRequest); ok {
		s.Equal(int64(0), v.GetRequestID())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.TerminateRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.TerminateRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
	}
}

func (s *UnitTestSuite) TestTerminateReply() {
	var message base.IProxyMessage = cluster.NewTerminateReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.TerminateReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(messages.None, v.GetErrorType())
		s.Nil(v.GetError())
		s.Nil(v.GetErrorDetails())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetErrorType(messages.Custom)
		s.Equal(messages.Custom, v.GetErrorType())

		errStr := "MyError"
		v.SetError(&errStr)
		s.Equal("MyError", *v.GetError())

		errDetailsStr := "MyError Details"
		v.SetErrorDetails(&errDetailsStr)
		s.Equal("MyError Details", *v.GetErrorDetails())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.TerminateReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.TerminateReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
	}
}

func (s *UnitTestSuite) TestHeartbeatRequest() {

	var message base.IProxyMessage = cluster.NewHeartbeatRequest()
	if v, ok := message.(*cluster.HeartbeatRequest); ok {
		s.Equal(messages.HeartbeatReply, v.GetReplyType())
	}

	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.HeartbeatRequest); ok {
		s.Equal(int64(0), v.GetRequestID())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.HeartbeatRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.HeartbeatRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
	}
}

func (s *UnitTestSuite) TestHeartbeatReply() {
	var message base.IProxyMessage = cluster.NewHeartbeatReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.HeartbeatReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(messages.None, v.GetErrorType())
		s.Nil(v.GetError())
		s.Nil(v.GetErrorDetails())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetErrorType(messages.Custom)
		s.Equal(messages.Custom, v.GetErrorType())

		errStr := "MyError"
		v.SetError(&errStr)
		s.Equal("MyError", *v.GetError())

		errDetailsStr := "MyError Details"
		v.SetErrorDetails(&errDetailsStr)
		s.Equal("MyError Details", *v.GetErrorDetails())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.HeartbeatReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.HeartbeatReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
	}
}

func (s *UnitTestSuite) TestCancelRequest() {

	var message base.IProxyMessage = cluster.NewCancelRequest()
	if v, ok := message.(*cluster.CancelRequest); ok {
		s.Equal(messages.CancelReply, v.GetReplyType())
	}

	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.CancelRequest); ok {
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

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.CancelRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(int64(666), v.GetTargetRequestID())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.CancelRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(int64(666), v.GetTargetRequestID())
	}
}

func (s *UnitTestSuite) TestCancelReply() {
	var message base.IProxyMessage = cluster.NewCancelReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.CancelReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(messages.None, v.GetErrorType())
		s.Nil(v.GetError())
		s.Nil(v.GetErrorDetails())
		s.False(v.GetWasCancelled())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetErrorType(messages.Custom)
		s.Equal(messages.Custom, v.GetErrorType())

		errStr := "MyError"
		v.SetError(&errStr)
		s.Equal("MyError", *v.GetError())

		errDetailsStr := "MyError Details"
		v.SetErrorDetails(&errDetailsStr)
		s.Equal("MyError Details", *v.GetErrorDetails())

		v.SetWasCancelled(true)
		s.True(v.GetWasCancelled())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.CancelReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
		s.True(v.GetWasCancelled())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.CancelReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
		s.True(v.GetWasCancelled())
	}
}
