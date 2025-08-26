import {Work} from '@/types/work';

const workEndpoint = process.env.NEXT_PUBLIC_WORK_URL || 'http://localhost:5128/api/v0/work';

export const getWorks = async (accessToken: string | null | undefined): Promise<Work[] | number> => {
    if (accessToken) {
        const response = await fetch(workEndpoint, {
            method: 'GET',
            headers: {'Authorization': `Bearer ` + accessToken}
        });
        if (response.ok) {
            return await response.json();
        } else {
            console.log('Unable to fetch works: ', await response.text());
            return response.status;
        }
    } else {
        return [];
    }
}

export const getWork = async (accessToken: string | null | undefined, workId: string): Promise<Work | void> => {
    if (accessToken) {
        const response = await fetch(workEndpoint + `/${workId}`, {
            method: 'GET',
            headers: {'Authorization': `Bearer ` + accessToken}
        });
        if (response.ok) {
            return await response.json();
        } else {
            console.log('Unable to fetch works: ', await response.text());
        }
    }
}

export const addWork = async (accessToken: string | null | undefined): Promise<string> => {
    if (accessToken) {
        const response = await fetch(workEndpoint, {
            method: 'PUT',
            headers: {'Authorization': `Bearer ` + accessToken}
        });
        if (response.ok) {
            return await response.json();
        } else {
            console.log('Unable to add work: ', await response.text());
            return "";
        }
    } else {
        return "";
    }
}

export const deleteWork = async (accessToken: string | null | undefined, workId: string): Promise<boolean> => {
    if (accessToken && workId) {
        const response = await fetch(workEndpoint + `/${workId}`, {
            method: 'DELETE',
            headers: {'Authorization': `Bearer ` + accessToken}
        });
        if (response.ok) {
            return true;
        } else {
            console.log('Unable to add work: ', await response.text());
            return false;
        }
    } else {
        return false;
    }
}


export async function getJupyterInfo(accessToken: string, workId: string) {
    const res = await fetch(`${workEndpoint}/${workId}`, {
        headers: {
            "Content-type": "application/json",
            "Authorization": `Bearer ${accessToken}`,
        },
        method: "GET",
    });
    if (!res.ok) return null;
    const data = await res.json();
    return {
        port: data.jupyterPort,
        token: data.jupyterToken,
    };
}