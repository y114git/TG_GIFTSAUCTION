import mongoose from 'mongoose';
import { Auction, AuctionStatus } from '../models/Auction';
import { connectDB } from '../db';
import dotenv from 'dotenv';

dotenv.config();

const seed = async () => {
    await connectDB();

    await Auction.deleteMany({});

    const ROUND_DURATION = 1000 * 60;

    const rounds = [];
    for (let i = 0; i < 3; i++) {
        rounds.push({
            index: i,
            duration: ROUND_DURATION,
            winnersCount: 3,
            minBid: 10,
            isFinalized: false
        });
    }

    const auction = await Auction.create({
        title: 'Limited Edition Blue Star',
        status: AuctionStatus.ACTIVE,
        rounds: rounds,
        currentRoundIndex: 0,
        createdAt: new Date()
    });

    console.log('Auction seeded:', auction.id);
    await mongoose.disconnect();
    process.exit(0);
};

seed();
