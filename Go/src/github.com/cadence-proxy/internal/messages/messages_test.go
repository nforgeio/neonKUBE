//-----------------------------------------------------------------------------
// FILE:		messages_test.go
// CONTRIBUTOR: John C Burns
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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

	"go.uber.org/cadence/client"
	"go.uber.org/cadence/worker"
	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"

	"github.com/a3linux/amazon-ssm-agent/agent/times"

	"github.com/stretchr/testify/suite"

	domain "github.com/cadence-proxy/internal/cadence/cadencedomains"
	"github.com/cadence-proxy/internal/cadence/cadenceerrors"
	"github.com/cadence-proxy/internal/endpoints"
	"github.com/cadence-proxy/internal/logger"
	"github.com/cadence-proxy/internal/messages"
	messagetypes "github.com/cadence-proxy/internal/messages/types"
	"github.com/cadence-proxy/internal/server"
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

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.InitializeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.InitializeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
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
		s.Equal(time.Second*30, v.GetClientTimeout())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		endpointsStr := "1.1.1.1:555,2.2.2.2:5555"
		v.SetEndpoints(&endpointsStr)
		s.Equal("1.1.1.1:555,2.2.2.2:5555", *v.GetEndpoints())

		identityStr := "my-identity"
		v.SetIdentity(&identityStr)
		s.Equal("my-identity", *v.GetIdentity())

		v.SetClientTimeout(time.Second * 30)
		s.Equal(time.Second*30, v.GetClientTimeout())
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
		s.Equal(time.Second*30, v.GetClientTimeout())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ConnectRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("1.1.1.1:555,2.2.2.2:5555", *v.GetEndpoints())
		s.Equal("my-identity", *v.GetIdentity())
		s.Equal(time.Second*30, v.GetClientTimeout())
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

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ConnectReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ConnectReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
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

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())

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
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
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
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
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

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainRegisterReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainRegisterReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
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

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainUpdateReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.DomainUpdateReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
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

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.TerminateReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.TerminateReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
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

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.HeartbeatReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.HeartbeatReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
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

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())

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
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
		s.True(v.GetWasCancelled())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.CancelReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
		s.True(v.GetWasCancelled())
	}
}

func (s *UnitTestSuite) TestNewWorkerReply() {
	var message messages.IProxyMessage = messages.NewNewWorkerReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.NewWorkerReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(int64(0), v.GetWorkerID())
		s.Nil(v.GetError())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetWorkerID(int64(666))
		s.Equal(int64(666), v.GetWorkerID())

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.NewWorkerReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(int64(666), v.GetWorkerID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.NewWorkerReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(int64(666), v.GetWorkerID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}
}

func (s *UnitTestSuite) TestNewWorkerRequest() {
	var message messages.IProxyMessage = messages.NewNewWorkerRequest()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.NewWorkerRequest); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetDomain())
		s.Nil(v.GetTaskList())
		s.Nil(v.GetOptions())
		s.False(v.GetIsWorkflow())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

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

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.NewWorkerRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-domain", *v.GetDomain())
		s.Equal("my-tasks", *v.GetTaskList())
		s.Equal(1234, v.GetOptions().MaxConcurrentActivityExecutionSize)
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.NewWorkerRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-domain", *v.GetDomain())
		s.Equal("my-tasks", *v.GetTaskList())
		s.Equal(1234, v.GetOptions().MaxConcurrentActivityExecutionSize)
	}
}

func (s *UnitTestSuite) TestStopWorkerRequest() {
	var message messages.IProxyMessage = messages.NewStopWorkerRequest()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.StopWorkerRequest); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(int64(0), v.GetWorkerID())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetWorkerID(int64(666))
		s.Equal(int64(666), v.GetWorkerID())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.StopWorkerRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(int64(666), v.GetWorkerID())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.StopWorkerRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(int64(666), v.GetWorkerID())
	}
}

func (s *UnitTestSuite) TestStopWorkerReply() {
	var message messages.IProxyMessage = messages.NewStopWorkerReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.StopWorkerReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.StopWorkerReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.StopWorkerReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}
}

func (s *UnitTestSuite) TestPingRequest() {
	var message messages.IProxyMessage = messages.NewPingRequest()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.PingRequest); ok {
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

	if v, ok := message.(*messages.PingRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.PingRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
	}
}

