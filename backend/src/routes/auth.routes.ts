import { FastifyInstance } from 'fastify';
import { AuthService } from '../services/AuthService';
import { PaymentService } from '../services/PaymentService';
import { User } from '../models/User';
import { Bid, BidStatus } from '../models/Bid';

const depositCooldown = new Map<string, number>();
const DEPOSIT_COOLDOWN_MS = 3000;

const sanitizeString = (str: string, maxLength: number = 50): string => {
    // Базовая чистка пользовательского ввода: обрезка, удаление потенциально опасных символов, нормализация пробелов.
    if (typeof str !== 'string') return '';
    return str.trim().slice(0, maxLength).replace(/[<>\"'`$\\]/g, '').replace(/\s+/g, ' ');
};

const isValidUsername = (username: string): boolean => {
    // Имя пользователя ограничено по длине и набору символов, чтобы не тянуть лишнее в UI/логи.
    return /^[a-zA-Zа-яА-ЯёЁ0-9_-]{3,30}$/.test(username);
};

const userLocks = new Map<string, Promise<any>>();
const withUserLock = async <T>(userId: string, fn: () => Promise<T>): Promise<T> => {
    // Последовательная очередь операций на одного пользователя (депозит/ставки), чтобы избежать гонок по балансу.
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
        // Текущий баланс пользователя. Авторизация упрощена до заголовка x-user-id.
        const userId = req.headers['x-user-id'] as string;
        if (!userId) {
            reply.code(401);
            return { error: 'Unauthorized' };
        }
        const user = await User.findById(userId);
        return user;
    });

    fastify.get('/me/bids', async (req, reply) => {
        // Карта активных ставок пользователя по аукционам: { auctionId: amount }.
        const userId = req.headers['x-user-id'] as string;
        if (!userId) {
            reply.code(401);
            return { error: 'Unauthorized' };
        }

        // Ленивый импорт модели позволяет избежать циклических зависимостей при старте.
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
        // Инвентарь: выигранные ставки (подарки), которые можно передать другому пользователю.
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
        
        // Простой rate-limit по пользователю, чтобы не забивать историю транзакций и не ловить гонки.
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
            // Операция баланса заворачивается в лок, чтобы параллельные запросы не перетёрли изменения.
            const user = await withUserLock(userId, () => PaymentService.deposit(userId, Math.floor(numAmount)));
            return user;
        } catch (e: any) {
            return reply.code(400).send({ error: e.message });
        }
    });

    fastify.post('/me/gift/transfer', async (req, reply) => {
        // Передача подарка работает через смену владельца у выигранной ставки.
        const userId = req.headers['x-user-id'] as string;
        const { bidId, recipientUsername } = req.body as { bidId: string; recipientUsername: string };

        if (!userId) return reply.code(401).send({ error: 'Unauthorized' });
        if (!bidId || !recipientUsername) return reply.code(400).send({ error: 'Missing bidId or recipientUsername' });

        const cleanRecipient = sanitizeString(recipientUsername, 30);
        if (!isValidUsername(cleanRecipient)) {
            return reply.code(400).send({ error: 'Invalid recipient username' });
        }

        try {
            const bid = await Bid.findOne({ 
                _id: bidId, 
                userId, 
                status: BidStatus.WINNER 
            });

            if (!bid) {
                return reply.code(404).send({ error: 'Gift not found in your inventory' });
            }

            const recipient = await User.findOne({ username: cleanRecipient });
            if (!recipient) {
                return reply.code(404).send({ error: 'User not found' });
            }

            if (recipient._id.toString() === userId) {
                return reply.code(400).send({ error: 'Cannot transfer to yourself' });
            }

            bid.userId = recipient._id;
            await bid.save();

            return { success: true, message: `Gift transferred to ${cleanRecipient}` };
        } catch (e: any) {
            return reply.code(500).send({ error: e.message });
        }
    });
}
