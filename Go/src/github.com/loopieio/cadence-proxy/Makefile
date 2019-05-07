.PHONY: test bins clean
default: test

# export to the path
export PATH := $(GOPATH)/bin:$(PATH)

# Project, test, source, and build paths
PROJECT_ROOT = github.com/loopieio/cadence-proxy
BUILD_DIR := $(NF_ROOT)/Build/
COMPRESSED_DIR := $(NF_ROOT)/Lib/Neon.Cadence/Resources
APP_DIR=./cmd/cadenceproxy

# test dirs
NEON_TEST_DIR := $(NF_ROOT)/Test/Test.Neon.Cadence/bin/Debug/netcoreapp3.0
BASE_TEST_DIR=./cmd/cadenceproxy/messages/base
CLUSTER_TEST_DIR=./cmd/cadenceproxy/messages/cluster
TEST_DIRS=./cmd/cadenceproxy/messages/base \
		./cmd/cadenceproxy/messages/cluster

# Gather all source code		
ALL_SRC := $(shell find ./cmd/cadenceproxy -name "*.go")

# programs in the /cmd dir to build
PROGS = cadenceproxy

# executable names
WIN := cadence-proxy.win.exe
LINUX := cadence-proxy.linux
OSX := cadence-proxy.osx

# Use dep to update and grab source
# for dependencies
dep-ensured:
	dep ensure

cadence-proxy-windows: dep-ensured $(ALL_SRC)
	env GOOS=windows GOARCH=amd64 CGO_ENABLED=1 go build -i -ldflags="-w -s" -race -v -o bin/$(WIN) $(APP_DIR)/*.go

cadence-proxy-linux: dep-ensured $(ALL_SRC)
	env GOOS=linux GOARCH=amd64 go build -i -ldflags="-w -s" -v -o bin/$(LINUX) $(APP_DIR)/*.go

cadence-proxy-osx: dep-ensured $(ALL_SRC)
	env GOOS=darwin GOARCH=amd64 go build -i -ldflags="-w -s" -v -o bin/$(OSX) $(APP_DIR)/*.go

cadenceproxy: cadence-proxy-linux \
	cadence-proxy-windows \
	cadence-proxy-osx

bins: cadenceproxy \

test: bins
	rm -f test/base.test
	rm -f test/cluster.test
	rm -f test/logs/test.log
	echo $(TEST_DIRS)

	go test -v -o test/base.test -coverprofile test/logs/base-cover.out $(BASE_TEST_DIR) | tee -a test/logs/test.log
	go tool cover -html=test/logs/base-cover.out -o test/logs/base-cover.html

	go test -v -o test/cluster.test -coverprofile test/logs/cluster-cover.out $(CLUSTER_TEST_DIR) | tee -a test/logs/test.log; \
	go tool cover -html=test/logs/cluster-cover.out -o test/logs/cluster-cover.html; \
	
clean:
	rm -Rf bin/*
	rm -f test/base.test
	rm -f test/cluster.test

	rm -f test/logs/test.log
	rm -f test/logs/base-cover.out
	rm -f test/logs/base-cover.html
	rm -f test/logs/cluster-cover.out
	rm -f test/logs/cluster-cover.html

	rm -f $(BUILD_DIR)/$(WIN)
	rm -f $(BUILD_DIR)/$(LINUX)
	rm -f $(BUILD_DIR)/$(OSX)
	rm -f $(COMPRESSED_DIR)/$(WIN).gz
	rm -f $(COMPRESSED_DIR)/$(LINUX).gz
	rm -f $(COMPRESSED_DIR)/$(OSX).gz
	rm -f $(NEON_TEST_DIR)/cadence-proxy.exe
