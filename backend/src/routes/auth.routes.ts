import { FastifyInstance } from 'fastify';
import { AuthService } from '../services/AuthService';
import { PaymentService } from '../services/PaymentService';
import { User } from '../models/User';
import { Bid, BidStatus } from '../models/Bid';

// Rate limiting for deposits/withdrawals (3 seconds per user)
const depositCooldown = new Map<string, number>();
const DEPOSIT_COOLDOWN_MS = 3000;

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

    // Adding Deposit Endpoint for testing (with rate limiting)
    fastify.post('/me/deposit', async (req, reply) => {
        const userId = req.headers['x-user-id'] as string;
        const { amount } = req.body as { amount: number };

        if (!userId) return reply.code(401).send({ error: 'Unauthorized' });
        
        // Rate limiting check
        const lastDeposit = depositCooldown.get(userId);
        const now = Date.now();
        if (lastDeposit && (now - lastDeposit) < DEPOSIT_COOLDOWN_MS) {
            const waitTime = Math.ceil((DEPOSIT_COOLDOWN_MS - (now - lastDeposit)) / 1000);
            return reply.code(429).send({ error: `Please wait ${waitTime} seconds` });
        }
        depositCooldown.set(userId, now);
        
        // Allow negative for withdrawal, but check for 0
        if (!amount || amount === 0) return reply.code(400).send({ error: 'Invalid amount' });

        try {
            const user = await PaymentService.deposit(userId, amount);
            return user;
        } catch (e: any) {
            return reply.code(400).send({ error: e.message });
        }
    });

    // Transfer gift to another user
    fastify.post('/me/gift/transfer', async (req, reply) => {
        const userId = req.headers['x-user-id'] as string;
        const { bidId, recipientUsername } = req.body as { bidId: string; recipientUsername: string };

        if (!userId) return reply.code(401).send({ error: 'Unauthorized' });
        if (!bidId || !recipientUsername) return reply.code(400).send({ error: 'Missing bidId or recipientUsername' });

        try {
            // Find the gift (winning bid) that belongs to the user
            const bid = await Bid.findOne({ 
                _id: bidId, 
                userId, 
                status: BidStatus.WINNER 
            });

            if (!bid) {
                return reply.code(404).send({ error: 'Gift not found in your inventory' });
            }

            // Find recipient by username
            const recipient = await User.findOne({ username: recipientUsername });
            if (!recipient) {
                return reply.code(404).send({ error: 'User not found' });
            }

            // Can't transfer to yourself
            if (recipient._id.toString() === userId) {
                return reply.code(400).send({ error: 'Cannot transfer to yourself' });
            }

            // Transfer the gift
            bid.userId = recipient._id;
            await bid.save();

            return { success: true, message: `Gift transferred to ${recipientUsername}` };
        } catch (e: any) {
            return reply.code(500).send({ error: e.message });
        }
    });
}
