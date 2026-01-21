import { FastifyInstance } from 'fastify';
import { AuthService } from '../services/AuthService';
import { PaymentService } from '../services/PaymentService';
import { User } from '../models/User';
import { Bid, BidStatus } from '../models/Bid';

// Rate limiting for deposits/withdrawals (3 seconds per user)
const depositCooldown = new Map<string, number>();
const DEPOSIT_COOLDOWN_MS = 3000;

const sanitizeString = (str: string, maxLength: number = 50): string => {
    if (typeof str !== 'string') return '';
    return str.trim().slice(0, maxLength).replace(/[<>\"'`$\\]/g, '').replace(/\s+/g, ' ');
};

const isValidUsername = (username: string): boolean => {
    return /^[a-zA-Zа-яА-ЯёЁ0-9_-]{3,30}$/.test(username);
};

const userLocks = new Map<string, Promise<any>>();
const withUserLock = async <T>(userId: string, fn: () => Promise<T>): Promise<T> => {
    const prev = userLocks.get(userId) || Promise.resolve();
    const current = prev.then(() => fn()).catch((e) => { throw e; });
    userLocks.set(userId, current.catch(() => {}));
    try { return await current; } finally {
        if (userLocks.get(userId) === current.catch(() => {})) userLocks.delete(userId);
    }
};

export async function authRoutes(fastify: FastifyInstance) {

    fastify.post('/auth/login', async (req, reply) => {
        const { username } = req.body as { username: string };
        
        if (!username || typeof username !== 'string') {
            return reply.code(400).send({ error: 'Username is required' });
        }

        const cleanUsername = sanitizeString(username, 30);
        
        if (!isValidUsername(cleanUsername)) {
            return reply.code(400).send({ error: 'Invalid username. Use 3-30 alphanumeric characters, underscores or hyphens only.' });
        }

        const user = await AuthService.login(cleanUsername);
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

    fastify.get('/me/bids', async (req, reply) => {
        const userId = req.headers['x-user-id'] as string;
        if (!userId) {
            reply.code(401);
            return { error: 'Unauthorized' };
        }

        const { Bid, BidStatus } = await import('../models/Bid');
        const bids = await Bid.find({
            userId,
            status: BidStatus.ACTIVE
        });

        const bidMap: Record<string, number> = {};
        for (const b of bids) {
            bidMap[b.auctionId.toString()] = b.amount;
        }
        return bidMap;
    });

    fastify.get('/me/inventory', async (req, reply) => {
        const userId = req.headers['x-user-id'] as string;
        if (!userId) {
            reply.code(401);
            return { error: 'Unauthorized' };
        }

        const { Bid, BidStatus } = await import('../models/Bid');
        const winnings = await Bid.find({
            userId,
            status: BidStatus.WINNER
        }).populate('auctionId');

        const userIds = winnings.map(w => w.userId);
        const users = await User.find({ _id: { $in: userIds } });
        const userMap = new Map(users.map(u => [u._id.toString(), u.username]));

        return winnings.map(w => {
            const auctionTitle = (w.auctionId as any)?.title || w.snapshotTitle || 'Unknown Gift';

            return {
                bidId: w._id,
                amount: w.amount,
                date: w.createdAt,
                winnerUsername: userMap.get(w.userId.toString()) || 'Unknown',
                auction: {
                    title: auctionTitle
                }
            };
        });
    });

    fastify.post('/me/deposit', async (req, reply) => {
        const userId = req.headers['x-user-id'] as string;
        const { amount } = req.body as { amount: number };

        if (!userId) return reply.code(401).send({ error: 'Unauthorized' });
        
        const lastDeposit = depositCooldown.get(userId);
        const now = Date.now();
        if (lastDeposit && (now - lastDeposit) < DEPOSIT_COOLDOWN_MS) {
            const waitTime = Math.ceil((DEPOSIT_COOLDOWN_MS - (now - lastDeposit)) / 1000);
            return reply.code(429).send({ error: `Please wait ${waitTime} seconds` });
        }
        depositCooldown.set(userId, now);
        
        const numAmount = Number(amount);
        if (!amount || isNaN(numAmount) || !isFinite(numAmount) || numAmount === 0) {
            return reply.code(400).send({ error: 'Invalid amount' });
        }
        
        if (numAmount < -100000 || numAmount > 100000) {
            return reply.code(400).send({ error: 'Amount must be between -100000 and 100000' });
        }

        try {
            const user = await withUserLock(userId, () => PaymentService.deposit(userId, Math.floor(numAmount)));
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

        const cleanRecipient = sanitizeString(recipientUsername, 30);
        if (!isValidUsername(cleanRecipient)) {
            return reply.code(400).send({ error: 'Invalid recipient username' });
        }

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
            const recipient = await User.findOne({ username: cleanRecipient });
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

            return { success: true, message: `Gift transferred to ${cleanRecipient}` };
        } catch (e: any) {
            return reply.code(500).send({ error: e.message });
        }
    });
}
