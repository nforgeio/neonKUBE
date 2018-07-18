#include <linux/init.h>
#include <linux/module.h>
#include <linux/kernel.h>

MODULE_LICENSE("MIT");
MODULE_AUTHOR("Jeff Lill (jeff@lilltek.com)");
MODULE_DESCRIPTION("iptables target extension that modifies TCP/UDP destination port.");
MODULE_VERSION("0.1");
MODULE_INFO(intree, "Y");	// Hack to avoid kernel load warnings.

static int __init xt_DPORT_init(void) {
	printk(KERN_INFO "Hello, World!\n");
	return 0;
}

static void __exit xt_DPORT_exit(void) {
	printk(KERN_INFO "Goodbye, World!\n");
}

module_init(xt_DPORT_init);
module_exit(xt_DPORT_exit);
