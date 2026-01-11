import axios from 'axios';
import { connectDB } from '../../db';
import { User } from '../../models/User';
import { Auction, AuctionStatus } from '../../models/Auction';
import dotenv from 'dotenv';
import mongoose from 'mongoose';

dotenv.config();

const API_URL = 'http://localhost:3000';
const CONCURRENT_USERS = 500; // NodeJS might hit limit with 1000 without tuning, start with 500

async function massiveConcurrency() {
    await connectDB();
    console.log(`Starting Massive Concurrency Test with ${CONCURRENT_USERS} users...`);

    // 1. Setup: Create 500 users with balance
    console.log(' Creating users...');
    const userIds: string[] = [];
    const BATCH_SIZE = 100;

    // We create them in DB directly for speed, bypassing API for setup
    for (let i = 0; i < CONCURRENT_USERS; i += BATCH_SIZE) {
        const batch = [];
        for (let j = 0; j < BATCH_SIZE && (i + j) < CONCURRENT_USERS; j++) {
            batch.push({ username: `stress_user_${i + j}_${Date.now()}`, balance: 10000, lockedBalance: 0 });
        }
        const created = await User.insertMany(batch);
        userIds.push(...created.map(u => u.id));
    }
    console.log(` Created ${userIds.length} users.`);

    // 2. Setup: Create active auction
    const auction = await Auction.create({
        title: 'Stress Test Item',
        status: AuctionStatus.ACTIVE,
        rounds: [{
            index: 0,
            duration: 1000 * 60 * 5,
            startTime: new Date(),
            endTime: new Date(Date.now() + 1000 * 60 * 5), // 5 mins
            winnersCount: 5,
            minBid: 1,
            isFinalized: false
        }],
        currentRoundIndex: 0
    });
    console.log(` Created Auction ${auction.id}`);

    // 3. Execution: Everyone bids simultaneously
    console.log(' firing bids...');

    let successCount = 0;
    let failCount = 0;
    const start = Date.now();

    const bidPromises = userIds.map(async (userId, idx) => {
        try {
            await axios.post(`${API_URL}/auctions/${auction.id}/bid`, {
                amount: 10 + (idx % 100) // Randomize amounts slightly
            }, {
                headers: { 'x-user-id': userId }
            });
            successCount++;
        } catch (e: any) {
            // console.error(e.message);
            failCount++;
        }
    });

    await Promise.all(bidPromises);
    const duration = Date.now() - start;

    console.log('--- Results ---');
    console.log(`Duration: ${duration}ms`);
    console.log(`Successful Bids: ${successCount}`);
    console.log(`Failed Bids: ${failCount}`);
    console.log(`RPS: ${CONCURRENT_USERS / (duration / 1000)}`);

    // 4. Verification
    const dbAuction = await Auction.findById(auction.id);
    console.log('Auction State:', dbAuction?.status);

    process.exit(0);
}

massiveConcurrency();
