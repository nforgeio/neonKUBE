/*
 * DESCRIPTION:	DPORT target extension for Xtables - set TCP/UDP destination port
 * CONTRIBUTOR:	Jeff Lill (jeff@lilltek.com)
 * COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
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

printk(KERN_INFO "xt_DPORT: *** 1\n");

    // Ignore rules that didn't set [--to-port].

    if (info->dport == 0)
        return XT_CONTINUE;

printk(KERN_INFO "xt_DPORT: *** 2\n");

    // Make the packet buffer writable for TCP/UDP packets and
    // let packets for any other protocols drop through.

    iph = ip_hdr(skb);
printk(KERN_INFO "xt_DPORT: *** 3\n");

    switch (iph->protocol) {

        case IPPROTO_TCP:

printk(KERN_INFO "xt_DPORT: *** 4\n");
            if (!skb_make_writable(skb, sizeof(struct iphdr) + sizeof(struct tcphdr)))
                return NF_DROP;
printk(KERN_INFO "xt_DPORT: *** 5\n");

            break;

        case IPPROTO_UDP:
        case IPPROTO_UDPLITE:
            
printk(KERN_INFO "xt_DPORT: *** 6\n");
            if (!skb_make_writable(skb, sizeof(struct iphdr) + sizeof(struct udphdr)))
                return NF_DROP;
printk(KERN_INFO "xt_DPORT: *** 7\n");

            break;

        default:

            // Ignore non-TCP/UDP packets.

printk(KERN_INFO "xt_DPORT: *** 8\n");
            return XT_CONTINUE;
    }

printk(KERN_INFO "xt_DPORT: *** 9\n");
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

printk(KERN_INFO "xt_DPORT: *** 10\n");
    switch (iph->protocol) {

        case IPPROTO_TCP:

printk(KERN_INFO "xt_DPORT: *** 11\n");
            tcph = tcp_hdr(skb);
            tcph->dest = htons(info->dport);
printk(KERN_INFO "xt_DPORT: *** 12\n");
            break;

        case IPPROTO_UDP:
        case IPPROTO_UDPLITE:

printk(KERN_INFO "xt_DPORT: *** 13\n");
            udph = udp_hdr(skb);
            udph->dest = htons(info->dport);
printk(KERN_INFO "xt_DPORT: *** 14\n");
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
    printk(KERN_INFO "xt_DPORT: Target module loaded.\n");
    return xt_register_targets(dport_tg_reg, ARRAY_SIZE(dport_tg_reg));
}

static void __exit dport_tg_exit(void) {
    printk(KERN_INFO "xt_DPORT: Target module unloaded.\n");
    xt_unregister_targets(dport_tg_reg, ARRAY_SIZE(dport_tg_reg));
}

module_init(dport_tg_init);
module_exit(dport_tg_exit);

MODULE_AUTHOR("Jeff Lill (jeff@lilltek.com)");
MODULE_DESCRIPTION("Xtables: target that modifies the TCP/UDP packet destination port.");
MODULE_LICENSE("GPL");
MODULE_VERSION("0.1");
MODULE_INFO(intree, "Y");	// Hack to avoid "out-of-tree tainted kernel" load warnings.
MODULE_ALIAS("ipt_DPORT");
