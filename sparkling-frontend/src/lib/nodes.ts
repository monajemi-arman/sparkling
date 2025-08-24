import {Node, NodeFormData} from '@/components/node-list';

const nodesEndpoint = process.env.NEXT_PUBLIC_NODES_URL || 'http://localhost:5128/api/v0/nodes';

export const getNodes = async (accessToken: string | null | undefined): Promise<Node[] | number> => {
    if (accessToken) {
        const response = await fetch(nodesEndpoint, {
            method: 'GET',
            headers: {'Authorization': `Bearer ` + accessToken}
        });
        if (response.ok) {
            return await response.json();
        } else {
            console.log('Unable to fetch nodes: ', await response.text());
            return response.status;
        }
    } else {
        return [];
    }
}

export const addNode = async (url: string, {arg}: { arg: NodeFormData }): Promise<boolean | number> => {
    const accessToken = url;
    const validatedData = arg;

    if (nodesEndpoint && accessToken) {
        const response = await fetch(nodesEndpoint, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ` + accessToken
            },
            body: JSON.stringify(validatedData),
        });

        if (!response.ok) {
            console.error(JSON.stringify(response.json()));
            return response.status;
        } else {
            return true;
        }
    }
    else {
        return false;
    }
}

export const setupNode = async (url: string, {arg}: { arg: string }): Promise<string | number> => {
    const accessToken = url;
    const nodeId = arg;

    const response = await fetch(nodesEndpoint + '/' + nodeId + '/script', {
        method: 'POST',
        headers: {"Authorization": `Bearer ` + accessToken},
        body: JSON.stringify(nodeId),
    });

    if (response.ok)
        return await response.text();
    else
        return response.status;
}

export const activateNode = async (url: string, {arg}: { arg: string }): Promise<boolean | number> => {
    const accessToken = url;
    const nodeId = arg;

    const response = await fetch(nodesEndpoint + '/' + nodeId + '/activate', {
        method: 'POST',
        headers: {"Authorization": `Bearer ` + accessToken},
        body: JSON.stringify(nodeId),
    })

    if (response.ok)
        return true;
    else
        return response.status;
    return false;
}

export async function deleteNode(accessToken: string, { arg: nodeId }: { arg: string }) {
    const res = await fetch(`/api/v0/nodes/${nodeId}`, {
        method: "DELETE",
        headers: {
            "Authorization": `Bearer ${accessToken}`,
        },
    });
    if (!res.ok) {
        return false;
    }
    return true;
}