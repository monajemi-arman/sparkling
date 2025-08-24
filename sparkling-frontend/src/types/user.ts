interface Profile {
    email: string,
    isAdmin: boolean,
    balanceByHour: number
}

interface User extends Profile {
    id: string
}

interface UpdateUser {
    newPassword?: string,
    balanceByHour?: number
}