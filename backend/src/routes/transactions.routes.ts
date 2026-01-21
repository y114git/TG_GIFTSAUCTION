import { FastifyInstance } from 'fastify';
import { Transaction } from '../models/Transaction';

export async function transactionRoutes(fastify: FastifyInstance) {
    fastify.get('/me/transactions', async (req, reply) => {
        // История операций пользователя. Авторизация упрощена до заголовка x-user-id.
        const userId = req.headers['x-user-id'] as string;
        if (!userId) {
            reply.code(401);
            return { error: 'Unauthorized' };
        }

        try {
            // Выдаётся последняя история, чтобы не тянуть лишнее в интерфейс.
            const history = await Transaction.find({ userId })
                .sort({ createdAt: -1 })
                .limit(50);
            return history;
        } catch (e: any) {
            return reply.code(500).send({ error: e.message });
        }
    });

}
