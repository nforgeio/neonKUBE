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

#include <netinet/in.h>
#include <getopt.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>
#include <xtables.h>
#include <linux/netfilter.h>
#include <linux/netfilter/x_tables.h>
#include "xt_DPORT.h"

enum {
    FLAGS_DPORT = 1 << 0,
};

static const struct option dport_tg_opts[] = {
    { .name = "to-port",.has_arg = true,.val = 't' },
{},
};

static void dport_tg_help(void)
{
    printf(
        "DPORT target options:\n"
        "    --to-port port    Destination port to be set\n"
    );
}

static int dport_tg4_parse(int c, char **argv, int invert, unsigned int *flags,
    const void *entry, struct xt_entry_target **target)
{
    struct xt_dport_tginfo *info = (void *)(*target)->data;
    unsigned int port;

    switch (c) {
        case 't':

            if (!xtables_strtoui(optarg, NULL, &port, 1, 65535))
                xtables_param_act(XTF_BAD_VALUE, "DPORT", "--to-port", optarg);

            info->dport = (__u16)port;

            *flags |= FLAGS_DPORT;
            return true;
    }
    return false;
}

static void dport_tg_check(unsigned int flags)
{
    if (!(flags & FLAGS_DPORT))
        xtables_error(PARAMETER_PROBLEM, "DPORT: "
            "\"--to-port\" is required.");
}

static void dport_tg4_print(const void *entry, const struct xt_entry_target *target,
    int numeric)
{
    const struct xt_dport_tginfo *info = (const void *)target->data;

    printf(" to-port %u ", info->dport);
}

static void dport_tg4_save(const void *entry, const struct xt_entry_target *target)
{
    const struct xt_dport_tginfo *info = (const void *)target->data;

    printf(" --to-port %u ", info->dport);
}

static struct xtables_target dport_tg_reg[] = {
    {
        .version       = XTABLES_VERSION,
        .name          = "DPORT",
        .revision      = 0,
        .family        = NFPROTO_IPV4,
        .size          = XT_ALIGN(sizeof(struct xt_dport_tginfo)),
        .userspacesize = XT_ALIGN(sizeof(struct xt_dport_tginfo)),
        .help          = dport_tg_help,
        .parse         = dport_tg4_parse,
        .final_check   = dport_tg_check,
        .print         = dport_tg4_print,
        .save          = dport_tg4_save,
        .extra_opts    = dport_tg_opts,
    }
};

static void _init(void)
{
    xtables_register_targets(dport_tg_reg, sizeof(dport_tg_reg) / sizeof(*dport_tg_reg));
}
