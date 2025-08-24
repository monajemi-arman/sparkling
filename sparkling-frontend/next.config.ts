import type { NextConfig } from "next";

const BACKEND_API_DIRECT = process.env.BACKEND_API_DIRECT

/** @type {import('next').NextConfig} */
const nextConfig = {
  output: "export",
  async rewrites() {
    return [
      {
        source: '/api/:path*',
        destination: `${BACKEND_API_DIRECT}/:path*`,
      },
    ]
  },
}

export default nextConfig;
