"use client";

import { Card, CardContent, CardDescription, CardFooter, CardHeader } from "@/components/ui/card"
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogHeader,
    DialogTitle,
    DialogTrigger,
} from "@/components/ui/dialog"
import { z } from 'zod';
import { useContext, useEffect, useRef, useState } from "react";
import { Check, Copy, Download } from "lucide-react";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
import { Button } from "@/components/ui/button";
import useSWRMutation from "swr/mutation";
import { addNode, getNodes, setupNode, activateNode, deleteNode } from "@/lib/nodes";
import { TokenContext } from "@/app/context/TokenProvider";
import {
    DropdownMenu, DropdownMenuContent,
    DropdownMenuItem, DropdownMenuLabel,
    DropdownMenuSeparator,
    DropdownMenuTrigger
} from "@/components/ui/dropdown-menu";

export interface Node {
    id: string;
    name: string;
    description: string;
    address: string;
    isLocal: boolean;
    isActive?: boolean;
    sshPublicKey?: string;
}

const nodeSchema = z.object({
    name: z.string().min(1),
    description: z.string(),
    address: z.string().min(1),
    isLocal: z.boolean()
});

export type NodeFormData = z.infer<typeof nodeSchema>;

export interface NodeListProps {
    nodes?: Node[]
}

