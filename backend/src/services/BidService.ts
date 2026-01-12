import { Bid, IBid, BidStatus } from '../models/Bid';
import { Auction, IAuction, AuctionStatus, IRound } from '../models/Auction';
import { PaymentService } from './PaymentService';
import mongoose from 'mongoose';

export class BidService {
    /**
     * Place a bid on an active auction.
     */
    static async placeBid(userId: string, auctionId: string, amount: number): Promise<IBid> {

        // 1. Validations
        const auction = await Auction.findById(auctionId);
        if (!auction) throw new Error('Auction not found');
        if (auction.status !== AuctionStatus.ACTIVE) throw new Error('Auction is not active');

        const currentRound = auction.rounds[auction.currentRoundIndex];
        if (!currentRound) throw new Error('Current round not found');

        const now = new Date();

        // 1.5 Start round if needed (First bid triggers timer)
        if (!currentRound.startTime) {
            currentRound.startTime = now;
            // Use duration from model, default 60s
            const duration = currentRound.duration || 60000;
            currentRound.endTime = new Date(now.getTime() + duration);

            // Mark modified and save immediately so concurrency isn't an issue (optimistic locking will handle it)
            auction.markModified('rounds');
            await auction.save();
        }

        if (currentRound.isFinalized) throw new Error('Round is finished');

        // Check window (allow 2s buffer)
        if (currentRound.endTime && now.getTime() > currentRound.endTime.getTime() + 2000) {
            throw new Error('Round is closed');
        }

        if (amount < currentRound.minBid) {
            throw new Error(`Minimum bid is ${currentRound.minBid}`);
        }

        // 2. Lock Funds
        // This will throw if insufficient funds
        // We treat the bid as new. If user is upgrading a bid, we might need diff logic?
        // User rules: "You can increase your bid at any time".
        // Implementation: Ideally we find existing bid for this round and "top up".
        // For simplicity V1: Let's assume new bid. But wait, if they have a bid from previous round carried over?
        // We should probably check if user already has an ACTIVE bid in this auction/round.

        // Check for ANY existing active bid by this user in this auction
        // We do not filter by roundIndex because a bid from a previous round (if active) 
        // should be upgraded rather than creating a second simultaneous bid.
        const existingBid = await Bid.findOne({
            auctionId,
            userId,
            status: BidStatus.ACTIVE
        });

        if (existingBid) {
            // Upgrade scenario
            if (amount <= existingBid.amount) {
                throw new Error(`New bid (${amount}) must be higher than existing bid (${existingBid.amount})`);
            }

            const diff = amount - existingBid.amount;

            // Lock only the difference
            await PaymentService.lockFunds(userId, diff, `UPGRADE_BID:${existingBid.id}`);

            existingBid.amount = amount;
            existingBid.roundIndex = auction.currentRoundIndex; // Update to current round
            existingBid.snapshotTitle = auction.title;
            await existingBid.save();

            // Update Auction Round Anti-Sniping
            await this.checkAntiSniping(auction, currentRound, now);

            return existingBid;
        } else {
            // New Bid scenario
            await PaymentService.lockFunds(userId, amount, `NEW_BID:${auctionId}`);

            const bid = await Bid.create({
                auctionId,
                userId,
                amount,
                roundIndex: auction.currentRoundIndex,
                status: BidStatus.ACTIVE,
                snapshotTitle: auction.title
            });

            // Update Auction Round Anti-Sniping
            await this.checkAntiSniping(auction, currentRound, now);

            return bid;
        }
    }

    private static async checkAntiSniping(auction: IAuction, round: IRound, now: Date) {
        if (!round.endTime) return;

        const SNIPE_WINDOW_MS = 30 * 1000; // 30 seconds
        const EXTENSION_MS = 30 * 1000; // 30 seconds

        const timeLeft = round.endTime.getTime() - now.getTime();
        if (timeLeft < SNIPE_WINDOW_MS && timeLeft > 0) {
            // Extend
            round.endTime = new Date(round.endTime.getTime() + EXTENSION_MS);
            // We need to save the auction document inside the main rounds array
            // Mongoose subdoc update:
            auction.markModified('rounds');
            await auction.save();
            console.log(`Anti-sniping triggered for Auction ${auction._id}, Round ${round.index}. Extended to ${round.endTime}`);
        }
    }
}
