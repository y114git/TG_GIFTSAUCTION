import mongoose from 'mongoose';
import { User } from '../models/User';
import { Bid, BidStatus } from '../models/Bid';
import { connectDB } from '../db';
import dotenv from 'dotenv';

dotenv.config();

const repair = async () => {
    await connectDB();

    console.log('Starting Balance Repair...');

    const users = await User.find({});
    for (const user of users) {
        // Find all ACTIVE bids for this user
        const activeBids = await Bid.find({ userId: user._id, status: BidStatus.ACTIVE });

        let expectedLocked = 0;
        for (const bid of activeBids) {
            expectedLocked += bid.amount;
        }

        if (user.lockedBalance !== expectedLocked) {
            console.log(`Fixing User ${user.username} (${user._id}): Locked ${user.lockedBalance} -> ${expectedLocked}`);
            user.lockedBalance = expectedLocked;
            await user.save();
        }
    }

    console.log('Repair Complete.');
    await mongoose.disconnect();
    process.exit(0);
};

repair();
