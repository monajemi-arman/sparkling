"use client";

import {cn, getProfile} from "@/lib/utils"
import {Button} from "@/components/ui/button"
import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
} from "@/components/ui/card"
import {Input} from "@/components/ui/input"
import {Label} from "@/components/ui/label"
import useSWRMutation from 'swr/mutation';
import {useContext, useEffect, useState} from "react";
import {z} from 'zod';
import {redirect} from "next/navigation";
import {TokenContext} from "@/app/context/TokenProvider";
import {useRouter} from "next/navigation";

const loginEndpoint = process.env.NEXT_PUBLIC_LOGIN_ENDPOINT;

interface LoginFormProps {
    email: string,
    password: string,
}

interface LoginResponse {
    tokenType: string,
    accessToken: string,
    expiresIn: number,
    refreshToken: string
}

const loginFormSchema = z.object({
    email: z.string().email(),
    password: z.string()
})

function calculateExpiresAt(expiresIn: number): number {
    return new Date().getTime() + expiresIn * 1000;
}

async function sendRequest(url: string, {arg}: { arg: LoginFormProps }): Promise<LoginResponse> {
    const res = await fetch(url, {
        method: "POST",
        headers: {'Content-Type': 'application/json'},
        body: JSON.stringify(arg),
    })
    return res.json();
}

export function LoginForm({
                              className,
                              ...props
                          }: React.ComponentProps<"div">) {

    const [email, setEmail] = useState<string>("");
    const [password, setPassword] = useState<string>("");
    const [loginSuccess, setLoginSuccess] = useState<boolean>(false);
    const tokenContext = useContext(TokenContext);
    const setAccessToken = tokenContext?.setAccessToken;
    const setRefreshToken = tokenContext?.setRefreshToken;
    const setExpiresAt = tokenContext?.setExpiresAt;
    const setProfile = tokenContext?.setProfile;

    const router = useRouter();

    const handleChangeEmail = (e: React.ChangeEvent<HTMLInputElement>) => {
        setEmail(e.target.value);
    }
    const handleChangePassword = (e: React.ChangeEvent<HTMLInputElement>) => {
        setPassword(e.target.value);
    }

    const {trigger, isMutating, error} = useSWRMutation(loginEndpoint, sendRequest);

    const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
        e.preventDefault();
        try {
            const loginFormProps: LoginFormProps = {email, password};
            const loginFormPropsSafe = loginFormSchema.safeParse(loginFormProps);
            if (loginFormPropsSafe.success) {
                const result = await trigger(loginFormPropsSafe.data);
                if (result && tokenContext) {
                    if (setAccessToken && setRefreshToken && setExpiresAt && setProfile) {
                        setAccessToken(result.accessToken);
                        setRefreshToken(result.refreshToken);
                        setExpiresAt(calculateExpiresAt(result.expiresIn));
                        const profile = await getProfile(result.accessToken);
                        setProfile(profile);
                        setLoginSuccess(true);
                    }
                }
            } else {
                alert("Bad inputs!");
                console.log({email, password})
            }
        } catch (error) {
            alert("Login failed!");
            console.log(error);
        }

    }

    useEffect(() => {
        if (loginSuccess) {
            router.push('/dashboard');
        }
    }, [loginSuccess, router]);

    return (
        <div className={cn("flex flex-col gap-6", className)} {...props}>
            <Card>
                <CardHeader>
                    <CardTitle>Login to your account</CardTitle>
                </CardHeader>
                <CardContent>
                    <form onSubmit={handleSubmit}>
                        <div className="flex flex-col gap-6">
                            <div className="grid gap-3">
                                <Label htmlFor="email">Email</Label>
                                <Input
                                    id="email"
                                    type="email"
                                    placeholder="m@example.com"
                                    onChange={handleChangeEmail}
                                    required
                                />
                            </div>
                            <div className="grid gap-3">
                                <div className="flex items-center">
                                    <Label htmlFor="password">Password</Label>
                                </div>
                                <Input id="password" type="password" required onChange={handleChangePassword}/>
                            </div>
                            <div className="flex flex-col gap-3">
                                <Button type="submit" className="w-full">
                                    Login
                                </Button>
                            </div>
                        </div>
                    </form>
                </CardContent>
            </Card>
        </div>
    )
}
