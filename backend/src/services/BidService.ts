import { Bid, IBid, BidStatus } from '../models/Bid';
import { Auction, IAuction, AuctionStatus, IRound } from '../models/Auction';
import { PaymentService } from './PaymentService';
import mongoose from 'mongoose';

export class BidService {
    static async placeBid(userId: string, auctionId: string, amount: number): Promise<IBid> {
        // Основной сценарий ставки: проверка состояния аукциона/раунда, блокировка средств, запись ставки.
        const auction = await Auction.findById(auctionId);
        if (!auction) throw new Error('Auction not found');
        if (auction.status !== AuctionStatus.ACTIVE) throw new Error('Auction is not active');

        const currentRound = auction.rounds[auction.currentRoundIndex];
        if (!currentRound) throw new Error('Current round not found');

        const now = new Date();

        if (!currentRound.startTime) {
            // Первый бид в раунде запускает таймер: фиксируются startTime и endTime.
            currentRound.startTime = now;
            const duration = currentRound.duration || 60000;
            currentRound.endTime = new Date(now.getTime() + duration);
            auction.markModified('rounds');
            await auction.save();
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
        });

        if (existingBid) {
            if (amount <= existingBid.amount) {
                throw new Error(`New bid (${amount}) must be higher than existing bid (${existingBid.amount})`);
            }

            // Дозакрепляется только разница между новой и предыдущей суммой.
            const diff = amount - existingBid.amount;
            await PaymentService.lockFunds(userId, diff, `UPGRADE_BID:${existingBid.id}`);

            existingBid.amount = amount;
            existingBid.roundIndex = auction.currentRoundIndex;
            existingBid.snapshotTitle = auction.title;
            await existingBid.save();

            await this.checkAntiSniping(auction, currentRound, now);

            return existingBid;
        } else {
            // Для новой ставки блокируется вся сумма.
            await PaymentService.lockFunds(userId, amount, `NEW_BID:${auctionId}`);

            const bid = await Bid.create({
                auctionId,
                userId,
                amount,
                roundIndex: auction.currentRoundIndex,
                status: BidStatus.ACTIVE,
                snapshotTitle: auction.title
            });

            await this.checkAntiSniping(auction, currentRound, now);

            return bid;
        }
    }

    private static async checkAntiSniping(auction: IAuction, round: IRound, now: Date) {
        if (!round.endTime) return;

        // Антиснайпинг: если ставка прилетает в последние секунды, раунд продлевается.
        const SNIPE_WINDOW_MS = 30 * 1000;
        const EXTENSION_MS = 30 * 1000;

        const timeLeft = round.endTime.getTime() - now.getTime();
        if (timeLeft < SNIPE_WINDOW_MS && timeLeft > 0) {
            round.endTime = new Date(round.endTime.getTime() + EXTENSION_MS);
            auction.markModified('rounds');
            await auction.save();
            console.log(`Anti-sniping triggered for Auction ${auction._id}, Round ${round.index}. Extended to ${round.endTime}`);
        }
    }
}
