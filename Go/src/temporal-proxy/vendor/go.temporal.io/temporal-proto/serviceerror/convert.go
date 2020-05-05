// The MIT License (MIT)
//
// Copyright (c) 2020 Temporal Technologies, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

package serviceerror

import (
	"context"
	"errors"

	"github.com/gogo/status"
	"google.golang.org/grpc/codes"

	"go.temporal.io/temporal-proto/failure"
)
// ToStatus converts service error to gogo gRPC status.
// If error is not a service error it returns status with code Unknown.
func ToStatus(err error) *status.Status {
	if err == nil{
		return status.New(codes.OK, "")
	}

	if svcerr, ok := err.(ServiceError); ok {
		return svcerr.status()
	}

	// Special case for context.DeadlineExceeded because it can happened in unpredictable places.
	if errors.Is(err, context.DeadlineExceeded){
		return status.New(codes.DeadlineExceeded, err.Error())
	}

	// Internal logic of status.Convert is:
	//   - if err is already gogo Status or gRPC status, then just return it (this should never happen though).
	//   - otherwise returns codes.Unknown with message from err.Error() (this might happen if some generic go error reach to this point).
	return status.Convert(err)
}

// FromStatus converts gogo gRPC status to service error.
func FromStatus(st *status.Status) error {
	if st == nil || st.Code() == codes.OK {
		return nil
	}

	// Simple case. Code to serviceerror is one to one mapping and there is no failure.
	switch st.Code() {
	case codes.Internal:
		return newInternal(st)
	case codes.DataLoss:
		return newDataLoss(st)
	case codes.ResourceExhausted:
		return newResourceExhausted(st)
	case codes.PermissionDenied:
		return newPermissionDenied(st)
	case codes.DeadlineExceeded:
		return newDeadlineExceeded(st)
	case codes.Canceled:
		return newCanceled(st)
	case codes.Unavailable:
		return newUnavailable(st)
	case codes.Unimplemented:
		return newUnimplemented(st)
	case codes.Unknown:
		// Unwrap error message from unknown error.
		return errors.New(st.Message())
	// Unsupported codes.
	case codes.OutOfRange,
		codes.Unauthenticated:
		// Use standard gRPC error representation for unsupported codes ("rpc error: code = %s desc = %s").
		return st.Err()
	}

	// Extract failure once to optimize performance.
	f := extractFailure(st)
	switch st.Code() {
	case codes.NotFound:
		if f == nil{
			return newNotFound(st, nil)
		}
		switch f := f.(type) {
		case *failure.NotFound:
			return newNotFound(st, f)
		}
	case codes.InvalidArgument:
		if f == nil {
			return newInvalidArgument(st)
		}
		switch f := f.(type) {
		case *failure.QueryFailed:
			return newQueryFailed(st)
		case *failure.CurrentBranchChanged:
			return newCurrentBranchChanged(st, f)
		}
	case codes.AlreadyExists:
		switch f := f.(type) {
		case *failure.NamespaceAlreadyExists:
			return newNamespaceAlreadyExists(st)
		case *failure.WorkflowExecutionAlreadyStarted:
			return newWorkflowExecutionAlreadyStarted(st, f)
		case *failure.CancellationAlreadyRequested:
			return newCancellationAlreadyRequested(st)
		case *failure.EventAlreadyStarted:
			return newEventAlreadyStarted(st)
		}
	case codes.FailedPrecondition:
		switch f := f.(type) {
		case *failure.NamespaceNotActive:
			return newNamespaceNotActive(st, f)
		case *failure.ClientVersionNotSupported:
			return newClientVersionNotSupported(st, f)
		case *failure.FeatureVersionNotSupported:
			return newFeatureVersionNotSupported(st, f)
		}
	case codes.Aborted:
		switch f := f.(type) {
		case *failure.ShardOwnershipLost:
			return newShardOwnershipLost(st, f)
		case *failure.RetryTask:
			return newRetryTask(st, f)
		case *failure.RetryTaskV2:
			return newRetryTaskV2(st, f)
		}
	}

	// st.Code() should have failure but it didn't (or failure is of a wrong type).
	// Then use standard gRPC error representation ("rpc error: code = %s desc = %s").
	return st.Err()
}

func extractFailure(st *status.Status) interface{} {
	details := st.Details()
	if len(details) > 0 {
		return details[0]
	}

	return nil
}
