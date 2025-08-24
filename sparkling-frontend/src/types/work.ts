export interface Work {
    id: string
    startTime: string
    endTime: string | null
    jupyterContainer: string | null
    jupyterContainerId: string | null
    user: string | null
    userId: string
    status: number
}