import { FastifyInstance } from 'fastify';
import { Auction, AuctionStatus } from '../models/Auction';
import { BidService } from '../services/BidService';
import { Bid, BidStatus } from '../models/Bid';

const auctionCreationCooldown = new Map<string, number>();
const AUCTION_COOLDOWN_MS = 5000;

const sanitizeString = (str: string, maxLength: number = 100): string => {
    // Убирает мусор и опасные символы из названия подарка.
    if (typeof str !== 'string') return '';
    return str.trim().slice(0, maxLength).replace(/[<>\"'`$\\]/g, '').replace(/\s+/g, ' ');
};

const isValidNumber = (val: any, min: number, max: number): boolean => {
    // Универсальная проверка числовых параметров из тела запроса.
    const num = Number(val);
    return !isNaN(num) && isFinite(num) && num >= min && num <= max;
};

const userLocks = new Map<string, Promise<any>>();
const withUserLock = async <T>(userId: string, fn: () => Promise<T>): Promise<T> => {
    // Последовательная очередь действий одного пользователя (в первую очередь — ставки).
    const prev = userLocks.get(userId) || Promise.resolve();
    const current = prev.then(() => fn()).catch((e) => { throw e; });
    userLocks.set(userId, current.catch(() => {}));
    try { return await current; } finally {
        if (userLocks.get(userId) === current.catch(() => {})) userLocks.delete(userId);
    }
};

export async function auctionRoutes(fastify: FastifyInstance) {
    fastify.get('/auctions', async (req, reply) => {
        // Список аукционов, которые ещё имеют смысл показывать в интерфейсе.
        const auctions = await Auction.find({ status: { $ne: AuctionStatus.FINISHED } });
        return auctions;
    });

    fastify.get('/auctions/:id', async (req, reply) => {
        const { id } = req.params as { id: string };
        const auction = await Auction.findById(id);
        if (!auction) {
            reply.code(404);
            return { error: 'Not found' };
        }

        // Топ ставок показывается только по текущему раунду.
        const topBids = await Bid.find({
            auctionId: id,
            roundIndex: auction.currentRoundIndex,
            status: { $in: [BidStatus.ACTIVE] }
        }).sort({ amount: -1 }).limit(200).populate('userId', 'username');

        return { ...auction.toObject(), topBids };
    });

    fastify.post('/auctions/:id/bid', async (req, reply) => {
        const { id } = req.params as { id: string };
        const { amount } = req.body as { amount: number };
        const userId = req.headers['x-user-id'] as string;

        if (!userId) return reply.code(401).send({ error: 'Unauthorized' });

        if (!isValidNumber(amount, 1, 10000000)) {
            return reply.code(400).send({ error: 'Invalid bid amount' });
        }

        try {
            // Лок на пользователя защищает от одновременных ставок, которые могут рассинхронизировать блокировку средств.
            const bid = await withUserLock(userId, () => BidService.placeBid(userId, id, Math.floor(Number(amount))));
            return bid;
        } catch (e: any) {
            return reply.code(400).send({ error: e.message });
        }
    });

    fastify.post('/admin/auctions', async (req, reply) => {
        const userId = req.headers['x-user-id'] as string || 'anonymous';
        const now = Date.now();
        
        // Ограничение частоты создания аукционов по пользователю, чтобы не засорять базу и UI.
        const lastCreation = auctionCreationCooldown.get(userId);
        if (lastCreation && (now - lastCreation) < AUCTION_COOLDOWN_MS) {
            const waitTime = Math.ceil((AUCTION_COOLDOWN_MS - (now - lastCreation)) / 1000);
            return reply.code(429).send({ error: `Please wait ${waitTime} seconds before creating another auction` });
        }
        
        const data = req.body as any;

        const title = sanitizeString(data.title, 100);
        if (!title || title.length < 2) {
            return reply.code(400).send({ error: 'Gift name is required (2-100 characters)' });
        }

        const roundsCount = isValidNumber(data.roundsCount, 1, 1000) ? Number(data.roundsCount) : 1;
        const duration = isValidNumber(data.duration, 30000, 3600000) ? Number(data.duration) : 60000;
        const winnersCount = isValidNumber(data.winnersCount, 1, 100) ? Number(data.winnersCount) : 1;
        const minBid = isValidNumber(data.minBid, 1, 1000000) ? Number(data.minBid) : 1;

        const rounds = [];
        for (let i = 0; i < roundsCount; i++) {
            rounds.push({
                index: i,
                duration: duration,
                winnersCount: winnersCount,
                minBid: minBid,
                isFinalized: false
            });
        }

        const auction = await Auction.create({
            title: title,
            status: AuctionStatus.ACTIVE,
            rounds: rounds,
            currentRoundIndex: 0,
            totalWinnersNeeded: winnersCount
        });

        auctionCreationCooldown.set(userId, Date.now());
        
        return auction;
    });
}
