"use client";

import {createContext, useEffect, useState} from "react";
import {usePathname, useRouter} from "next/navigation";
import {isPathRestricted, getProfile} from "@/lib/utils";

const refreshEndpoint = process.env.NEXT_PUBLIC_REFRESH_URL;

const getNewToken = async (refreshToken: string): Promise<any> => {
    if (!refreshEndpoint) {
        return null;
    }
    const res = await fetch(refreshEndpoint, {
        method: "POST",
        headers: {'Content-Type': 'application/json'},
        body: JSON.stringify({refreshToken: refreshToken}),
    })
    try {
        const {expiresIn: newExpiresIn, accessToken: newAccessToken, refreshToken: newRefreshToken} = await res.json()
        return {newExpiresIn, newAccessToken, newRefreshToken};
    } catch (error) {
        return false;
    }
}

interface TokenContext {
    accessToken: string | null,
    refreshToken: string | null,
    profile: Profile | null,
    setAccessToken: (token: string | null) => void,
    setRefreshToken: (token: string | null) => void,
    setProfile: (token: Profile | null) => void,
    setExpiresAt: (token: number | null) => void,
}

export const TokenContext = createContext<TokenContext | undefined>(undefined);

export default ({children}: { children: React.ReactNode }) => {
    const pathName = usePathname();
    const router = useRouter();

    const [accessToken, setAccessToken] = useState<string | null>(() =>
        typeof window !== 'undefined' ? localStorage.getItem('accessToken') : null
    );
    const [refreshToken, setRefreshToken] = useState<string | null>(() =>
        typeof window !== 'undefined' ? localStorage.getItem('refreshToken') : null
    );
    const [profile, setProfile] = useState<Profile | null>(() => {
        if (typeof window !== 'undefined') {
            const savedProfile = localStorage.getItem('profile');
            return savedProfile ? JSON.parse(savedProfile) : null;
        }
        return null;
    });
    const [expiresAt, setExpiresAt] = useState<number | null>(() => {
        if (typeof window !== 'undefined') {
            const saved = localStorage.getItem('expiresAt');
            return saved ? parseInt(saved, 10) : null;
        }
        return null;
    });

    // Update localStorage when tokens change
    useEffect(() => {
        if (accessToken) {
            localStorage.setItem('accessToken', accessToken);
        } else {
            localStorage.removeItem('accessToken');
        }
    }, [accessToken]);

    useEffect(() => {
        if (refreshToken) {
            localStorage.setItem('refreshToken', refreshToken);
        } else {
            localStorage.removeItem('refreshToken');
        }
    }, [refreshToken]);

    useEffect(() => {
        if (profile) {
            localStorage.setItem('profile', JSON.stringify(profile));
        } else {
            localStorage.removeItem('profile');
        }
    }, [profile]);

    useEffect(() => {
        if (accessToken) {
            const fetchProfile = (async () => {
                try {
                    const newProfile = await getProfile(accessToken)
                    setProfile(newProfile)
                } catch (e) {
                    setAccessToken(null);
                }
            });

            const interval = setInterval(fetchProfile, 5 * 60 * 180);

            return () => clearInterval(interval);
        }
    }, [accessToken]);

    useEffect(() => {
        if (expiresAt) {
            localStorage.setItem('expiresAt', expiresAt.toString());
        } else {
            localStorage.removeItem('expiresAt');
        }
    }, [expiresAt]);

    useEffect(() => {
        const fetchNewToken = async () => {
            if (!accessToken) {
                if (refreshToken) {
                    // Get access-token
                    const {newExpiresIn, newAccessToken, newRefreshToken} = await getNewToken(refreshToken);
                    if (newAccessToken) {
                        const date = new Date();
                        const newExpiresAt = date.getTime() + newExpiresIn * 1000;
                        setAccessToken(newAccessToken);
                        setRefreshToken(newRefreshToken);
                        setExpiresAt(newExpiresAt);
                    } else {
                        router.push('/login');
                    }
                } else {
                    if (isPathRestricted(pathName))
                        router.push('/login');
                }
            }
        };
        fetchNewToken();
    }, [accessToken, refreshToken]);

    return (
        <TokenContext.Provider
            value={{accessToken, refreshToken, profile, setAccessToken, setRefreshToken, setProfile, setExpiresAt}}>
            {children}
        </TokenContext.Provider>
    );
}