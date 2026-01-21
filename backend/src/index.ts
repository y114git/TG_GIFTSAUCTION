import fastify from 'fastify';
import cors from '@fastify/cors';
import dotenv from 'dotenv';
import { connectDB } from './db';
import { authRoutes } from './routes/auth.routes';
import { auctionRoutes } from './routes/auction.routes';
import { transactionRoutes } from './routes/transactions.routes';
import { AuctionEngine } from './services/AuctionEngine';

dotenv.config();

const server = fastify({ logger: true });

// CORS нужен фронтенду; x-user-id используется как простой идентификатор пользователя без полноценной авторизации.
server.register(cors, {
    origin: true,
    methods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS'],
    allowedHeaders: ['Content-Type', 'x-user-id'],
    credentials: true,
});

// Роуты разделены по доменам: авторизация/аукционы/история операций.
server.register(authRoutes);
server.register(auctionRoutes);
server.register(transactionRoutes);

server.get('/health', async (request, reply) => {
    return { status: 'ok' };
});

const start = async () => {
    try {
        await connectDB();

        // Фоновая проверка активных аукционов и закрытие раундов по таймеру.
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
