
import mongoose from 'mongoose';
import { Auction, AuctionStatus } from '../models/Auction';
import { User } from '../models/User';
import { Bid, BidStatus } from '../models/Bid';
import { AuctionEngine } from '../services/AuctionEngine';
import { BidService } from '../services/BidService';

const reproduce = async () => {
    try {
        await mongoose.connect('mongodb://localhost:27017/auction_db?directConnection=true');

        // 1. Setup
        await User.deleteMany({});
        await Auction.deleteMany({});
        await Bid.deleteMany({});

        const userA = await User.create({ username: 'UserA', balance: 1000, lockedBalance: 0 });
        const userB = await User.create({ username: 'UserB', balance: 1000, lockedBalance: 0 });

        const auction = await Auction.create({
            title: 'Repro Tied Bids',
            status: AuctionStatus.ACTIVE,
            rounds: [{
                index: 0,
                duration: 60000,
                winnersCount: 3,
                minBid: 10,
                isFinalized: false
            }],
            currentRoundIndex: 0
        });

        // 2. Place Tied Bids
        console.log('Placing Bids...');
        await BidService.placeBid(userA.id, auction.id, 100);
        await BidService.placeBid(userB.id, auction.id, 100);

        // 3. Resolve
        const currentRound = auction.rounds[0];
        await Auction.updateOne(
            { _id: auction.id },
            { $set: { "rounds.0.endTime": new Date(Date.now() - 5000) } }
        );

        console.log('Resolving...');
        await AuctionEngine.resolveRound(auction.id);

        // 4. Verify
        const check = await Auction.findById(auction.id);
        if (check) {
            console.error('FAILURE: Auction still exists!', check.status);
        } else {
            console.log('SUCCESS: Auction deleted.');
        }

    } catch (e) {
        console.error('CRASHED:', e);
    } finally {
        await mongoose.disconnect();
    }
};

reproduce();
