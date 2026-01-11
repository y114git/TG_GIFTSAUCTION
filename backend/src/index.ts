import fastify from 'fastify';
import cors from '@fastify/cors';
import dotenv from 'dotenv';
import { connectDB } from './db';
import { authRoutes } from './routes/auth.routes';
import { auctionRoutes } from './routes/auction.routes';
import { AuctionEngine } from './services/AuctionEngine';

dotenv.config();

const server = fastify({ logger: true });

server.register(cors, {
    origin: '*', // Allow all for demo
});

// Register Routes
server.register(authRoutes);
server.register(auctionRoutes);

// Health check
server.get('/health', async (request, reply) => {
    return { status: 'ok' };
});

const start = async () => {
    try {
        await connectDB();

        // Start the game loop
        AuctionEngine.startEngine();

        const port = process.env.PORT ? parseInt(process.env.PORT) : 3000;
        await server.listen({ port, host: '0.0.0.0' });
        console.log(`Server listening on ${port}`);
    } catch (err) {
        server.log.error(err);
        process.exit(1);
    }
};

start();
