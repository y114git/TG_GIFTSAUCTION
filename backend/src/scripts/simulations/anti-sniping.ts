import axios from 'axios';
import { connectDB } from '../../db';
import { Auction, AuctionStatus } from '../../models/Auction';
import dotenv from 'dotenv';
import mongoose from 'mongoose';

dotenv.config();

const API_URL = 'http://localhost:3000';

async function antiSnipingTest() {
    await connectDB();
    console.log('--- Anti-Sniping Test ---');

    // 1. Create Auction ending soon
    const now = new Date();
    const END_SEC = 15; // Ends in 15 seconds
    const auction = await Auction.create({
        title: 'Anti-Snipe Test Item',
        status: AuctionStatus.ACTIVE,
        rounds: [{
            index: 0,
            duration: 1000 * END_SEC,
            startTime: now,
            endTime: new Date(now.getTime() + 1000 * END_SEC),
            winnersCount: 1,
            minBid: 1,
            isFinalized: false
        }],
        currentRoundIndex: 0
    });
    console.log(`Created Auction ${auction.id} ending in ${END_SEC}s`);

    // 2. Wait until 5 seconds left (Snipe Window is 30s usually, so this is inside window)
    console.log('Waiting 5 seconds...');
    await new Promise(r => setTimeout(r, 5000));

    // 3. Place Bid
    // We need a userId. Let's assume one exists or mock it in header
    // In real test we'd create one. For this script let's rely on a known ID or create on fly
    // Quick create user via DB
    const { User } = await import('../../models/User');
    const user = await User.create({ username: `sniper_${Date.now()}`, balance: 1000, lockedBalance: 0 });

    console.log(`Placing bid at ${(auction.rounds[0].endTime!.getTime() - Date.now()) / 1000}s remaining...`);

    await axios.post(`${API_URL}/auctions/${auction.id}/bid`, { amount: 50 }, {
        headers: { 'x-user-id': user.id }
    });

    // 4. Verify Extension
    const updatedAuction = await Auction.findById(auction.id);
    const newEndTime = updatedAuction?.rounds[0].endTime;
    console.log('Old EndTime:', auction.rounds[0].endTime!.toISOString());
    console.log('New EndTime:', newEndTime?.toISOString());

    if (newEndTime!.getTime() > auction.rounds[0].endTime!.getTime()) {
        console.log('SUCCESS: Round extended!');
    } else {
        console.error('FAIL: Round NOT extended.');
        process.exit(1);
    }

    process.exit(0);
}

antiSnipingTest();
