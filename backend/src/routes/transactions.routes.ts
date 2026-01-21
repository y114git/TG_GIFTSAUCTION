import { FastifyInstance } from 'fastify';
import { Transaction } from '../models/Transaction';

export async function transactionRoutes(fastify: FastifyInstance) {

    // Get My Transactions
    fastify.get('/me/transactions', async (req, reply) => {
        const userId = req.headers['x-user-id'] as string;
        if (!userId) {
            reply.code(401);
            return { error: 'Unauthorized' };
        }

        try {
            const history = await Transaction.find({ userId })
                .sort({ createdAt: -1 })
                .limit(50);
            return history;
        } catch (e: any) {
            return reply.code(500).send({ error: e.message });
        }
    });

}
