import { FastifyInstance } from 'fastify';
import { Auction, AuctionStatus } from '../models/Auction';
import { BidService } from '../services/BidService';
import { Bid, BidStatus } from '../models/Bid';

// Rate limiting for auction creation (5 seconds per user)
const auctionCreationCooldown = new Map<string, number>();
const AUCTION_COOLDOWN_MS = 5000;

export async function auctionRoutes(fastify: FastifyInstance) {

    // List active auctions
    fastify.get('/auctions', async (req, reply) => {
        const auctions = await Auction.find({ status: { $ne: AuctionStatus.FINISHED } });
        return auctions;
    });

    // Get details
    fastify.get('/auctions/:id', async (req, reply) => {
        const { id } = req.params as { id: string };
        const auction = await Auction.findById(id);
        if (!auction) {
            reply.code(404);
            return { error: 'Not found' };
        }

        // Include top bids for current round context
        // We could aggregate or just simple query
        const topBids = await Bid.find({
            auctionId: id,
            roundIndex: auction.currentRoundIndex,
            status: { $in: [BidStatus.ACTIVE] }
        }).sort({ amount: -1 }).limit(200).populate('userId', 'username');

        return { ...auction.toObject(), topBids };
    });

    // Place bid
    fastify.post('/auctions/:id/bid', async (req, reply) => {
        const { id } = req.params as { id: string };
        const { amount } = req.body as { amount: number };
        const userId = req.headers['x-user-id'] as string;

        if (!userId) return reply.code(401).send({ error: 'Unauthorized' });

        try {
            const bid = await BidService.placeBid(userId, id, amount);
            return bid;
        } catch (e: any) { // Type 'any' used for error to access message safely in catch block in TS in this context
            return reply.code(400).send({ error: e.message });
        }
    });

    // Admin: Create Auction (with rate limiting)
    fastify.post('/admin/auctions', async (req, reply) => {
        const userId = req.headers['x-user-id'] as string || 'anonymous';
        const now = Date.now();
        
        // Rate limiting check
        const lastCreation = auctionCreationCooldown.get(userId);
        if (lastCreation && (now - lastCreation) < AUCTION_COOLDOWN_MS) {
            const waitTime = Math.ceil((AUCTION_COOLDOWN_MS - (now - lastCreation)) / 1000);
            return reply.code(429).send({ error: `Please wait ${waitTime} seconds before creating another auction` });
        }
        
        const data = req.body as any;

        // Validation (before setting cooldown)
        if (!data.title || data.title.trim().length === 0) {
            return reply.code(400).send({ error: 'Gift name is required' });
        }

        // Config defaults
        const roundsCount = data.roundsCount || 1;
        const duration = Math.max(data.duration || 60000, 30000); // minimum 30 seconds
        const winnersCount = data.winnersCount || 1;
        const minBid = data.minBid || 0;

        // Construct rounds dynamically
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
            title: data.title,
            status: AuctionStatus.ACTIVE,
            rounds: rounds,
            currentRoundIndex: 0,
            totalWinnersNeeded: winnersCount // simplified
        });

        // Set cooldown only after successful creation
        auctionCreationCooldown.set(userId, Date.now());
        
        return auction;
    });
}
