//-----------------------------------------------------------------------------
// FILE:		utils.go
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

package common

import (
	"bytes"
	"fmt"
	"io"
	"io/ioutil"
	"net/http"
	"os"

	"go.uber.org/zap"

	"github.com/cadence-proxy/internal"
	"github.com/cadence-proxy/internal/messages"
)

//----------------------------------------------------------------------------
// ProxyMessage processing helpers

// CheckRequestValidity checks to make sure that the request is in
// the correct format to be handled
func CheckRequestValidity(w http.ResponseWriter, r *http.Request) (int, error) {
	internal.Logger.Debug("Request Received",
		zap.String("Address", fmt.Sprintf("http://%s%s", r.Host, r.URL.String())),
		zap.String("Method", r.Method),
		zap.Int("ProcessId", os.Getpid()),
	)

	// check if the content type is correct
	if r.Header.Get("Content-Type") != internal.ContentType {
		err := fmt.Errorf("incorrect Content-Type %s. Content must be %s",
			r.Header.Get("Content-Type"),
			internal.ContentType,
		)

		internal.Logger.Error("Incorrect Content-Type",
			zap.String("Content Type", r.Header.Get("Content-Type")),
			zap.String("Expected Content Type", internal.ContentType),
			zap.Error(err),
		)

		return http.StatusBadRequest, err
	}

	if r.Method != http.MethodPut {
		err := fmt.Errorf("invalid HTTP Method: %s, must be HTTP Metho: %s",
			r.Method,
			http.MethodPut,
		)

		internal.Logger.Error("Invalid HTTP Method",
			zap.String("Method", r.Method),
			zap.String("Expected", http.MethodPut),
			zap.Error(err),
		)

		return http.StatusMethodNotAllowed, err
	}

	return http.StatusOK, nil
}

// ReadAndDeserialize reads the ProxyMessage from the request body and
// deserializes it into the corresponding message type.
func ReadAndDeserialize(body io.Reader) (messages.IProxyMessage, error) {
	payload, err := ioutil.ReadAll(body)
	if err != nil {
		internal.Logger.Error("Null request body", zap.Error(err))
		return nil, err
	}

	// deserialize the payload
	buf := bytes.NewBuffer(payload)
	message, err := messages.Deserialize(buf, false)
	if err != nil {
		internal.Logger.Error("Error deserializing input", zap.Error(err))
		return nil, err
	}

	return message, nil
}
