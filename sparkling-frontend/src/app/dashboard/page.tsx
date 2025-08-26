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
import {Briefcase, Server, Users} from "lucide-react";

export default function Page() {
    const context = useContext(TokenContext);
    const accessToken = context?.accessToken;
    const setAccessToken = context?.setAccessToken;
    const profile = context?.profile;
    const admin = profile?.isAdmin;
    const router = useRouter();
    const hash = useHash();

    // State to manage the active hash for client-side rendering after hydration
    const [activeHash, setActiveHash] = useState<string | null>(null);

    useEffect(() => {
        // Update activeHash only on the client after the component mounts
        setActiveHash(hash);
    }, [hash]); // Re-run when the hash from useHash changes

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

    // Add a handler to update the hash
    const handleNav = (hashValue: string) => {
        window.location.hash = hashValue;
    };

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
                {/* Render default dashboard content if no hash or hash is empty/root */}
                {(!activeHash || activeHash === "#") && (
                    <div className="flex grow items-center justify-center px-4">
                        <div className="bg-white shadow-xl rounded-2xl p-8 max-w-2xl w-full text-center">
                            <h1 className="text-4xl font-bold text-gray-800 mb-4">
                                Welcome to <span className="text-blue-600">Sparkling Dashboard</span>
                            </h1>
                            <p className="text-lg text-gray-600 mb-6">
                                Manage users, nodes, and your work sessions with ease.
                            </p>
                            <div className="flex flex-col gap-6 mt-8">
                                <button
                                    className="flex items-center justify-center gap-4 bg-blue-600 hover:bg-blue-700 text-white text-xl font-semibold py-6 rounded-xl shadow transition"
                                    onClick={() => handleNav("#work-list")}
                                >
                                    <Briefcase size={32} />
                                    Work List
                                </button>
                                {admin && (
                                    <button
                                        className="flex items-center justify-center gap-4 bg-green-600 hover:bg-green-700 text-white text-xl font-semibold py-6 rounded-xl shadow transition"
                                        onClick={() => handleNav("#node-list")}
                                    >
                                        <Server size={32} />
                                        Node List
                                    </button>
                                )}
                                {admin && (
                                    <button
                                        className="flex items-center justify-center gap-4 bg-purple-600 hover:bg-purple-700 text-white text-xl font-semibold py-6 rounded-xl shadow transition"
                                        onClick={() => handleNav("#user-management")}
                                    >
                                        <Users size={32} />
                                        User Management
                                    </button>
                                )}
                            </div>
                            {/* Workflow Steps Card */}
                            <div className="mt-10 bg-gray-50 rounded-xl shadow-inner p-6 text-left">
                                <h2 className="text-2xl font-semibold text-gray-800 mb-4 flex items-center gap-2">
                                    <Briefcase size={24} className="text-blue-500" />
                                    Workflow Steps
                                </h2>
                                <ol className="space-y-4 list-decimal list-inside">
                                    <li className="flex items-center gap-2">
                                        <Users size={20} className="text-purple-500" />
                                        <span>Create a new user if needed</span>
                                    </li>
                                    <li className="flex items-center gap-2">
                                        <Server size={20} className="text-green-500" />
                                        <span>Add, set up, and activate a node</span>
                                    </li>
                                    <li className="flex items-center gap-2">
                                        <Briefcase size={20} className="text-blue-500" />
                                        <span>Start your work session</span>
                                    </li>
                                    <li className="flex items-center gap-2">
                                        {/* Jupyter SVG Icon */}
                                        <span className="inline-block">
                                            <svg width="20" height="20" viewBox="0 0 32 32" fill="none">
                                                <ellipse cx="16" cy="16" rx="13" ry="5" fill="#F37726"/>
                                                <ellipse cx="16" cy="16" rx="9" ry="3.5" fill="#FFFFFF" opacity="0.7"/>
                                                <circle cx="7" cy="7" r="2" fill="#F37726"/>
                                                <circle cx="25" cy="25" r="2" fill="#F37726"/>
                                            </svg>
                                        </span>
                                        <span>Utilize the built-in Jupyter environment</span>
                                    </li>
                                </ol>
                            </div>
                        </div>
                    </div>
                )}
                {activeHash == "#work-list" && workList && Array.isArray(workList) && <WorkList workList={workList}/>}
                {activeHash == "#node-list" && admin && nodeList && Array.isArray(nodeList) &&
                    <NodeList nodeList={nodeList}/>}
                {activeHash == "#user-management" && admin && userList && Array.isArray(userList) &&
                    <UserList userList={userList}/>}
            </SidebarInset>
        </SidebarProvider>
    );
}