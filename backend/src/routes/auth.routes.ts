import { FastifyInstance } from 'fastify';
import { AuthService } from '../services/AuthService';
import { PaymentService } from '../services/PaymentService';
import { User } from '../models/User';

export async function authRoutes(fastify: FastifyInstance) {

    fastify.post('/auth/login', async (req, reply) => {
        const { username } = req.body as { username: string };
        const user = await AuthService.login(username);
        return user;
    });

    fastify.get('/me/balance', async (req, reply) => {
        const userId = req.headers['x-user-id'] as string;
        if (!userId) {
            reply.code(401);
            return { error: 'Unauthorized' };
        }
        const user = await User.findById(userId);
        return user;
    });

    // Inventory Endpoint
    fastify.get('/me/inventory', async (req, reply) => {
        const userId = req.headers['x-user-id'] as string;
        if (!userId) {
            reply.code(401);
            return { error: 'Unauthorized' };
        }

        // Find winning bids for this user
        // We really want the *Items* (Auctions) they won.
        // A bid with status WINNER means they won that round. 
        // We populate the auction details to show "Gift Name".
        const { Bid, BidStatus } = await import('../models/Bid');
        const winnings = await Bid.find({
            userId,
            status: BidStatus.WINNER
        }).populate('auctionId');

        return winnings.map(w => {
            // Because auction might be deleted, fall back to snapshotTitle
            const auctionTitle = (w.auctionId as any)?.title || w.snapshotTitle || 'Unknown Gift';

            return {
                bidId: w._id,
                amount: w.amount,
                date: w.createdAt,
                auction: {
                    title: auctionTitle
                }
            };
        });
    });

    // Adding Deposit Endpoint for testing
    fastify.post('/me/deposit', async (req, reply) => {
        const userId = req.headers['x-user-id'] as string;
        const { amount } = req.body as { amount: number };

        if (!userId) return reply.code(401).send({ error: 'Unauthorized' });
        // Allow negative for withdrawal, but check for 0
        if (!amount || amount === 0) return reply.code(400).send({ error: 'Invalid amount' });

        try {
            const user = await PaymentService.deposit(userId, amount);
            return user;
        } catch (e: any) {
            return reply.code(400).send({ error: e.message });
        }
    });
}