func (s *UnitTestSuite) TestPingReply() {
	var message messages.IProxyMessage = messages.NewPingReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.PingReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.PingReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.PingReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}
}

func (s *UnitTestSuite) TestWorkflowSetCacheSizeRequest() {
	var message messages.IProxyMessage = messages.NewWorkflowSetCacheSizeRequest()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSetCacheSizeRequest); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(0, v.GetSize())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetSize(20000)
		s.Equal(20000, v.GetSize())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSetCacheSizeRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(20000, v.GetSize())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSetCacheSizeRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(20000, v.GetSize())
	}
}

func (s *UnitTestSuite) TestWorkflowSetCacheSizeReply() {
	var message messages.IProxyMessage = messages.NewWorkflowSetCacheSizeReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSetCacheSizeReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSetCacheSizeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSetCacheSizeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}
}

func (s *UnitTestSuite) TestWorkflowRegisterRequest() {
	var message messages.IProxyMessage = messages.NewWorkflowRegisterRequest()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowRegisterRequest); ok {
		s.Equal(messagetypes.WorkflowRegisterReply, v.ReplyType)
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetName())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		name := "Foo"
		v.SetName(&name)
		s.Equal("Foo", *v.GetName())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowRegisterRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("Foo", *v.GetName())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowRegisterRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("Foo", *v.GetName())
	}
}

func (s *UnitTestSuite) TestWorkflowRegisterReply() {
	var message messages.IProxyMessage = messages.NewWorkflowRegisterReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowRegisterReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowRegisterReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowRegisterReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}
}

func (s *UnitTestSuite) TestWorkflowExecuteRequest() {
	var message messages.IProxyMessage = messages.NewWorkflowExecuteRequest()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowExecuteRequest); ok {
		s.Equal(messagetypes.WorkflowExecuteReply, v.ReplyType)
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetDomain())
		s.Nil(v.GetWorkflow())
		s.Nil(v.GetArgs())
		s.Nil(v.GetOptions())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		domain := "my-domain"
		v.SetDomain(&domain)
		s.Equal("my-domain", *v.GetDomain())

		workflow := "Foo"
		v.SetWorkflow(&workflow)
		s.Equal("Foo", *v.GetWorkflow())

		args := []byte{0, 1, 2, 3, 4}
		v.SetArgs(args)
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetArgs())

		opts := client.StartWorkflowOptions{TaskList: "my-list", ExecutionStartToCloseTimeout: time.Second * 100}
		v.SetOptions(&opts)
		s.Equal(time.Second*100, v.GetOptions().ExecutionStartToCloseTimeout)
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowExecuteRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-domain", *v.GetDomain())
		s.Equal("Foo", *v.GetWorkflow())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetArgs())
		s.Equal(time.Second*100, v.GetOptions().ExecutionStartToCloseTimeout)
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowExecuteRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-domain", *v.GetDomain())
		s.Equal("Foo", *v.GetWorkflow())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetArgs())
		s.Equal(time.Second*100, v.GetOptions().ExecutionStartToCloseTimeout)
	}
}

func (s *UnitTestSuite) TestWorkflowExecuteReply() {
	var message messages.IProxyMessage = messages.NewWorkflowExecuteReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowExecuteReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		exe := workflow.Execution{ID: "foo", RunID: "bar"}
		v.SetExecution(&exe)
		s.Equal("foo", v.GetExecution().ID)
		s.Equal("bar", v.GetExecution().RunID)

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowExecuteReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("foo", v.GetExecution().ID)
		s.Equal("bar", v.GetExecution().RunID)
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowExecuteReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("foo", v.GetExecution().ID)
		s.Equal("bar", v.GetExecution().RunID)
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}
}

