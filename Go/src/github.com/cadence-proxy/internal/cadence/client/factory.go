//-----------------------------------------------------------------------------
// FILE:		factory.go
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

package proxyclient

import (
	"errors"
	"fmt"

	"github.com/google/uuid"

	"github.com/uber-go/tally"

	"go.uber.org/cadence/.gen/go/cadence/workflowserviceclient"
	"go.uber.org/cadence/client"
	"go.uber.org/yarpc"
	"go.uber.org/yarpc/transport/tchannel"
	"go.uber.org/zap"

	globals "github.com/cadence-proxy/internal"
)

const (
	_cadenceClientName      = "cadence-client"
	_cadenceFrontendService = "cadence-frontend"
)

// WorkflowClientBuilder build client to cadence service
type WorkflowClientBuilder struct {
	hostPort      string
	domain        string
	dispatcher    *yarpc.Dispatcher
	Logger        *zap.Logger
	clientOptions *client.Options
}

// NewBuilder creates a new WorkflowClientBuilder
func NewBuilder(logger *zap.Logger) *WorkflowClientBuilder {
	return &WorkflowClientBuilder{
		Logger: logger,
	}
}

// SetHostPort sets the hostport for the builder
func (b *WorkflowClientBuilder) SetHostPort(hostport string) *WorkflowClientBuilder {
	b.hostPort = hostport
	return b
}

// SetDomain sets the domain for the builder
func (b *WorkflowClientBuilder) SetDomain(domain string) *WorkflowClientBuilder {
	b.domain = domain
	return b
}

// SetDispatcher sets the dispatcher for the builder
func (b *WorkflowClientBuilder) SetDispatcher(dispatcher *yarpc.Dispatcher) *WorkflowClientBuilder {
	b.dispatcher = dispatcher
	return b
}

// SetClientOptions set the options for builder Cadence domain client
// and Cadence service client
func (b *WorkflowClientBuilder) SetClientOptions(opts *client.Options) *WorkflowClientBuilder {

	// set defaults for client options
	if opts == nil {
		b.clientOptions.MetricsScope = tally.NoopScope
		b.clientOptions.Identity = fmt.Sprintf("%s__%s", _cadenceFrontendService, uuid.New().String())
	}
	b.clientOptions = opts

	return b
}

// BuildCadenceClient builds a client to cadence service
func (b *WorkflowClientBuilder) BuildCadenceClient() (client.Client, error) {
	service, err := b.BuildServiceClient()
	if err != nil {
		return nil, err
	}

	return client.NewClient(service, b.domain, b.clientOptions), nil
}

// BuildCadenceDomainClient builds a domain client to cadence service
func (b *WorkflowClientBuilder) BuildCadenceDomainClient() (client.DomainClient, error) {
	service, err := b.BuildServiceClient()
	if err != nil {
		return nil, err
	}

	return client.NewDomainClient(service, b.clientOptions), nil
}

// BuildServiceClient builds a rpc service client to cadence service
func (b *WorkflowClientBuilder) BuildServiceClient() (workflowserviceclient.Interface, error) {
	if err := b.build(); err != nil {
		return nil, err
	}
	if b.dispatcher == nil {
		err := errors.New("no RPC dispatcher provided to create a connection to Cadence Service")
		b.Logger.Error("error building service client", zap.Error(err))
		return nil, err
	}

	return workflowserviceclient.New(b.dispatcher.ClientConfig(_cadenceFrontendService)), nil
}

// build builds the transport channels and dispatcher
// for connection between the cadence client instance
// and the cadence server
func (b *WorkflowClientBuilder) build() error {
	if b.dispatcher != nil {
		return nil
	}

	if len(b.hostPort) == 0 {
		return errors.New("HostPort is empty")
	}

	ch, err := tchannel.NewChannelTransport(
		tchannel.ServiceName(_cadenceClientName),
		tchannel.Logger(b.Logger),
	)

	if err != nil {
		b.Logger.Error("Failed to create transport channel", zap.Error(err))
		return err
	}

	b.Logger.Info("Creating RPC dispatcher outbound",
		zap.String("ServiceName", _cadenceFrontendService),
		zap.String("HostPort", b.hostPort),
	)

	b.dispatcher = yarpc.NewDispatcher(yarpc.Config{
		Name: _cadenceClientName,
		Outbounds: yarpc.Outbounds{
			_cadenceFrontendService: {Unary: ch.NewSingleOutbound(b.hostPort)},
		},
	})
	if b.dispatcher != nil {
		if err := b.dispatcher.Start(); err != nil {
			b.Logger.Error("Failed to create outbound transport channel", zap.Error(err))
			return err
		}
		b.Logger.Info("Created outbound transport channel/RPC dispatcher outbound",
			zap.String("ServiceName", _cadenceFrontendService),
			zap.String("HostPort", b.hostPort),
		)
	}

	return nil
}

func (b *WorkflowClientBuilder) destroy() error {
	if b.dispatcher == nil {
		return globals.ErrEntityNotExist
	}
	b.Logger.Info("Removing outbound transport channel/RPC dispatcher outbound",
		zap.String("ServiceName", _cadenceFrontendService),
		zap.String("HostPort", b.hostPort),
	)

	return b.dispatcher.Stop()
}
