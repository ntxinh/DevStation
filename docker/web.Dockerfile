FROM node:22-alpine
WORKDIR /app
RUN corepack enable
COPY . .
RUN cd apps/web && pnpm i --frozen-lockfile && pnpm build
EXPOSE 3000
CMD ["pnpm", "--dir", "apps/web", "start"]