func (s *UnitTestSuite) TestWorkflowInvokeRequest() {
	var message messages.IProxyMessage = messages.NewWorkflowInvokeRequest()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowInvokeRequest); ok {
		s.Equal(messagetypes.WorkflowInvokeReply, v.ReplyType)
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(int64(0), v.GetContextID())
		s.Nil(v.GetArgs())
		s.Nil(v.GetDomain())
		s.Nil(v.GetWorkflowID())
		s.Nil(v.GetRunID())
		s.Nil(v.GetWorkflowType())
		s.Nil(v.GetTaskList())
		s.Equal(time.Duration(0), v.GetExecutionStartToCloseTimeout())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		name := "Foo"
		v.SetName(&name)
		s.Equal("Foo", *v.GetName())

		v.SetContextID(int64(666))
		s.Equal(int64(666), v.GetContextID())

		args := []byte{0, 1, 2, 3, 4}
		v.SetArgs(args)
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetArgs())

		domain := "my-domain"
		v.SetDomain(&domain)
		s.Equal("my-domain", *v.GetDomain())

		workflowID := "my-workflowid"
		v.SetWorkflowID(&workflowID)
		s.Equal("my-workflowid", *v.GetWorkflowID())

		taskList := "my-tasklist"
		v.SetTaskList(&taskList)
		s.Equal("my-tasklist", *v.GetTaskList())

		runID := "my-runid"
		v.SetRunID(&runID)
		s.Equal("my-runid", *v.GetRunID())

		workflowType := "my-workflowtype"
		v.SetWorkflowType(&workflowType)
		s.Equal("my-workflowtype", *v.GetWorkflowType())

		v.SetExecutionStartToCloseTimeout(time.Hour * 24)
		s.Equal(time.Hour*24, v.GetExecutionStartToCloseTimeout())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowInvokeRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("Foo", *v.GetName())
		s.Equal(int64(666), v.GetContextID())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetArgs())
		s.Equal("my-domain", *v.GetDomain())
		s.Equal("my-workflowid", *v.GetWorkflowID())
		s.Equal("my-tasklist", *v.GetTaskList())
		s.Equal("my-runid", *v.GetRunID())
		s.Equal("my-workflowtype", *v.GetWorkflowType())
		s.Equal(time.Hour*24, v.GetExecutionStartToCloseTimeout())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowInvokeRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("Foo", *v.GetName())
		s.Equal(int64(666), v.GetContextID())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetArgs())
		s.Equal("my-domain", *v.GetDomain())
		s.Equal("my-workflowid", *v.GetWorkflowID())
		s.Equal("my-tasklist", *v.GetTaskList())
		s.Equal("my-runid", *v.GetRunID())
		s.Equal("my-workflowtype", *v.GetWorkflowType())
		s.Equal(time.Hour*24, v.GetExecutionStartToCloseTimeout())
	}
}

func (s *UnitTestSuite) TestWorkflowInvokeReply() {
	var message messages.IProxyMessage = messages.NewWorkflowInvokeReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowInvokeReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())
		s.Equal(int64(0), v.GetContextID())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetContextID(int64(666))
		s.Equal(int64(666), v.GetContextID())

		result := []byte{0, 1, 2, 3, 4}
		v.SetResult(result)
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetResult())

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowInvokeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(int64(666), v.GetContextID())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetResult())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowInvokeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(int64(666), v.GetContextID())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetResult())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}
}

func (s *UnitTestSuite) TestWorkflowCancelRequest() {
	var message messages.IProxyMessage = messages.NewWorkflowCancelRequest()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowCancelRequest); ok {
		s.Equal(messagetypes.WorkflowCancelReply, v.ReplyType)
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetWorkflowID())
		s.Nil(v.GetRunID())
		s.Nil(v.GetDomain())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		workflowID := "777"
		v.SetWorkflowID(&workflowID)
		s.Equal("777", *v.GetWorkflowID())

		runID := "666"
		v.SetRunID(&runID)
		s.Equal("666", *v.GetRunID())

		domain := "my-domain"
		v.SetDomain(&domain)
		s.Equal("my-domain", *v.GetDomain())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowCancelRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("777", *v.GetWorkflowID())
		s.Equal("my-domain", *v.GetDomain())
		s.Equal("666", *v.GetRunID())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowCancelRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("777", *v.GetWorkflowID())
		s.Equal("my-domain", *v.GetDomain())
		s.Equal("666", *v.GetRunID())
	}
}

func (s *UnitTestSuite) TestWorkflowCancelReply() {
	var message messages.IProxyMessage = messages.NewWorkflowCancelReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowCancelReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowCancelReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowCancelReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}
}

