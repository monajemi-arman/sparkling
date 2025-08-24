import {clsx, type ClassValue} from "clsx"
import {twMerge} from "tailwind-merge"
import { restrictedPaths } from "@/app/config";

const profileEndpoint = process.env.NEXT_PUBLIC_PROFILE_URL;

export function cn(...inputs: ClassValue[]) {
    return twMerge(clsx(inputs))
}

export function isPathRestricted(path: string) {
    return restrictedPaths.some((item) => path.startsWith(item));
}

export const getProfile = async (accessToken: string): Promise<Profile | null> => {
    if (!profileEndpoint) {
        return null;
    }
    const res = await fetch(profileEndpoint, {
        method: "GET",
        headers: {'Authorization': `Bearer ${accessToken}`},
    })
    return await res.json();
}