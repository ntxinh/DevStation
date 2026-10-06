FROM node:22-alpine
WORKDIR /app
RUN corepack enable
# NEXT_PUBLIC_* is inlined at build time — must be a build ARG, not runtime env
ARG NEXT_PUBLIC_API_URL=http://localhost:5080
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
COPY . .
RUN cd apps/web && pnpm i --frozen-lockfile && pnpm build
EXPOSE 3000
CMD ["pnpm", "--dir", "apps/web", "start"]
