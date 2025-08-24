"use client";

import * as React from "react";
import {useContext, useEffect, useState} from "react";
import {useRouter} from "next/navigation";

import {
    Sidebar,
    SidebarContent,
    SidebarGroup,
    SidebarGroupContent,
    SidebarGroupLabel,
    SidebarHeader,
    SidebarMenu,
    SidebarMenuButton,
    SidebarMenuItem,
    SidebarRail,
} from "@/components/ui/sidebar";
import {TokenContext} from "@/app/context/TokenProvider";
import {UserAccount} from "@/components/user-account";

const adminNav = [
    {
        title: "Administrative Panel",
        url: "#",
        items: [
            {title: "Node List", url: "#node-list", isActive: false},
            {title: "User Management", url: "#user-management", isActive: false},
        ],
    },
];

const userNav = [
    {
        title: "User Panel",
        url: "#",
        items: [{title: "Work List", url: "#work-list", isActive: false}],
    },
];

export function AppSidebar(props: React.ComponentProps<typeof Sidebar>) {
    const {profile} = useContext(TokenContext) || {};
    const isAdmin = profile?.isAdmin ?? false;
    const router = useRouter();

    const [navMain, setNavMain] = useState(() => [...userNav]);

    useEffect(() => {
        if (isAdmin) {
            setNavMain((prev) => {
                if (prev.some((g) => g.title === adminNav[0].title)) return prev;
                return [...prev, ...adminNav];
            });
        } else {
            setNavMain([...userNav]);
        }
    }, [isAdmin]);

    const handleLogin = () => {
        router.push("/login");
    };
    const handleLogout = () => {
        localStorage.clear();
        router.push("/login");
    };

    // if no token/profile yet, don’t render anything
    if (!profile) return null;

    return (
        <Sidebar {...props}>
            <SidebarHeader>
                <UserAccount
                    email={profile.email}
                    balanceHours={profile.balanceByHour}
                    onLogin={handleLogin}
                    onLogout={handleLogout}
                />
            </SidebarHeader>

            <SidebarContent>
                {navMain.map((group) => (
                    <SidebarGroup key={group.title}>
                        <SidebarGroupLabel>{group.title}</SidebarGroupLabel>
                        <SidebarGroupContent>
                            <SidebarMenu>
                                {group.items.map((item) => (
                                    <SidebarMenuItem key={item.title}>
                                        <SidebarMenuButton asChild isActive={item.isActive}>
                                            <a href={item.url}>{item.title}</a>
                                        </SidebarMenuButton>
                                    </SidebarMenuItem>
                                ))}
                            </SidebarMenu>
                        </SidebarGroupContent>
                    </SidebarGroup>
                ))}
            </SidebarContent>

            <SidebarRail/>
        </Sidebar>
    );
}