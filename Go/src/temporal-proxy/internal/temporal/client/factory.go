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
	"go.temporal.io/temporal/client"
	"go.uber.org/zap"
)

const (
	_temporalClientName      = "temporal-client"
	_temporalFrontendService = "temporal-frontend"
)

// TemporalClientBuilder builds Temporal namespace and workflow clients
type TemporalClientBuilder struct {
	Logger        *zap.Logger
	clientOptions client.Options
}

// NewBuilder creates a new TemporalClientBuilder
func NewBuilder(logger *zap.Logger) *TemporalClientBuilder {
	return &TemporalClientBuilder{
		Logger:        logger,
		clientOptions: client.Options{},
	}
}

// GetHostPort gets the hostport for the builder
func (b *TemporalClientBuilder) GetHostPort() string {
	return b.clientOptions.HostPort
}

// GetNamespace gets the namespace for the builder
func (b *TemporalClientBuilder) GetNamespace() string {
	return b.clientOptions.Namespace
}

// GetClientOptions gets the options for builder Temporal namespace client
// and Temporal service client
func (b *TemporalClientBuilder) GetClientOptions(opts *client.Options) client.Options {
	return b.clientOptions
}

// SetHostPort sets the hostport for the builder
func (b *TemporalClientBuilder) SetHostPort(hostport string) *TemporalClientBuilder {
	b.clientOptions.HostPort = hostport
	return b
}

// SetNamespace sets the namespace for the builder
func (b *TemporalClientBuilder) SetNamespace(namespace string) *TemporalClientBuilder {
	b.clientOptions.Namespace = namespace
	return b
}

// SetClientOptions set the options for builder Temporal namespace client
// and Temporal service client
func (b *TemporalClientBuilder) SetClientOptions(opts client.Options) *TemporalClientBuilder {
	b.clientOptions = opts
	return b
}

// BuildClient creates a new Temporal service client
func (b *TemporalClientBuilder) BuildClient() (client.Client, error) {
	client, err := client.NewClient(b.clientOptions)
	if err != nil {
		b.Logger.Error("Failed to create Temporal client",
			zap.String("Namespace", b.clientOptions.Namespace),
			zap.String("HostPort", b.clientOptions.HostPort),
			zap.Error(err))

		return nil, err
	}

	b.Logger.Info("Successfully created temporal client",
		zap.String("Namespace", b.clientOptions.Namespace),
		zap.String("HostPort", b.clientOptions.HostPort))

	return client, nil
}

// BuildTemporalNamespaceClient builds a namespace client to temporal service
func (b *TemporalClientBuilder) BuildTemporalNamespaceClient() (client.NamespaceClient, error) {
	client, err := client.NewNamespaceClient(b.clientOptions)
	if err != nil {

		b.Logger.Error("Failed to create Temporal namespace client",
			zap.String("HostPort", b.clientOptions.HostPort),
			zap.Error(err))

		return nil, err
	}

	b.Logger.Info("Successfully created Temporal namespace client",
		zap.String("HostPort", b.clientOptions.HostPort))

	return client, nil
}