func (s *UnitTestSuite) TestWorkflowTerminateRequest() {
	var message messages.IProxyMessage = messages.NewWorkflowTerminateRequest()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowTerminateRequest); ok {
		s.Equal(messagetypes.WorkflowTerminateReply, v.ReplyType)
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetWorkflowID())
		s.Nil(v.GetRunID())
		s.Nil(v.GetReason())
		s.Nil(v.GetDetails())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		workflowID := "777"
		v.SetWorkflowID(&workflowID)
		s.Equal("777", *v.GetWorkflowID())

		runID := "666"
		v.SetRunID(&runID)
		s.Equal("666", *v.GetRunID())

		reason := "my-reason"
		v.SetReason(&reason)
		s.Equal("my-reason", *v.GetReason())

		details := []byte{0, 1, 2, 3, 4}
		v.SetDetails(details)
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetDetails())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowTerminateRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("777", *v.GetWorkflowID())
		s.Equal("my-reason", *v.GetReason())
		s.Equal("666", *v.GetRunID())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetDetails())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowTerminateRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("777", *v.GetWorkflowID())
		s.Equal("my-reason", *v.GetReason())
		s.Equal("666", *v.GetRunID())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetDetails())
	}
}

func (s *UnitTestSuite) TestWorkflowTerminateReply() {
	var message messages.IProxyMessage = messages.NewWorkflowTerminateReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowTerminateReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowTerminateReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowTerminateReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}
}

func (s *UnitTestSuite) TestWorkflowSignalRequest() {
	var message messages.IProxyMessage = messages.NewWorkflowSignalRequest()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSignalRequest); ok {
		s.Equal(messagetypes.WorkflowSignalReply, v.ReplyType)
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetWorkflowID())
		s.Nil(v.GetRunID())
		s.Nil(v.GetSignalName())
		s.Nil(v.GetSignalArgs())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		workflowID := "777"
		v.SetWorkflowID(&workflowID)
		s.Equal("777", *v.GetWorkflowID())

		runID := "666"
		v.SetRunID(&runID)
		s.Equal("666", *v.GetRunID())

		signalName := "my-signal"
		v.SetSignalName(&signalName)
		s.Equal("my-signal", *v.GetSignalName())

		signalArgs := []byte{0, 1, 2, 3, 4}
		v.SetSignalArgs(signalArgs)
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetSignalArgs())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSignalRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("777", *v.GetWorkflowID())
		s.Equal("my-signal", *v.GetSignalName())
		s.Equal("666", *v.GetRunID())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetSignalArgs())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSignalRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("777", *v.GetWorkflowID())
		s.Equal("my-signal", *v.GetSignalName())
		s.Equal("666", *v.GetRunID())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetSignalArgs())
	}
}

func (s *UnitTestSuite) TestWorkflowSignalReply() {
	var message messages.IProxyMessage = messages.NewWorkflowSignalReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSignalReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSignalReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSignalReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}
}

func (s *UnitTestSuite) TestWorkflowSignalWithStartRequest() {
	var message messages.IProxyMessage = messages.NewWorkflowSignalWithStartRequest()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSignalWithStartRequest); ok {
		s.Equal(messagetypes.WorkflowSignalWithStartReply, v.ReplyType)
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetWorkflow())
		s.Nil(v.GetWorkflowID())
		s.Nil(v.GetSignalName())
		s.Nil(v.GetSignalArgs())
		s.Nil(v.GetOptions())
		s.Nil(v.GetWorkflowArgs())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		workflow := "my-workflow"
		v.SetWorkflow(&workflow)
		s.Equal("my-workflow", *v.GetWorkflow())

		workflowID := "777"
		v.SetWorkflowID(&workflowID)
		s.Equal("777", *v.GetWorkflowID())

		signalName := "my-signal"
		v.SetSignalName(&signalName)
		s.Equal("my-signal", *v.GetSignalName())

		signalArgs := []byte{0, 1, 2, 3, 4}
		v.SetSignalArgs(signalArgs)
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetSignalArgs())

		opts := client.StartWorkflowOptions{TaskList: "my-tasklist", WorkflowIDReusePolicy: client.WorkflowIDReusePolicyAllowDuplicate}
		v.SetOptions(&opts)
		s.Equal("my-tasklist", v.GetOptions().TaskList)
		s.Equal(client.WorkflowIDReusePolicyAllowDuplicate, v.GetOptions().WorkflowIDReusePolicy)

		workflowArgs := []byte{5, 6, 7, 8, 9}
		v.SetWorkflowArgs(workflowArgs)
		s.Equal([]byte{5, 6, 7, 8, 9}, v.GetWorkflowArgs())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSignalWithStartRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-workflow", *v.GetWorkflow())
		s.Equal("777", *v.GetWorkflowID())
		s.Equal("my-signal", *v.GetSignalName())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetSignalArgs())
		s.Equal("my-tasklist", v.GetOptions().TaskList)
		s.Equal(client.WorkflowIDReusePolicyAllowDuplicate, v.GetOptions().WorkflowIDReusePolicy)
		s.Equal([]byte{5, 6, 7, 8, 9}, v.GetWorkflowArgs())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSignalWithStartRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("my-workflow", *v.GetWorkflow())
		s.Equal("777", *v.GetWorkflowID())
		s.Equal("my-signal", *v.GetSignalName())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetSignalArgs())
		s.Equal("my-tasklist", v.GetOptions().TaskList)
		s.Equal(client.WorkflowIDReusePolicyAllowDuplicate, v.GetOptions().WorkflowIDReusePolicy)
		s.Equal([]byte{5, 6, 7, 8, 9}, v.GetWorkflowArgs())
	}
}

