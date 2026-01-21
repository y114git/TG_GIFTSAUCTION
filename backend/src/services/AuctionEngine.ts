import { Auction, AuctionStatus, IAuction, IRound } from '../models/Auction';
import { Bid, BidStatus } from '../models/Bid';
import { PaymentService } from './PaymentService';
import mongoose from 'mongoose';

export class AuctionEngine {
    static async resolveRound(auctionId: string) {
        // Итог раунда считается внутри транзакции: смена статусов ставок и списание/возврат средств должны быть атомарными.
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

            if (!currentRound.endTime) {
                await session.abortTransaction();
                return;
            }

            if (now < currentRound.endTime) {
                await session.abortTransaction();
                return;
            }

            console.log(`Resolving Round ${currentIndex} for Auction ${auctionId}`);

            const allBids = await Bid.find({
                auctionId: auction.id,
                roundIndex: currentIndex,
                status: BidStatus.ACTIVE
            }).sort({ amount: -1, createdAt: 1 }).session(session);

            // В рамках раунда учитывается одна ставка на пользователя (берётся максимальная, при равенстве — более ранняя).
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

                await PaymentService.captureFunds(
                    winBid.userId.toString(),
                    winBid.amount,
                    `WIN_AUCTION:${auction.id}_ROUND:${currentIndex}_BID:${winBid.id}`,
                    session
                );
            }

            // Возврат проигравших ставок выполняется только когда у аукциона нет следующего раунда.
            const hasNextRound = (currentIndex + 1) < auction.rounds.length;

            if (!hasNextRound) {
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

                await auction.deleteOne({ session });
                console.log(`Auction ${auction.id} finished and deleted.`);
            } else {
                auction.currentRoundIndex = currentIndex + 1;
                const nextRound = auction.rounds[auction.currentRoundIndex];
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

    static startEngine(intervalMs: number = 2000) {
        setInterval(async () => {
            const activeAuctions = await Auction.find({ status: AuctionStatus.ACTIVE });
            for (const auction of activeAuctions) {
                const currentRound = auction.rounds[auction.currentRoundIndex];

                // Защита от параллельного resolveRound по одному и тому же аукциону.
                if (AuctionEngine.processingQueue.has(auction.id)) continue;

                if (currentRound && !currentRound.isFinalized && currentRound.endTime && new Date() >= currentRound.endTime) {
                    AuctionEngine.processingQueue.add(auction.id);
                    try {
                        await AuctionEngine.resolveRound(auction.id);
                    } finally {
                        AuctionEngine.processingQueue.delete(auction.id);
                    }
                }
            }
        }, intervalMs);
        console.log('Auction Engine started...');
    }
}
