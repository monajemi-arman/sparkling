const USER_URL = process.env.NEXT_PUBLIC_USER_URL;

export const getUsers = async (accessToken: string): Promise<User[] | number> => {
    if (accessToken) {
        const res = await fetch(`${USER_URL}`, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${accessToken}`,
            }
        })
        if (res.status === 200)
            return await res.json();
        else {
            console.error(res.statusText, await res.text());
            return res.status;
        }
    }
    return [];
}

export const getUser = async (accessToken: string, userId: string): Promise<User | null | number> => {
    if (accessToken) {
        const res = await fetch(`${USER_URL}/${userId}`, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${accessToken}`,
            }
        })
        if (res.status === 200)
            return await res.json();
        else {
            console.error(res.statusText, await res.text());
            return res.status;
        }
    }
    return null;
}

export const deleteUser = async (accessToken: string, userId: string): Promise<boolean> => {
    if (accessToken) {
        const res = await fetch(`${USER_URL}/${userId}`, {
            method: 'DELETE',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${accessToken}`,
            }
        })
        if (res.status === 200)
            return true;
        else {
            console.error(res.statusText, await res.text());
        }
    }
    return false;
}

export const addUser = async (
    accessToken: string, credentials: {
        email: string,
        password: string,
    }): Promise<boolean> => {
    if (accessToken) {
        const res = await fetch(`${USER_URL}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${accessToken}`,
            },
            body: JSON.stringify(credentials),
        })
        if (res.status === 200)
            return true
        else {
            console.error(res.statusText, await res.text());
        }
    }
    return false;
}

export const updateUser = async (accessToken: string, userId: string, user: UpdateUser): Promise<boolean> => {
    if (accessToken) {
        const res = await fetch(`${USER_URL}/${userId}`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${accessToken}`,
            },
            body: JSON.stringify(user),
        })
        if (res.status === 200)
            return true
        else {
            console.error(res.statusText, await res.text());
        }
    }
    return false;
}