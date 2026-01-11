FROM node:22-alpine

WORKDIR /app

COPY package*.json ./

RUN npm install

COPY . .

# Build the app
RUN npm run build

# Expose port
EXPOSE 5173

# Serve the built app using 'preview' (or 'serve' package if installed)
# Vite preview listens on 4173 by default, we can change it to 5173 to match our compose config
CMD ["npm", "run", "preview", "--", "--port", "5173", "--host"]