func (s *UnitTestSuite) TestWorkflowSignalWithStartReply() {
	var message messages.IProxyMessage = messages.NewWorkflowSignalWithStartReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSignalWithStartReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())
		s.Nil(v.GetExecution())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		exe := workflow.Execution{ID: "666", RunID: "777"}
		v.SetExecution(&exe)
		s.Equal("666", v.GetExecution().ID)
		s.Equal("777", v.GetExecution().RunID)

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSignalWithStartReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("666", v.GetExecution().ID)
		s.Equal("777", v.GetExecution().RunID)
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowSignalWithStartReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("666", v.GetExecution().ID)
		s.Equal("777", v.GetExecution().RunID)
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}
}

func (s *UnitTestSuite) TestWorkflowQueryRequest() {
	var message messages.IProxyMessage = messages.NewWorkflowQueryRequest()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowQueryRequest); ok {
		s.Equal(messagetypes.WorkflowQueryReply, v.ReplyType)
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetWorkflowID())
		s.Nil(v.GetRunID())
		s.Nil(v.GetQueryName())
		s.Nil(v.GetQueryArgs())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		workflowID := "777"
		v.SetWorkflowID(&workflowID)
		s.Equal("777", *v.GetWorkflowID())

		runID := "666"
		v.SetRunID(&runID)
		s.Equal("666", *v.GetRunID())

		queryName := "my-query"
		v.SetQueryName(&queryName)
		s.Equal("my-query", *v.GetQueryName())

		queryArgs := []byte{0, 1, 2, 3, 4}
		v.SetQueryArgs(queryArgs)
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetQueryArgs())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowQueryRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("666", *v.GetRunID())
		s.Equal("777", *v.GetWorkflowID())
		s.Equal("my-query", *v.GetQueryName())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetQueryArgs())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowQueryRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("666", *v.GetRunID())
		s.Equal("777", *v.GetWorkflowID())
		s.Equal("my-query", *v.GetQueryName())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetQueryArgs())
	}
}

func (s *UnitTestSuite) TestWorkflowQueryReply() {
	var message messages.IProxyMessage = messages.NewWorkflowQueryReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowQueryReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())
		s.Nil(v.GetResult())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		result := []byte{0, 1, 2, 3, 4}
		v.SetResult(result)
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetResult())

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowQueryReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetResult())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowQueryReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetResult())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}
}

func (s *UnitTestSuite) TestWorkflowMutableRequest() {
	var message messages.IProxyMessage = messages.NewWorkflowMutableRequest()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowMutableRequest); ok {
		s.Equal(messagetypes.WorkflowMutableReply, v.ReplyType)
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetMutableID())
		s.Equal(int64(0), v.GetContextID())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		mutableID := "777"
		v.SetMutableID(&mutableID)
		s.Equal("777", *v.GetMutableID())

		v.SetContextID(int64(888))
		s.Equal(int64(888), v.GetContextID())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowMutableRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("777", *v.GetMutableID())
		s.Equal(int64(888), v.GetContextID())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowMutableRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("777", *v.GetMutableID())
		s.Equal(int64(888), v.GetContextID())
	}
}

func (s *UnitTestSuite) TestWorkflowMutableReply() {
	var message messages.IProxyMessage = messages.NewWorkflowMutableReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowMutableReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())
		s.Nil(v.GetResult())
		s.Equal(int64(0), v.GetContextID())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetContextID(int64(888))
		s.Equal(int64(888), v.GetContextID())

		result := []byte{0, 1, 2, 3, 4}
		v.SetResult(result)
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetResult())

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowMutableReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetResult())
		s.Equal(int64(888), v.GetContextID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowMutableReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(int64(888), v.GetContextID())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetResult())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}
}

