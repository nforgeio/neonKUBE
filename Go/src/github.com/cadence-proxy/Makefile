#------------------------------------------------------------------------------
# FILE:         Makefile
# CONTRIBUTOR:  John C Burns
# COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http:#www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

.PHONY: test bins clean
default: test

# export to the path
export PATH := $(GOPATH)/bin:$(PATH)

# Project, test, source, and build paths
BASE            = github.com/cadence-proxy
BUILD_DIR       = $(NF_ROOT)/Build
COMPRESSED_DIR  = $(NF_ROOT)/Lib/Neon.Cadence/Resources
APP_DIR			= ./cmd/cadenceproxy
PLAYGROUND_DIR	= ./cmd/playground
TEST_LOGS		= ./test/logs

# test dirs
NEON_TEST_DIR 	= $(NF_ROOT)/Test/Test.Neon.Cadence/bin/Debug/netcoreapp3.0
TEST_DIRS 		= internal/messages  \
				  internal/dotnet-logger

# programs in the /cmd dir to build
PROGS = cadenceproxy \
	playground \

# executable names
WIN   = cadence-proxy.win.exe
LINUX = cadence-proxy.linux
OSX   = cadence-proxy.osx

cadence-proxy-windows:
	env GOOS=windows GOARCH=amd64 go build -i -mod=vendor -ldflags="-w -s" -v -o bin/$(WIN) $(APP_DIR)/*.go

cadence-proxy-linux:
	env GOOS=linux GOARCH=amd64 go build -i -mod=vendor -ldflags="-w -s" -v -o bin/$(LINUX) $(APP_DIR)/*.go

cadence-proxy-osx:
	env GOOS=darwin GOARCH=amd64 go build -i -mod=vendor -ldflags="-w -s" -v -o bin/$(OSX) $(APP_DIR)/*.go

simple:
	go build -i -o bin/simple $(PLAYGROUND_DIR)/simple/*.go

cadenceproxy: cadence-proxy-linux \
	cadence-proxy-windows \
	cadence-proxy-osx

bins: cadenceproxy \

test: bins
	@rm -Rf test/*
	@echo $(TEST_DIRS)
	@for dir in $(TEST_DIRS); do \
		mkdir -p $(TEST_LOGS)/$$dir; \
		go test -mod=vendor -v -o test/$$dir/test.test -coverprofile=$(TEST_LOGS)/$$dir/cover.out ./$$dir | tee -a $(TEST_LOGS)/test.log; \
		go tool cover -html=$(TEST_LOGS)/$$dir/cover.out -o $(TEST_LOGS)/$$dir/cover.html; \
	done;
	
clean:
	rm -Rf bin/*
	rm -Rf test/*

	rm -f $(BUILD_DIR)/$(WIN)
	rm -f $(BUILD_DIR)/$(LINUX)
	rm -f $(BUILD_DIR)/$(OSX)

	rm -f $(COMPRESSED_DIR)/$(WIN).gz
	rm -f $(COMPRESSED_DIR)/$(LINUX).gz
	rm -f $(COMPRESSED_DIR)/$(OSX).gz

	rm -f $(NEON_TEST_DIR)/cadence-proxy.exe

