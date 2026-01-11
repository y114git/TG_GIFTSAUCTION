#!/bin/sh
echo "Waiting for MongoDB to be ready..."
# Simple wait loop (or rely on docker-compose healthcheck + depends_on condition)
# But for safety we can just allow crash & restart, or wait.
# We'll rely on docker-compose 'service_healthy'.

echo "Seeding database..."
npx ts-node src/scripts/seed.ts

echo "Starting server..."
node dist/index.js
