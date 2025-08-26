"use client";

import {Work} from "@/types/work";
import {Card, CardHeader} from "@/components/ui/card";
import dayjs from 'dayjs';
import {Button} from "@/components/ui/button";
import {Checkbox} from "@/components/ui/checkbox";
import {Tooltip, TooltipContent, TooltipProvider, TooltipTrigger} from "@/components/ui/tooltip";
import {useContext, useEffect, useState} from "react";
import useSWRMutation from "swr/mutation";
import {addUser} from "@/lib/users";
import {TokenContext} from "@/app/context/TokenProvider";
import {addWork, deleteWork, getWork, getJupyterInfo} from "@/lib/works";
import {FaExternalLinkAlt} from "react-icons/fa";

const workStatusToString = [
    "🏃 Running",
    "✅ Ended",
    "❌ Failed",
    "🛑 Stopped",
    "🔄 Starting"
];

export function WorkList(props: { workList: Work[] }) {
    const [workList, setWorkList] = useState<Work[]>(props.workList);
    const context = useContext(TokenContext);
    const accessToken = context?.accessToken;

    const [showEnded, setShowEnded] = useState(false);

    const filteredWorkList = showEnded
        ? workList
        : workList?.filter((work) => work.status !== 1);

    const {trigger: triggerAdd} = useSWRMutation(accessToken ? "addWork" : "", (url: string) => addWork(accessToken!));
    const handleAddWork = async () => {
        const res = await triggerAdd();
        if (res.length > 0) {
            const newWork = await getWork(accessToken, res);
            if (newWork)
                setWorkList(prev => [newWork, ...prev]);
            alert("Work created! ID: " + res);
        } else {
            alert("Failed to create work.");
        }
    }

    const {trigger: triggerDelete} = useSWRMutation(accessToken ? "addUser" : "", (url: string, {arg}: {
        arg: string
    }) => deleteWork(accessToken!, arg));
    const handleDeleteWork = async (workId: string) => {
        const res = await triggerDelete(workId);
        if (res) {
            setWorkList(prev => prev.filter((work) => work.id !== workId));
            alert("Work deleted!");
        } else {
            alert("Failed to create work.");
        }
    }

    // Poll for status updates every 5 seconds
    useEffect(() => {
        if (!accessToken) return;
        const interval = setInterval(async () => {
            const runningOrStarting = workList.filter(w => w.status === 0 || w.status === 4);
            if (runningOrStarting.length === 0) return;

            const updatedWorks = await Promise.all(
                workList.map(async (work) => {
                    // Only fetch if status is Starting or Running
                    if (work.status === 0 || work.status === 4) {
                        const updated = await getWork(accessToken, work.id);
                        return updated || work;
                    }
                    return work;
                })
            );
            setWorkList(updatedWorks);
        }, 5000);

        return () => clearInterval(interval);
    }, [workList, accessToken]);

    return (
        <Card>
            <CardHeader className="flex flex-col">
                <b className={"p-4"}>Work List</b>

                {/* Determine if any work is Starting or Running */}
                {(() => {
                    const disableAddButton = filteredWorkList?.some(
                        work => work.status === 0 || work.status === 4
                    );
                    const tooltipMessage = "Cannot add new work when a work is already starting or running.";

                    return (
                        <TooltipProvider>
                            <Tooltip>
                                <TooltipTrigger asChild>
                            <span>
                                <Button
                                    onClick={handleAddWork}
                                    variant="default"
                                    disabled={disableAddButton}
                                >
                                    Add
                                </Button>
                            </span>
                                </TooltipTrigger>
                                {disableAddButton && (
                                    <TooltipContent>{tooltipMessage}</TooltipContent>
                                )}
                            </Tooltip>
                        </TooltipProvider>
                    );
                })()}

                <br/>

                <div className="flex items-center gap-2 sm:items-center p-2">
                    <label className="flex items-center gap-1 text-sm">
                        <Checkbox
                            checked={showEnded}
                            onCheckedChange={() => setShowEnded(prev => !prev)}
                        />
                        Show Ended
                    </label>
                </div>
            </CardHeader>

            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 p-4">
                {filteredWorkList?.map((work) => {
                    const start = dayjs(work.startTime).format('MMM D, YYYY h:mm A');
                    const end = work.endTime ? dayjs(work.endTime).format('MMM D, YYYY h:mm A') : null;

                    // Handler for opening Jupyter
                    const handleOpenJupyter = async () => {
                        if (!accessToken) return;
                        const info = await getJupyterInfo(accessToken, work.id);
                        if (info) {
                            const url = `http://localhost:${info.port}/?token=${info.token}`;
                            window.open(url, "_blank");
                        } else {
                            alert("Failed to get Jupyter info.");
                        }
                    };

                    return (
                        <Card key={work.id} className="shadow-md rounded-2xl p-4 bg-white">
                            <div className="flex flex-col gap-2 text-sm text-gray-700">
                                <div>
                                    <span className="font-semibold text-gray-900">Start:</span> {start}
                                </div>
                                {end && (
                                    <div>
                                        <span className="font-semibold text-gray-900">End:</span> {end}
                                    </div>
                                )}
                                <div>
                                    <span
                                        className="font-semibold text-gray-900">Status:</span> {workStatusToString[work.status]}
                                </div>
                                <div className="flex gap-2">
                                    <Button onClick={() => handleDeleteWork(work.id)} className={'w-1/4'}>Delete</Button>
                                    {work.status === 0 && (
                                        <Button
                                            variant="outline"
                                            onClick={handleOpenJupyter}
                                            title="Open Jupyter Notebook"
                                            className="flex items-center gap-2"
                                        >
                                            <FaExternalLinkAlt className="inline-block" />
                                            Jupyter
                                        </Button>
                                    )}
                                </div>
                            </div>
                        </Card>
                    );
                })}
            </div>
        </Card>

    );
}