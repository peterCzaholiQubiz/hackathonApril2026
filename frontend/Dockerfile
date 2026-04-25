# Build stage
FROM node:20-alpine AS builder

WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npx ng build --configuration production

# Serve stage
FROM nginx:alpine

COPY --from=builder /app/dist/portfolio-thermometer/browser /usr/share/nginx/html

# Support Angular client-side routing + Railway $PORT
RUN printf 'server {\n  listen ${PORT};\n  root /usr/share/nginx/html;\n  index index.html;\n  location / {\n    try_files $uri $uri/ /index.html;\n  }\n}\n' > /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["/bin/sh", "-c", "export PORT=${PORT:-80} && envsubst '$PORT' < /etc/nginx/conf.d/default.conf > /tmp/default.conf && mv /tmp/default.conf /etc/nginx/conf.d/default.conf && nginx -g 'daemon off;'"]
