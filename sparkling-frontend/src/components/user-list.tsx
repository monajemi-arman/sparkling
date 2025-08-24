"use client";

import {Card, CardContent, CardFooter, CardHeader} from "@/components/ui/card";
import {DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger} from "@/components/ui/dropdown-menu";
import {Button} from "@/components/ui/button";
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogHeader,
    DialogTitle,
    DialogTrigger
} from "@/components/ui/dialog";
import {useContext, useState} from "react";
import {Input} from "@/components/ui/input";
import {Label} from "@/components/ui/label";
import {Checkbox} from "@/components/ui/checkbox";
import useSWRMutation from "swr/mutation";
import {TokenContext} from "@/app/context/TokenProvider";
import {addUser, deleteUser, updateUser} from "@/lib/users";
import {z} from "zod";

const userSchema = z.object({
    email: z.string().email(),
    password: z.string(),
});
const updateUserSchema = z.object({
    id: z.string(),
    newPassword: z.string().optional(),
    balanceHours: z.number().optional()
})

type UserFormData = z.infer<typeof userSchema>;
type updateUserFormData = z.infer<typeof updateUserSchema>;

export function UserList(props: { userList: User[] }) {
    const userList = props.userList;
    const context = useContext(TokenContext);
    const accessToken = context?.accessToken;

    const [isChangePasswordOpen, setIsChangePasswordOpen] = useState<boolean>(false);
    const [selectedUserId, setSelectedUserId] = useState("");
    const [balanceUser, setBalanceUser] = useState(0);
    const [isBalanceFormOpen, setIsBalanceFormOpen] = useState(false);
    const [newPassword, setNewPassword] = useState("");

    // Form
    const [formData, setFormData] = useState<UserFormData>({
        email: '',
        password: ''
    });

    const {trigger: triggerAdd} = useSWRMutation(accessToken ? "addUser" : "", (url: string, {arg}: {
        arg: UserFormData
    }) => addUser(accessToken!, arg));

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        try {
            const validatedData = userSchema.parse(formData);
            const result = await triggerAdd(validatedData);
            if (result) {
                alert("User created!");
                console.log(result);
            } else {
                alert("Failed to create user!");
            }

            setFormData({
                email: '',
                password: ''
            });
        } catch (error) {
            console.error('Error adding user:', error);
        }
    };

    const {trigger: triggerUpdateUser} = useSWRMutation(
        accessToken ? "updateUser" : "",
        (url: string, {arg}: { arg: updateUserFormData }) =>
            updateUser(accessToken!, arg.id, {
                newPassword: arg?.newPassword,
                balanceByHour: arg?.balanceHours,
            })
    );

    const handleBalanceUser = async (userId: string, balance: number) => {
        const res = await triggerUpdateUser({id: userId, balanceHours: balance});
        if (res)
            alert("User balance updated!");
        else alert("Failed to update user!");
    }

    const handleChangePasswordUser = async (userId: string, newPassword: string) => {
        const res = await triggerUpdateUser({id: userId, newPassword: newPassword});
        if (res)
            alert("User password updated!");
        else alert("Failed to update user!");
    }

    const {trigger: triggerDeleteUser} = useSWRMutation(
        accessToken ? "updateUser" : "",
        (url: string, {arg}: { arg: string }) =>
            deleteUser(accessToken!, arg)
    );

    const handleRemoveUser = async (userId: string) => {
        const res = await triggerDeleteUser(userId);
        if (res)
            alert("User removed!");
        else alert("Failed to remove user!");
    }

    return (
        <Card id={'user-list'}>
            <CardHeader>
                User Management
                <Dialog>
                    <DialogTrigger asChild>
                        <Button className={'w-1/8'} variant="default">Add</Button>
                    </DialogTrigger>
                    <DialogContent>
                        <DialogHeader>
                            <DialogTitle>User details</DialogTitle>
                            <DialogDescription>
                                Fill out details to add to users...
                            </DialogDescription>
                        </DialogHeader>
                        <form onSubmit={handleSubmit} className="space-y-4">
                            <div className="space-y-2">
                                <Label htmlFor="name">Email</Label>
                                <Input
                                    id="email"
                                    value={formData.email}
                                    onChange={(e) => setFormData({...formData, email: e.target.value})}
                                    required
                                />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="description">Password</Label>
                                <Input
                                    id="description"
                                    value={formData.password}
                                    type="password"
                                    onChange={(e) => setFormData({...formData, password: e.target.value})}
                                />
                            </div>
                            <Button type="submit" className="w-full">Add User</Button>
                        </form>
                    </DialogContent>
                </Dialog>
            </CardHeader>
            <CardContent>
                {userList && userList.map((user: User) => (
                    <Card key={user.email} className={'w-xs'}>
                        <CardHeader>
                            {user.email}
                            <br></br>
                            ⏳ {user.isAdmin ? <>&infin;</> : user.balanceByHour}
                        </CardHeader>
                        <CardFooter>
                            <DropdownMenu>
                                <DropdownMenuTrigger asChild><Button>Manage</Button></DropdownMenuTrigger>
                                <DropdownMenuContent>
                                    <DropdownMenuItem>
                                        <button onClick={() => {
                                            setSelectedUserId(user.id);
                                            setIsBalanceFormOpen(true);
                                        }}>
                                            Modify Balance
                                        </button>
                                    </DropdownMenuItem>
                                    <DropdownMenuItem>
                                        <button onClick={() => {
                                            setSelectedUserId(user.id);
                                            setIsChangePasswordOpen(true);
                                        }}>
                                            Change Password
                                        </button>
                                    </DropdownMenuItem>
                                    <DropdownMenuItem>
                                        <button onClick={() => handleRemoveUser(user.id)}>
                                            Remove Account
                                        </button>
                                    </DropdownMenuItem>
                                </DropdownMenuContent>
                            </DropdownMenu>
                        </CardFooter>
                    </Card>
                ))}
            </CardContent>
            <Dialog open={isBalanceFormOpen} onOpenChange={setIsBalanceFormOpen}>
                <DialogContent>
                    <DialogTitle>
                        Modify Balance
                    </DialogTitle>
                    <form onSubmit={(e) => {
                        e.preventDefault();
                        return handleBalanceUser(selectedUserId, balanceUser);
                    }}>
                        <Input
                            id="balance"
                            value={balanceUser}
                            onChange={(e) => setBalanceUser(Number(e.target.value))}
                            required
                        />
                    </form>
                    <button type={"submit"}>Save</button>
                </DialogContent>
            </Dialog>
            <Dialog open={isChangePasswordOpen} onOpenChange={setIsChangePasswordOpen}>
                <DialogContent>
                    <DialogTitle>
                        Change Password
                    </DialogTitle>
                    <form onSubmit={(e) => {
                        e.preventDefault();
                        const tmpNewPassword = newPassword;
                        setNewPassword("");
                        return handleChangePasswordUser(selectedUserId, tmpNewPassword);
                    }}>
                        <Input
                            id="newPassword"
                            value={newPassword}
                            onChange={(e) => setNewPassword(e.target.value)}
                            required
                        />
                    </form>
                    <button type={"submit"}>Save</button>
                </DialogContent>
            </Dialog>
        </Card>
    )
}