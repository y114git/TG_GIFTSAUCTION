FROM node:22-alpine

WORKDIR /app

COPY package*.json ./

# Install dependencies including dev dependencies for compilation
RUN npm install

COPY . .

# Compile TypeScript
RUN npx tsc

# Expose port
EXPOSE 3000

# Copy startup script and make executable
COPY start.sh .
RUN chmod +x start.sh

# Use shell script as entrypoint
CMD ["./start.sh"]
