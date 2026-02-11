FROM node:alpine AS builder
WORKDIR /sparkling-frontend
COPY . .
RUN npm install
RUN npm run build

FROM docker.io/library/nginx:stable-alpine
COPY --from=builder /sparkling-frontend/out /www
COPY nginx.conf /etc/nginx/
COPY fullchain.pem /
COPY privkey.pem /