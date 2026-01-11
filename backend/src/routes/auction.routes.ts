import { FastifyInstance } from 'fastify';
import { Auction, AuctionStatus } from '../models/Auction';
import { BidService } from '../services/BidService';
import { Bid, BidStatus } from '../models/Bid';

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
        }).sort({ amount: -1 }).limit(10).populate('userId', 'username');

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

    // Admin: Create Auction
    fastify.post('/admin/auctions', async (req, reply) => {
        const data = req.body as any;

        // Config defaults
        const roundsCount = data.roundsCount || 1;
        const duration = data.duration || 60000;
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
        return auction;
    });
}