func (s *UnitTestSuite) TestWorkflowMutableInvokeRequest() {
	var message messages.IProxyMessage = messages.NewWorkflowMutableInvokeRequest()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowMutableInvokeRequest); ok {
		s.Equal(messagetypes.WorkflowMutableInvokeReply, v.ReplyType)
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetMutableID())
		s.Equal(int64(0), v.GetContextID())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		mutableID := "777"
		v.SetMutableID(&mutableID)
		s.Equal("777", *v.GetMutableID())

		v.SetContextID(int64(888))
		s.Equal(int64(888), v.GetContextID())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowMutableInvokeRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("777", *v.GetMutableID())
		s.Equal(int64(888), v.GetContextID())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowMutableInvokeRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("777", *v.GetMutableID())
		s.Equal(int64(888), v.GetContextID())
	}
}

func (s *UnitTestSuite) TestWorkflowMutableInvokeReply() {
	var message messages.IProxyMessage = messages.NewWorkflowMutableInvokeReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowMutableInvokeReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())
		s.Nil(v.GetResult())
		s.Equal(int64(0), v.GetContextID())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetContextID(int64(888))
		s.Equal(int64(888), v.GetContextID())

		result := []byte{0, 1, 2, 3, 4}
		v.SetResult(result)
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetResult())

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowMutableInvokeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetResult())
		s.Equal(int64(888), v.GetContextID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowMutableInvokeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(int64(888), v.GetContextID())
		s.Equal([]byte{0, 1, 2, 3, 4}, v.GetResult())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
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

func (s *UnitTestSuite) TestWorkflowDescribeExecutionRequest() {
	var message messages.IProxyMessage = messages.NewWorkflowDescribeExecutionRequest()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowDescribeExecutionRequest); ok {
		s.Equal(messagetypes.WorkflowDescribeExecutionReply, v.ReplyType)
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetWorkflowID())
		s.Nil(v.GetRunID())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		workflowID := "777"
		v.SetWorkflowID(&workflowID)
		s.Equal("777", *v.GetWorkflowID())

		runID := "666"
		v.SetRunID(&runID)
		s.Equal("666", *v.GetRunID())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowDescribeExecutionRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("777", *v.GetWorkflowID())
		s.Equal("666", *v.GetRunID())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowDescribeExecutionRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("777", *v.GetWorkflowID())
		s.Equal("666", *v.GetRunID())
	}
}

func (s *UnitTestSuite) TestWorkflowDescribeExecutionReply() {
	var message messages.IProxyMessage = messages.NewWorkflowDescribeExecutionReply()
	proxyMessage := message.GetProxyMessage()

	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowDescribeExecutionReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetDetails())
		s.Nil(v.GetError())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetError(cadenceerrors.NewCadenceError("foo"))
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = messages.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowDescribeExecutionReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowDescribeExecutionReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.NewCadenceError("foo", cadenceerrors.Custom), v.GetError())
	}
}

// --------------------------------------------------------------------------
// Test the base messages (messages.ProxyMessage, messages.ProxyRequest, messages.ProxyReply,
// messages.WorkflowRequest, messages.WorkflowReply)

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

		v.SetJSONProperty("Error", cadenceerrors.NewCadenceError("foo"))

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
	message, err := messages.Deserialize(buf, true, "ProxyRequest")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ProxyRequest); ok {
		s.Equal(messagetypes.Unspecified, v.GetType())
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(messagetypes.Unspecified, v.GetReplyType())

		// Round-trip
		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		// serialize the new message
		serializedMessage, err := v.Serialize(true)
		s.NoError(err)

		// byte buffer to deserialize
		buf = bytes.NewBuffer(serializedMessage)
	}

	message, err = messages.Deserialize(buf, true, "ProxyRequest")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ProxyRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messagetypes.Unspecified, v.GetReplyType())
	}
}

