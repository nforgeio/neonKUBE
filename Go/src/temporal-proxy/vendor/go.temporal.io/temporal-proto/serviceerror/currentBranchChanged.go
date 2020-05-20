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
	"github.com/gogo/status"
	"google.golang.org/grpc/codes"

	"go.temporal.io/temporal-proto/failure"
)

type (
	// CurrentBranchChanged represents current branch changed error.
	CurrentBranchChanged struct {
		Message        string
		CurrentBranchToken []byte
		st             *status.Status
	}
)

// NewCurrentBranchChanged returns new CurrentBranchChanged error.
func NewCurrentBranchChanged(message string, currentBranchToken []byte) *CurrentBranchChanged {
	return &CurrentBranchChanged{
		Message:        message,
		CurrentBranchToken: currentBranchToken,
	}
}

// Error returns string message.
func (e *CurrentBranchChanged) Error() string {
	return e.Message
}

func (e *CurrentBranchChanged) status() *status.Status {
	if e.st != nil {
		return e.st
	}

	st := status.New(codes.InvalidArgument, e.Message)
	st, _ = st.WithDetails(
		&failure.CurrentBranchChanged{
			CurrentBranchToken:e.CurrentBranchToken,
		},
	)
	return st
}

func newCurrentBranchChanged(st *status.Status, failure *failure.CurrentBranchChanged) *CurrentBranchChanged {
	return &CurrentBranchChanged{
		Message: st.Message(),
		CurrentBranchToken: failure.GetCurrentBranchToken(),
		st:      st,
	}
}
