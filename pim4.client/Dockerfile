FROM node:20-alpine AS build
WORKDIR /app

# Instala dependências
COPY pim4.client/package.json pim4.client/package-lock.json ./
RUN npm ci

# Copia código e injeta variável de build do backend
COPY pim4.client/ .
ARG VITE_API_BASE_URL=/api
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
RUN npm run build

FROM nginx:1.27-alpine AS runtime
WORKDIR /usr/share/nginx/html
COPY --from=build /app/dist .
COPY pim4.client/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
