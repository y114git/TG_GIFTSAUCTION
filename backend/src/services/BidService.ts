import { Bid, IBid, BidStatus } from '../models/Bid';
import { Auction, IAuction, AuctionStatus, IRound } from '../models/Auction';
import { PaymentService } from './PaymentService';
import mongoose from 'mongoose';

export class BidService {
    static async placeBid(userId: string, auctionId: string, amount: number): Promise<IBid> {
        // Инвариант: нельзя допустить расхождение "деньги заблокированы, но ставка не создана" (или наоборот).
        // Проверки + lockFunds + create/update Bid + anti-sniping выполняются в одной MongoDB транзакции.
        const session = await mongoose.startSession();
        session.startTransaction();
        try {
            const auction = await Auction.findById(auctionId).session(session);
            if (!auction) throw new Error('Auction not found');
            if (auction.status !== AuctionStatus.ACTIVE) throw new Error('Auction is not active');

            const currentRound = auction.rounds[auction.currentRoundIndex];
            if (!currentRound) throw new Error('Current round not found');

            const now = new Date();

            if (!currentRound.startTime) {
                // Первый bid в раунде запускает таймер: фиксируются startTime и endTime.
                currentRound.startTime = now;
                const duration = currentRound.duration || 60000;
                currentRound.endTime = new Date(now.getTime() + duration);
                auction.markModified('rounds');
                await auction.save({ session });
            }

            if (currentRound.isFinalized) throw new Error('Round is finished');

            if (currentRound.endTime && now.getTime() > currentRound.endTime.getTime() + 2000) {
                throw new Error('Round is closed');
            }

            if (amount < currentRound.minBid) {
                throw new Error(`Minimum bid is ${currentRound.minBid}`);
            }

            // Один активный бид на пользователя в рамках аукциона: повторная ставка повышает сумму существующей.
            const existingBid = await Bid.findOne({
                auctionId,
                userId,
                status: BidStatus.ACTIVE
            }).session(session);

            if (existingBid) {
                if (amount <= existingBid.amount) {
                    throw new Error(`New bid (${amount}) must be higher than existing bid (${existingBid.amount})`);
                }

                // Повышение ставки: блокируется только разница, чтобы не блокировать сумму повторно.
                const diff = amount - existingBid.amount;
                await PaymentService.lockFunds(userId, diff, `UPGRADE_BID:${existingBid.id}`, session);

                existingBid.amount = amount;
                existingBid.roundIndex = auction.currentRoundIndex;
                existingBid.snapshotTitle = auction.title;
                await existingBid.save({ session });

                await this.checkAntiSniping(auction, currentRound, now, session);

                await session.commitTransaction();
                return existingBid;
            }

            // Для новой ставки блокируется вся сумма.
            await PaymentService.lockFunds(userId, amount, `NEW_BID:${auctionId}`, session);

            const bid = await Bid.create([
                {
                    auctionId,
                    userId,
                    amount,
                    roundIndex: auction.currentRoundIndex,
                    status: BidStatus.ACTIVE,
                    snapshotTitle: auction.title
                }
            ], { session });

            await this.checkAntiSniping(auction, currentRound, now, session);

            await session.commitTransaction();
            return bid[0];
        } catch (error) {
            await session.abortTransaction();
            throw error;
        } finally {
            session.endSession();
        }
    }

    private static async checkAntiSniping(
        auction: IAuction,
        round: IRound,
        now: Date,
        session?: mongoose.ClientSession
    ) {
        if (!round.endTime) return;

        // Антиснайпинг: если ставка прилетает в последние секунды, раунд продлевается.
        const SNIPE_WINDOW_MS = 15 * 1000;
        const EXTENSION_MS = 30 * 1000;

        const timeLeft = round.endTime.getTime() - now.getTime();
        if (timeLeft < SNIPE_WINDOW_MS && timeLeft > 0) {
            round.endTime = new Date(round.endTime.getTime() + EXTENSION_MS);
            auction.markModified('rounds');
            await auction.save({ session });
            console.log(`Anti-sniping triggered for Auction ${auction._id}, Round ${round.index}. Extended to ${round.endTime}`);
        }
    }
}
