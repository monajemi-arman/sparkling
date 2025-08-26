"use client";

import * as React from "react";
import {useContext, useEffect, useState} from "react";
import {useRouter} from "next/navigation";
import { FiHome, FiUsers, FiList, FiUser, FiServer, FiClipboard, FiGrid, FiUserCheck } from "react-icons/fi";

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

const homeNav = [
    {
        title: "Home",
        url: "/dashboard",
        icon: <FiHome size={18} />,
        isActive: false,
    },
];

const adminNav = [
    {
        title: "Administrative Panel",
        url: "#",
        items: [
            {title: "Node List", url: "#node-list", icon: <FiGrid size={18} />, isActive: false},
            {title: "User Management", url: "#user-management", icon: <FiUserCheck size={18} />, isActive: false},
        ],
    },
];

const userNav = [
    {
        title: "User Panel",
        url: "#",
        items: [
            {title: "Work List", url: "#work-list", icon: <FiClipboard size={18} />, isActive: false},
        ],
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
                {/* Home Button */}
                <SidebarMenu>
                    <SidebarMenuItem>
                        <SidebarMenuButton asChild isActive={false}>
                            <a
                                href={homeNav[0].url}
                                className="sidebar-link"
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: "0.75em",
                                    padding: "0.5em 0.75em",
                                    fontSize: "1rem",
                                    borderRadius: "6px",
                                    transition: "background 0.2s",
                                }}
                            >
                                <span>{homeNav[0].icon}</span>
                                <span>{homeNav[0].title}</span>
                            </a>
                        </SidebarMenuButton>
                    </SidebarMenuItem>
                </SidebarMenu>
                <hr style={{margin: "1em 0", border: "none", borderTop: "1px solid #eee"}} />

                {/* Navigation Groups */}
                {navMain.map((group) => (
                    <SidebarGroup key={group.title}>
                        <SidebarGroupLabel>{group.title}</SidebarGroupLabel>
                        <SidebarGroupContent>
                            <SidebarMenu>
                                {group.items.map((item) => (
                                    <SidebarMenuItem key={item.title}>
                                        <SidebarMenuButton asChild isActive={item.isActive}>
                                            <a
                                                href={item.url}
                                                className="sidebar-link"
                                                style={{
                                                    display: "flex",
                                                    alignItems: "center",
                                                    gap: "0.75em",
                                                    padding: "0.5em 0.75em",
                                                    fontSize: "1rem",
                                                    borderRadius: "6px",
                                                    transition: "background 0.2s",
                                                }}
                                            >
                                                <span>{item.icon}</span>
                                                <span>{item.title}</span>
                                            </a>
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