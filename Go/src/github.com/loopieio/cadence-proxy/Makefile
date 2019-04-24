.PHONY: test bins clean
default: test

export PATH := $(GOPATH)/bin:$(PATH)

PROJECT_ROOT = github.com/loopieio/cadence-proxy
PROGS = cadenceproxy
BUILD := ./build
APP_DIR=./cmd/cadenceproxy
TEST_DIRS=./cmd/cadenceproxy/messages/base
ALL_SRC := $(shell find ./cmd/cadenceproxy -name "*.go")

dep-ensured:
	dep ensure

cadence-proxy-windows: dep-ensured $(ALL_SRC)
	env GOOS=windows GOARCH=amd64 go build -i -ldflags="-w -s" -v -o bin/cadence-proxy.win.exe $(APP_DIR)/*.go

cadence-proxy-linux: dep-ensured $(ALL_SRC)
	env GOOS=linux GOARCH=amd64 go build -i -ldflags="-w -s" -v -o bin/cadence-proxy.linux $(APP_DIR)/*.go

cadence-proxy-osx: dep-ensured $(ALL_SRC)
	env GOOS=darwin GOARCH=amd64 go build -i -ldflags="-w -s" -v -o bin/cadence-proxy.osx $(APP_DIR)/*.go

cadenceproxy: cadence-proxy-linux \
	cadence-proxy-windows \
	cadence-proxy-osx

bins: cadenceproxy \

test: bins
	@rm -f test/cadence-proxy.test
	@rm -f test/logs/test.log
	@echo $(TEST_DIRS)
	@for dir in $(TEST_DIRS); do \
		go test -v -o test/cadence-proxy.test "$$dir" | tee -a test/logs/test.log; \
	done;

clean:
	rm -f test/cadence-proxy.test
	rm -f test/logs/test.log
	rm -fr bin/*
	rm -Rf $(BUILD)
	rm -f $(NF_ROOT)/Build/cadence-proxy.win.exe
	rm -f $(NF_ROOT)/Build/cadence-proxy.linux
	rm -f $(NF_ROOT)/Build/cadence-proxy.osx