"use client";

import {AppSidebar} from "@/components/app-sidebar"
import {Separator} from "@/components/ui/separator"
import {
    SidebarInset,
    SidebarProvider,
    SidebarTrigger,
} from "@/components/ui/sidebar"
import {startTransition, useActionState, useContext, useEffect, useState} from "react"
import {NodeList, NodeListProps} from "@/components/node-list"
import {TokenContext} from "../context/TokenProvider";
import {UserList} from "@/components/user-list";
import useSWR from "swr";
import {getUsers} from "@/lib/users";
import {getNodes} from "@/lib/nodes";
import {useRouter} from "next/navigation";
import useHash from "@/lib/useHash";
import {getWorks} from "@/lib/works";
import {WorkList} from "@/components/work-list";

export default function Page() {
    const context = useContext(TokenContext);
    const accessToken = context?.accessToken;
    const setAccessToken = context?.setAccessToken;
    const profile = context?.profile;
    const admin = profile?.isAdmin;
    const router = useRouter();
    const hash = useHash();

    const {
        data: workList
    } = useSWR(accessToken && admin ? 'works' : null, () => getWorks(accessToken!));

    const {
        data: nodeList
    } = useSWR(accessToken && admin ? 'nodes' : null, () => getNodes(accessToken!));

    const {
        data: userList
    } = useSWR(accessToken && admin ? 'users' : null, () => getUsers(accessToken!));

    useEffect(() => {
        if (workList == 401) {
            setAccessToken ? setAccessToken(null) : null;
            router.push('/login');
        }
    }, [workList]);

    useEffect(() => {
        if (nodeList == 401) {
            setAccessToken ? setAccessToken(null) : null;
            router.push('/login');
        }
    }, [nodeList]);

    useEffect(() => {
        if (nodeList == 401) {
            setAccessToken ? setAccessToken(null) : null;
            router.push('/login');
        }
    }, [userList]);

    return (
        <SidebarProvider>
            <AppSidebar/>
            <SidebarInset>
                <header className="flex h-16 shrink-0 items-center gap-2 border-b px-4">
                    <SidebarTrigger className="-ml-1"/>
                    <Separator
                        orientation="vertical"
                        className="mr-2 data-[orientation=vertical]:h-4"
                    />
                </header>
                {(!hash || hash === "#") && (
                    <div className="flex grow items-center justify-center px-4">
                        <div className="bg-white shadow-xl rounded-2xl p-8 max-w-2xl w-full text-center">
                            <h1 className="text-4xl font-bold text-gray-800 mb-4">
                                Welcome to <span className="text-blue-600">Sparkling Dashboard</span>
                            </h1>
                            <p className="text-lg text-gray-600 mb-6">
                                Get started with your workflow in a few simple steps:
                            </p>
                            <ul className="text-left text-gray-700 space-y-2 list-disc list-inside">
                                <li>Create a new user if needed</li>
                                <li>Add, set up, and activate a node</li>
                                <li>Start your work session</li>
                                <li>Utilize the built-in Jupyter environment</li>
                            </ul>
                        </div>
                    </div>
                )}
                {hash == "#work-list" && workList && Array.isArray(workList) && <WorkList workList={workList}/>}
                {hash == "#node-list" && admin && nodeList && Array.isArray(nodeList) &&
                    <NodeList nodeList={nodeList}/>}
                {hash == "#user-management" && admin && userList && Array.isArray(userList) &&
                    <UserList userList={userList}/>}
            </SidebarInset>
        </SidebarProvider>
    );
}