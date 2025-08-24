import { Button } from "@/components/ui/button"

export default function Home() {
    return (
        <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-gray-900 text-white p-4">
            <div className="flex flex-col items-center justify-center max-w-3xl mx-auto py-12 md:py-24 text-center">
                <div className="space-y-6">
                    <h1 className="text-5xl md:text-6xl font-extrabold tracking-tight leading-tight">Sparkling</h1>
                    <p className="text-lg md:text-xl text-gray-300 max-w-md">Easy Spark cluster usage & management</p>
                    <Button variant="default" size="lg" className="mt-4">
                        <a href={'/dashboard'}>Dashboard</a>
                    </Button>
                </div>
            </div>
        </div>
    )
}