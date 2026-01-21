import { Auction, AuctionStatus, IAuction, IRound } from '../models/Auction';
import { Bid, BidStatus } from '../models/Bid';
import { PaymentService } from './PaymentService';
import mongoose from 'mongoose';

export class AuctionEngine {

    /**
     * Process round end logic.
     * Should be called by a scheduler or lazily on access/bid attempts if expired.
     * For this demo, we might need a polling loop or trigger.
     */
    static async resolveRound(auctionId: string) {
        const session = await mongoose.startSession();
        session.startTransaction();
        try {
            const auction = await Auction.findById(auctionId).session(session);
            if (!auction || auction.status !== AuctionStatus.ACTIVE) {
                await session.abortTransaction();
                return;
            }

            const currentIndex = auction.currentRoundIndex;
            const currentRound = auction.rounds[currentIndex];

            if (!currentRound || currentRound.isFinalized) {
                await session.abortTransaction();
                return;
            }

            const now = new Date();

            // Safety check: if no endTime, it hasn't started, nothing to resolve
            if (!currentRound.endTime) {
                await session.abortTransaction();
                return;
            }

            if (now < currentRound.endTime) {
                // Not ended yet
                await session.abortTransaction();
                return;
            }

            console.log(`Resolving Round ${currentIndex} for Auction ${auctionId}`);

            const allBids = await Bid.find({
                auctionId: auction.id,
                status: BidStatus.ACTIVE
            }).sort({ amount: -1, createdAt: 1 }).session(session);

            const uniqueBids = [];
            const seenUsers = new Set();
            for (const b of allBids) {
                if (!seenUsers.has(b.userId.toString())) {
                    seenUsers.add(b.userId.toString());
                    uniqueBids.push(b);
                }
            }

            const winnersCount = currentRound.winnersCount;
            const winners = uniqueBids.slice(0, winnersCount);

            const winnerIds = new Set(winners.map(w => w._id.toString()));
            const losers = allBids.filter(b => !winnerIds.has(b._id.toString()));

            for (const winBid of winners) {
                winBid.status = BidStatus.WINNER;
                await winBid.save({ session });

                // Capture funds - PASS SESSION
                await PaymentService.captureFunds(
                    winBid.userId.toString(),
                    winBid.amount,
                    `WIN_AUCTION:${auction.id}_ROUND:${currentIndex}_BID:${winBid.id}`,
                    session
                );
            }

            // 4. Process Losers / Carry Over
            // If there is a next round:
            // They stay ACTIVE. Their roundIndex in DB doesn't change (it was when they placed it). 
            // But logically they are now part of next round.
            // We don't need to do anything to them for them to be "carried over" if our query is "status: ACTIVE".

            // If NO next round:
            // Refund everyone else.
            const hasNextRound = (currentIndex + 1) < auction.rounds.length;

            if (!hasNextRound) {
                // End of Auction
                for (const loseBid of losers) {
                    loseBid.status = BidStatus.LOST;
                    await loseBid.save({ session });
                    await PaymentService.unlockFunds(
                        loseBid.userId.toString(),
                        loseBid.amount,
                        `REFUND_LOST:${auction.id}_BID:${loseBid.id}`,
                        session
                    );
                }

                // User Request: "Delete auction when last round ends"
                // strict delete
                await auction.deleteOne({ session });
                console.log(`Auction ${auction.id} finished and deleted.`);
            } else {
                // Prepare next round
                auction.currentRoundIndex = currentIndex + 1;
                // Optionally update start/end times of next round if they were relative?
                // Let's assume they are fixed absolute times for now or shifted?
                // Task doesn't specify. Often rounds are back-to-back.
                // If back-to-back, next round starts NOW.
                const nextRound = auction.rounds[auction.currentRoundIndex];
                // If nextRound.startTime was hardcoded in past/future, we might want to adjust it to ensure continuity?
                // Let's respect what's in DB for now, assuming creation logic handled it.

                currentRound.isFinalized = true;
                await auction.save({ session });
            }

            await session.commitTransaction();
            console.log(`Round ${currentIndex} resolved. ${winners.length} winners.`);

        } catch (error) {
            console.error('Error resolving round:', error);
            await session.abortTransaction();
        } finally {
            session.endSession();
        }
    }

    private static processingQueue = new Set<string>();

    /**
     * Run the loop to check for round endings. 
     * In production this should be a job queue or cron.
     * For demo, simplistic `setInterval`.
     */
    static startEngine(intervalMs: number = 2000) {
        setInterval(async () => {
            // Find active auctions with rounds that might have ended
            const activeAuctions = await Auction.find({ status: AuctionStatus.ACTIVE });
            for (const auction of activeAuctions) {
                const currentRound = auction.rounds[auction.currentRoundIndex];

                // Skip if already processing this auction
                if (AuctionEngine.processingQueue.has(auction.id)) continue;

                // Only resolve if round has started (endTime exists) and passed
                if (currentRound && !currentRound.isFinalized && currentRound.endTime && new Date() >= currentRound.endTime) {

                    // LOCK
                    AuctionEngine.processingQueue.add(auction.id);
                    try {
                        await AuctionEngine.resolveRound(auction.id);
                    } finally {
                        // UNLOCK
                        AuctionEngine.processingQueue.delete(auction.id);
                    }
                }
            }
        }, intervalMs);
        console.log('Auction Engine started...');
    }
}
