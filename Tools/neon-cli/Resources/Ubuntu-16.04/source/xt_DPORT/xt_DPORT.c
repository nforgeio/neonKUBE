/*
 *	"DPORT" target extension for Xtables - set TCP/UDP destination port
 *	Copyright (c) 2016-2018 by neonFORGE, LLC.
 *	License MIT: https://opensource.org/licenses/MIT
*/
#include <linux/ip.h>
#include <linux/ipv6.h>
#include <linux/module.h>
#include <linux/skbuff.h>
#include <linux/tcp.h>
#include <linux/udp.h>
#include <linux/version.h>
#include <linux/netfilter.h>
#include <linux/netfilter/nf_conntrack_common.h>
#include <linux/netfilter/x_tables.h>
#include <linux/netfilter_ipv6/ip6_tables.h>
#include <net/ip.h>
#include <net/ipv6.h>
#include "xt_DPORT.h"

static int dport_tg_check(const struct xt_tgchk_param *par)
{
	return 0;	// DPORT target can be referenced from any table. 
}

static unsigned int dport_tg4(struct sk_buff *skb, const struct xt_action_param *par)
{
	const struct xt_dport_tginfo *info = par->targinfo;
	struct iphdr *iph;
	struct tcphdr *tcph;
	struct udphdr *udph;

	// Ignore rules that didn't set [--to-port].

	if (info->dport == 0)
		return XT_CONTINUE;

	// Make the packet buffer writable for TCP/UDP packets and
	// let packets for any other protocols drop through.

	iph = ip_hdr(skb);

	switch (iph->protocol) {

		case IPPROTO_TCP:

			if (!skb_make_writable(skb, sizeof(struct iphdr) + sizeof(struct tcphdr)))
				return NF_DROP;

			break;

		case IPPROTO_UDP:
		case IPPROTO_UDPLITE:
			
			if (!skb_make_writable(skb, sizeof(struct iphdr) + sizeof(struct udphdr)))
				return NF_DROP;

			break;

		default:

			// Ignore non-TCP/UDP packets.

			return XT_CONTINUE;
	}

	// IMPLEMENTATION NOTE
	// -------------------
	// We're going to make the packet buffer writable (gaining exclusive
	// access to it), get a pointer to the TCP/UDP header (immediately after 
	// the IP header), and then simply write the new destination port into 
	// the TCP/UDP header.  Note that we need to ensure that we write the 
	// port in network (little endian) byte order.
	//
	// Note that source and destination ports are not included in the packet
	// checksum computation so we don't need to bother with recomputing this.

	iph = ip_hdr(skb);		// Fetch this again because [skb_make_writable()]
							// may have relocated the packet buffer.

	switch (iph->protocol) {

		case IPPROTO_TCP:

			tcph = tcp_hdr(skb);
			tcph->dest = cpu_to_le16(info->dport);
			break;

		case IPPROTO_UDP:
		case IPPROTO_UDPLITE:

			udph = udp_hdr(skb);
			udph->dest = cpu_to_le16(info->dport);
			break;
	}

	return XT_CONTINUE;
}

static struct xt_target dport_tg_reg[] __read_mostly = {
	{
		.name       = "DPORT",
		.revision   = 0,
		.family     = NFPROTO_IPV4,
		.target     = dport_tg4,
		.targetsize = sizeof(struct xt_dport_tginfo),
		.checkentry = dport_tg_check,
		.me         = THIS_MODULE,
	}
};

static int __init dport_tg_init(void) {
	printk(KERN_INFO "Hello World!\n");
	return xt_register_targets(dport_tg_reg, ARRAY_SIZE(dport_tg_reg));
}

static void __exit dport_tg_exit(void) {
	printk(KERN_INFO "Goodbye World!\n");
	xt_unregister_targets(dport_tg_reg, ARRAY_SIZE(dport_tg_reg));
}

module_init(dport_tg_init);
module_exit(dport_tg_exit);

MODULE_AUTHOR("Jeff Lill (jeff@lilltek.com)");
MODULE_DESCRIPTION("Xtables: target that modifies the TCP/UDP packet destination port.");
MODULE_LICENSE("MIT");
MODULE_VERSION("0.1");
MODULE_INFO(intree, "Y");	// Hack to avoid kernel load warnings.
MODULE_ALIAS("ipt_DPORT");
