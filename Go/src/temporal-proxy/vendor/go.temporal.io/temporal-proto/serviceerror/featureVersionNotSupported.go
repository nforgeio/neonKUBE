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
	"fmt"

	"github.com/gogo/status"
	"google.golang.org/grpc/codes"

	"go.temporal.io/temporal-proto/failure"
)

type (
	// FeatureVersionNotSupported represents client version is not supported error.
	FeatureVersionNotSupported struct {
		Message           string
		FeatureVersion    string
		Feature           string
		SupportedVersions string
		st                *status.Status
	}
)

// NewFeatureVersionNotSupported returns new FeatureVersionNotSupported error.
func NewFeatureVersionNotSupported(feature, featureVersion, supportedVersions string) *FeatureVersionNotSupported {
	return &FeatureVersionNotSupported{
		Message:           fmt.Sprintf("Feature %s is not supported in feature set version %s. At least %s feature set version is required", feature, featureVersion, supportedVersions),
		FeatureVersion:    featureVersion,
		Feature:           feature,
		SupportedVersions: supportedVersions,
	}
}

// Error returns string message.
func (e *FeatureVersionNotSupported) Error() string {
	return e.Message
}

func (e *FeatureVersionNotSupported) status() *status.Status {
	if e.st != nil {
		return e.st
	}

	st := status.New(codes.FailedPrecondition, e.Message)
	st, _ = st.WithDetails(
		&failure.FeatureVersionNotSupported{
			FeatureVersion:    e.FeatureVersion,
			Feature:           e.Feature,
			SupportedVersions: e.SupportedVersions,
		},
	)
	return st
}

func newFeatureVersionNotSupported(st *status.Status, failure *failure.FeatureVersionNotSupported) *FeatureVersionNotSupported {
	return &FeatureVersionNotSupported{
		Message:           st.Message(),
		FeatureVersion:    failure.GetFeatureVersion(),
		Feature:           failure.GetFeature(),
		SupportedVersions: failure.GetSupportedVersions(),
		st:                st,
	}
}