func (s *UnitTestSuite) TestProxyReply() {

	// Ensure that we can serialize and deserialize reply messages

	buf := bytes.NewBuffer(make([]byte, 0))
	message, err := messages.Deserialize(buf, true, "ProxyReply")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ProxyReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())

		// Round-trip
		v.SetRequestID(int64(555))
		v.SetError(cadenceerrors.NewCadenceError("MyError"))

		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.Custom, v.GetError().GetType())
		s.Equal("MyError", *v.GetError().String)

		// serialize the new message
		serializedMessage, err := v.Serialize(true)
		s.NoError(err)

		// byte buffer to deserialize
		buf = bytes.NewBuffer(serializedMessage)
	}

	message, err = messages.Deserialize(buf, true, "ProxyReply")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ProxyReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.Custom, v.GetError().GetType())
		s.Equal("MyError", *v.GetError().String)
	}
}

func (s *UnitTestSuite) TestWorkflowRequest() {

	// Ensure that we can serialize and deserialize request messages

	buf := bytes.NewBuffer(make([]byte, 0))
	message, err := messages.Deserialize(buf, true, "WorkflowRequest")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowRequest); ok {
		s.Equal(v.ReplyType, messagetypes.Unspecified)
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(int64(0), v.GetContextID())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetContextID(int64(555))
		s.Equal(int64(555), v.GetContextID())

		// serialize the new message
		serializedMessage, err := v.Serialize(true)
		s.NoError(err)

		// byte buffer to deserialize
		buf = bytes.NewBuffer(serializedMessage)
	}

	message, err = messages.Deserialize(buf, true, "WorkflowRequest")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(int64(555), v.GetContextID())
	}
}

func (s *UnitTestSuite) TestWorkflowReply() {

	// Ensure that we can serialize and deserialize reply messages

	buf := bytes.NewBuffer(make([]byte, 0))
	message, err := messages.Deserialize(buf, true, "WorkflowReply")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())
		s.Equal(int64(0), v.GetContextID())

		// Round-trip
		v.SetRequestID(int64(555))
		v.SetError(cadenceerrors.NewCadenceError("MyError"))

		v.SetContextID(int64(555))
		s.Equal(int64(555), v.GetContextID())

		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.Custom, v.GetError().GetType())
		s.Equal("MyError", *v.GetError().String)

		// serialize the new message
		serializedMessage, err := v.Serialize(true)
		s.NoError(err)

		// byte buffer to deserialize
		buf = bytes.NewBuffer(serializedMessage)
	}

	message, err = messages.Deserialize(buf, true, "WorkflowReply")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.WorkflowReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.Custom, v.GetError().GetType())
		s.Equal(int64(555), v.GetContextID())
		s.Equal("MyError", *v.GetError().String)
	}
}

func (s *UnitTestSuite) TestActivityRequest() {

	// Ensure that we can serialize and deserialize request messages

	buf := bytes.NewBuffer(make([]byte, 0))
	message, err := messages.Deserialize(buf, true, "ActivityRequest")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ActivityRequest); ok {
		s.Equal(v.ReplyType, messagetypes.Unspecified)
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(int64(0), v.GetContextID())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		v.SetContextID(int64(555))
		s.Equal(int64(555), v.GetContextID())

		// serialize the new message
		serializedMessage, err := v.Serialize(true)
		s.NoError(err)

		// byte buffer to deserialize
		buf = bytes.NewBuffer(serializedMessage)
	}

	message, err = messages.Deserialize(buf, true, "ActivityRequest")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ActivityRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(int64(555), v.GetContextID())
	}
}

func (s *UnitTestSuite) TestActivityReply() {

	// Ensure that we can serialize and deserialize reply messages

	buf := bytes.NewBuffer(make([]byte, 0))
	message, err := messages.Deserialize(buf, true, "ActivityReply")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ActivityReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetError())
		s.Equal(int64(0), v.GetContextID())

		// Round-trip
		v.SetRequestID(int64(555))
		v.SetError(cadenceerrors.NewCadenceError("MyError"))

		v.SetContextID(int64(555))
		s.Equal(int64(555), v.GetContextID())

		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.Custom, v.GetError().GetType())
		s.Equal("MyError", *v.GetError().String)

		// serialize the new message
		serializedMessage, err := v.Serialize(true)
		s.NoError(err)

		// byte buffer to deserialize
		buf = bytes.NewBuffer(serializedMessage)
	}

	message, err = messages.Deserialize(buf, true, "ActivityReply")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*messages.ActivityReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.Custom, v.GetError().GetType())
		s.Equal(int64(555), v.GetContextID())
		s.Equal("MyError", *v.GetError().String)
	}
}
