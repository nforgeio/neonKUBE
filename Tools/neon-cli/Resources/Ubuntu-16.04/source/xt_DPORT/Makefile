obj-m += xt_DPORT.o

all:
    make -C /lib/modules/$(shell uname -r)/build M=$(PWD) modules

clean:
    make -C /lib/modules/$(shell uname -r)/build M=$(PWD) clean

# cp xt_DPORT.ko /lib/modules/$(uname -r)/kernel/net/netfilter/xt_DPORT.ko
