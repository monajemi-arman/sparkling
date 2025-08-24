"use client";

import {useContext, useEffect, useState} from "react";
import {ChevronDown, LogOut} from "lucide-react";
import {Button} from "@/components/ui/button";
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {SidebarMenuButton} from "@/components/ui/sidebar";
import TokenProvider, {TokenContext} from "@/app/context/TokenProvider";

interface UserAccountProps {
    email: string | null;
    balanceHours: number | null;
    onLogin: () => void;
    onLogout: () => void;
}

export function UserAccount({
                                email,
                                balanceHours,
                                onLogin,
                                onLogout,
                            }: UserAccountProps) {
    const context = useContext(TokenContext);
    const isAdmin = context?.profile?.isAdmin;

    const [mounted, setMounted] = useState(false);
    useEffect(() => {
        setMounted(true);
    }, []);
    if (!mounted)
        return null;

    if (email) {
        return (
            <DropdownMenu>
                <DropdownMenuTrigger asChild>
                    <SidebarMenuButton className="flex items-center justify-between w-full gap-2">
                        <div className="flex items-center gap-2">
                            <div className="flex flex-col items-start">
                                <span className="text-sm font-medium truncate">{email}</span>
                                {balanceHours !== null && (
                                    <span className="text-xs text-muted-foreground">
                    {isAdmin ? <>&infin;</> : balanceHours} hours left
                  </span>
                                )}
                            </div>
                        </div>
                        <ChevronDown className="ml-auto h-4 w-4 text-muted-foreground"/>
                    </SidebarMenuButton>
                </DropdownMenuTrigger>
                <DropdownMenuContent
                    side="top"
                    align="start"
                    className="w-[--radix-popper-anchor-width]"
                >
                    <DropdownMenuItem className="flex flex-col items-start">
                        <span className="font-medium">{email}</span>
                    </DropdownMenuItem>
                    <DropdownMenuSeparator/>
                    <DropdownMenuItem>
                        <span>Settings</span>
                    </DropdownMenuItem>
                    <DropdownMenuSeparator/>
                    <DropdownMenuItem onClick={onLogout}>
                        <LogOut className="mr-2 h-4 w-4"/>
                        <span>Sign out</span>
                    </DropdownMenuItem>
                </DropdownMenuContent>
            </DropdownMenu>
        );
    }

    return (
        <Button onClick={onLogin} className="w-full">
            Log in
        </Button>
    );
}