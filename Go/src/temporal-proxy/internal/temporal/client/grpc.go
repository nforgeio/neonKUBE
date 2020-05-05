//-----------------------------------------------------------------------------
// FILE:		grpc.go
// CONTRIBUTOR: John C Burns
//
// COPYRIGHT (c) 2020 Temporal Technologies Inc.  All rights reserved.
// COPYRIGHT (c) 2020 Uber Technologies, Inc.
// ADDAPTED: (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
	"context"

	"github.com/gogo/status"
	"github.com/uber-go/tally"
	"go.temporal.io/temporal-proto/serviceerror"
	"go.temporal.io/temporal/client"
	"google.golang.org/grpc"
)

const (
	_defaultServiceConfig = "{\"loadBalancingConfig\": [{\"round_robin\":{}}]}"
)

type (
	// GRPCDialer creates gRPC connection.
	GRPCDialer func(params client.GRPCDialerParams) (*grpc.ClientConn, error)
)

func defaultGRPCDialer(params client.GRPCDialerParams) (*grpc.ClientConn, error) {
	return grpc.Dial(params.HostPort,
		grpc.WithInsecure(),
		grpc.WithChainUnaryInterceptor(params.RequiredInterceptors...),
		grpc.WithDefaultServiceConfig(params.DefaultServiceConfig),
	)
}

func requiredInterceptors(metricScope tally.Scope) []grpc.UnaryClientInterceptor {
	return []grpc.UnaryClientInterceptor{errorInterceptor}
}

func errorInterceptor(ctx context.Context, method string, req, reply interface{}, cc *grpc.ClientConn, invoker grpc.UnaryInvoker, opts ...grpc.CallOption) error {
	err := invoker(ctx, method, req, reply, cc, opts...)
	err = serviceerror.FromStatus(status.Convert(err))
	return err
}