export function NodeList(props: { nodeList: Node[] }) {
    const nodeList = props.nodeList;
    const context = useContext(TokenContext);
    const accessToken = context?.accessToken;
    const [setupScript, setSetupScript] = useState<string>("");
    const [isSetupDialogOpen, setIsSetupDialogOpen] = useState<boolean>(false);
    const [copied, setCopied] = useState(false);
    const [activationSteps, setActivationSteps] = useState<{ nodeId: string, step: string, message?: string, ts: number }[]>([]);
    const [activationStarted, setActivationStarted] = useState(false);
    const stepsRef = useRef(activationSteps);
    stepsRef.current = activationSteps;
    let forceLocal = nodeList.length === 0;
    const [formData, setFormData] = useState<NodeFormData>({
        name: '',
        description: '',
        address: '',
        isLocal: forceLocal
    });
    // Add state to trigger re-render after delete
    const [deletedNodeIds, setDeletedNodeIds] = useState<string[]>([]);


    const { trigger, isMutating, error } = useSWRMutation(accessToken, addNode);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        try {
            const validatedData = nodeSchema.parse(formData);
            const result = await trigger(validatedData);
            if (result) {
                alert("Node created!");
                console.log(result);
            } else {
                alert("Failed to create node!");
            }

            // Reset form and close the dialog
            setFormData({
                name: '',
                description: '',
                address: '',
                isLocal: forceLocal
            });
        } catch (error) {
            console.error('Error adding node:', error);
        }
    };

    const { trigger: triggerSetupNode } = useSWRMutation(accessToken, setupNode);
    const handleSetupNode = async (nodeId: string, e: any) => {
        const setupScript = await triggerSetupNode(nodeId);
        if (typeof setupScript === "string") {
            setSetupScript(setupScript);
            setIsSetupDialogOpen(true);
        }
        else {
            alert(setupScript);
        }
    }

    const handleDownloadScript = () => {
        const blob = new Blob([setupScript], { type: 'text/x-sh' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'setup.sh';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }
    const handleCopyScript = () => {
        navigator.clipboard.writeText(setupScript)
        setCopied(true)
        setTimeout(() => {
            setCopied(false)
        }, 2000)
    }

    const { trigger: triggerActivateNode } = useSWRMutation(accessToken, activateNode);
    const handleActivateNode = async (nodeId: string, e: any) => {
        setActivationStarted(true);

        // Optimistically show the first step immediately
        setActivationSteps(prev => [
            ...prev,
            {
                nodeId,
                step: "starting",
                message: "Starting node activation",
                ts: Date.now()
            }
        ]);
        const result = await triggerActivateNode(nodeId);
        if (result)
            alert("Node activated!");
        else
            alert("Failed to activate node!");
    }

    // Add delete handler
    const { trigger: triggerDeleteNode } = useSWRMutation(accessToken, deleteNode);
    const handleDeleteNode = async (nodeId: string, e: any) => {
        if (!window.confirm("Are you sure you want to delete this node?")) return;
        const result = await triggerDeleteNode(nodeId);
        if (result) {
            setDeletedNodeIds(ids => [...ids, nodeId]);
        } else {
            alert("Failed to delete node!");
        }
    };

    useEffect(() => {
        const eventSource = new EventSource("/api/v0/nodes/activation-events");
        eventSource.onmessage = (event) => {
            try {
                const data = JSON.parse(event.data);
                setActivationSteps(prev => [
                    ...prev,
                    {
                        nodeId: data.nodeId,
                        step: data.step,
                        message: data.message,
                        ts: Date.now()
                    }
                ]);
            } catch { }
        };
        eventSource.onerror = () => {
            eventSource.close();
        };
        return () => eventSource.close();
    }, [activationStarted]);

    // Optionally, auto-hide steps after some time
    useEffect(() => {
        if (activationSteps.length === 0) return;
        const timeout = setTimeout(() => {
            setActivationSteps(stepsRef.current.filter(s => Date.now() - s.ts < 15000));
        }, 5000);
        return () => clearTimeout(timeout);
    }, [activationSteps]);

    return (
        <>
            <Card id={'node-list'}>
                <CardHeader>
                    <div className="space-y-2">
                        <h2 className="text-xl font-semibold">Node List</h2>
                        <CardDescription>
                            Choose a server to manage...
                        </CardDescription>

                        <div className="mt-3 p-4 rounded-lg bg-blue-50 border border-blue-200 text-blue-800 text-sm leading-relaxed">
                            <p className="font-medium">📘 Important:</p>
                            <p>
                                The <strong>first node</strong> you add will always be your
                                <strong> local master node</strong>.
                            </p>
                            <p className="mt-1">
                                If you also want your local node to act as a <strong>worker node</strong>,
                                simply add it <em>a second time</em> — this time <strong>without</strong> checking
                                the <code>Local Node</code> checkbox.
                            </p>
                        </div>
                    </div>

                    <Dialog>
                        <DialogTrigger asChild>
                            <Button className="w-1/8" variant="default">Add</Button>
                        </DialogTrigger>
                        <DialogContent>
                            <DialogHeader>
                                <DialogTitle>Node details</DialogTitle>
                                <DialogDescription>
                                    Fill out server details to add to nodes...
                                </DialogDescription>
                            </DialogHeader>
                            <form onSubmit={handleSubmit} className="space-y-4">
                                <div className="space-y-2">
                                    <Label htmlFor="name">Name</Label>
                                    <Input
                                        id="name"
                                        value={formData.name}
                                        onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                                        required
                                    />
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="description">Description</Label>
                                    <Input
                                        id="description"
                                        value={formData.description}
                                        onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                    />
                                </div>
                                <div className="space-y-2">
                                    <Label htmlFor="address">Address</Label>
                                    <Input
                                        id="address"
                                        value={formData.address}
                                        onChange={(e) => setFormData({ ...formData, address: e.target.value })}
                                        required
                                    />
                                </div>
                                <div className="flex items-center space-x-4">
                                    <div className="flex items-center space-x-2">
                                        <Checkbox
                                            id="isLocal"
                                            checked={formData.isLocal || forceLocal}
                                            disabled={forceLocal}
                                            onCheckedChange={(checked) =>
                                                setFormData({ ...formData, isLocal: checked as boolean })}
                                        />
                                        <Label htmlFor="isLocal">Local Node</Label>
                                    </div>
                                </div>
                                <Button type="submit" className="w-full">Add Node</Button>
                            </form>
                        </DialogContent>
                    </Dialog>
                </CardHeader>
                <CardContent>
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                        {nodeList && nodeList
                            .filter(node => !deletedNodeIds.includes(node.id))
                            .map((node) => (
                                <Card key={node.id} className="cursor-pointer hover:bg-gray-50 transition-colors">
                                    <CardContent className="p-4">
                                        <div className="flex items-center justify-between">
                                            <h3 className="font-medium truncate">{node.name}</h3>
                                            <span
                                                className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${node.isActive
                                                        ? 'bg-green-100 text-green-800'
                                                        : 'bg-red-100 text-red-800'
                                                    }`}>
                                                {node.isActive ? 'Active' : 'Inactive'}
                                            </span>
                                        </div>
                                        <p className="text-sm text-gray-600 mt-2 line-clamp-2">{node.description}</p>
                                        <div className="mt-3 text-sm text-gray-500">
                                            <div className="flex items-center gap-1">
                                                <span className="font-mono truncate">{node.address}</span>
                                                {node.isLocal && (
                                                    <span
                                                        className="text-xs bg-blue-100 text-blue-800 px-2 py-0.5 rounded-full">
                                                        Local
                                                    </span>
                                                )}
                                            </div>
                                        </div>
                                    </CardContent>
                                    <CardFooter>
                                        <DropdownMenu>
                                            <DropdownMenuTrigger asChild><Button>Manage</Button></DropdownMenuTrigger>
                                            <DropdownMenuContent>
                                                <DropdownMenuItem onSelect={(e) => handleSetupNode(node.id, e)}>Setup Script</DropdownMenuItem>
                                                <DropdownMenuItem onSelect={(e) => handleActivateNode(node.id, e)}><a>Activate</a></DropdownMenuItem>
                                                <DropdownMenuItem onSelect={(e) => handleDeleteNode(node.id, e)} className="text-red-600">
                                                    Delete
                                                </DropdownMenuItem>
                                            </DropdownMenuContent>
                                        </DropdownMenu>
                                    </CardFooter>
                                </Card>
                            ))}
                    </div>
                </CardContent>
            </Card>

            <Dialog open={isSetupDialogOpen} onOpenChange={setIsSetupDialogOpen}>
                <DialogContent className="sm:max-w-lg">
                    <DialogHeader>
                        <DialogTitle>Setup Script</DialogTitle>
                        <DialogDescription>
                            Run this script on your server to set up the node.
                        </DialogDescription>
                    </DialogHeader>
                    <div className="mt-4 max-h-[60vh] overflow-auto">
                        <pre className="bg-gray-100 p-4 rounded-md overflow-x-auto">
                            <code className="text-sm">{setupScript}</code>
                        </pre>
                    </div>
                    <div className="mt-4 flex justify-end gap-2">
                        <Button onClick={handleCopyScript} variant="outline">
                            {copied ? (
                                <>
                                    <Check className="mr-2 h-4 w-4" /> Copied!
                                </>
                            ) : (
                                <>
                                    <Copy className="mr-2 h-4 w-4" /> Copy Script
                                </>
                            )}
                        </Button>
                        <Button onClick={handleDownloadScript}>
                            Download Script
                        </Button>
                    </div>
                </DialogContent>
            </Dialog>

            {/* Activation steps progress bar at the bottom */}
            <div style={{
                position: "fixed",
                left: 0,
                right: 0,
                bottom: 0,
                zIndex: 1000,
                pointerEvents: "none"
            }}>
                {activationSteps.length > 0 && (
                    <div className="flex flex-col items-center mb-4">
                        {activationSteps.slice(-5).map((step, idx) => (
                            <div
                                key={step.ts + step.nodeId + step.step}
                                className="bg-gray-900 text-white px-4 py-2 rounded shadow mb-1 pointer-events-auto"
                                style={{ minWidth: 300, maxWidth: 600 }}
                            >
                                <span className="font-mono text-xs text-gray-400">{step.nodeId.slice(0, 8)}</span>
                                <span className="ml-2 font-semibold">{step.step.replace(/_/g, " ")}</span>
                                {step.message && <span className="ml-2">{step.message}</span>}
                            </div>
                        ))}
                    </div>
                )}
            </div>
        </>
    )
}