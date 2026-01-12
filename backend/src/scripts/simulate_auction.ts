
import mongoose from 'mongoose';
import { Auction, AuctionStatus } from '../models/Auction';
import { User, IUser } from '../models/User';
import { Bid, BidStatus } from '../models/Bid';
import { Transaction } from '../models/Transaction';
import { AuctionEngine } from '../services/AuctionEngine';
import { PaymentService } from '../services/PaymentService';
import { BidService } from '../services/BidService';
import dotenv from 'dotenv';
import { connectDB } from '../db';

dotenv.config();

const NUM_USERS = 50;
const INITIAL_BALANCE = 10000;
const AUCTION_DURATION_MS = 30000; // 30 sec rounds for speed
const ROUNDS_COUNT = 3;

// Sleep utility
const sleep = (ms: number) => new Promise(r => setTimeout(r, ms));

async function runSimulation() {
    console.log('>>> STARTING AUCTION SIMULATION <<<');

    // Connect DB
    await connectDB();

    // Cleanup previous data
    await User.deleteMany({});
    await Auction.deleteMany({});
    await Bid.deleteMany({});
    await Transaction.deleteMany({});
    console.log('Cleaned DB.');

    // 1. Create Users
    console.log(`Creating ${NUM_USERS} users...`);
    const users: IUser[] = [];
    for (let i = 0; i < NUM_USERS; i++) {
        const user = await User.create({
            username: `bot_${i}`,
            balance: 0,
            lockedBalance: 0
        });
        await PaymentService.deposit(user._id.toString(), INITIAL_BALANCE);
        users.push(user);
    }
    console.log('Users created and funded.');

    // 2. Create Auction
    console.log('Creating Auction...');
    // Only 1 winner per round to keep it competitive
    const auctionConfig = {
        title: 'Simulation Rare Username',
        minBid: 10,
        winnersCount: 3, // 3 winners per round
        duration: AUCTION_DURATION_MS,
        roundsCount: ROUNDS_COUNT
    };

    // Create logic (mimic Controller)
    const rounds = [];
    for (let i = 0; i < auctionConfig.roundsCount; i++) {
        rounds.push({
            index: i,
            duration: auctionConfig.duration,
            winnersCount: auctionConfig.winnersCount,
            minBid: auctionConfig.minBid * (i + 1), // Scaling min bid
            isFinalized: false
        });
    }

    const auction = await Auction.create({
        title: auctionConfig.title,
        status: AuctionStatus.ACTIVE,
        rounds: rounds,
        currentRoundIndex: 0,
        totalWinnersNeeded: auctionConfig.winnersCount * auctionConfig.roundsCount
    });
    console.log(`Auction ${auction._id.toString()} created.`);

    // Start Engine Loop
    AuctionEngine.startEngine(1000); // 1s check interval

    // --- ROUND 1 ---
    console.log('\n--- ROUND 1 START ---');
    // First bid triggers start
    await BidService.placeBid(users[0]._id.toString(), auction._id.toString(), 100);
    console.log('Auction Clock Started by User 0');

    // Random bids
    const promisesR1 = [];
    for (let i = 1; i < 20; i++) {
        const amount = 100 + Math.floor(Math.random() * 500);
        promisesR1.push(
            BidService.placeBid(users[i]._id.toString(), auction._id.toString(), amount)
                .catch((e: any) => console.error(`R1 Bid Error for ${users[i].username}: ${e.message}`))
        );
    }
    await Promise.all(promisesR1);
    console.log('Round 1 Bids Placed.');

    // Wait for end of round 1
    console.log('Waiting for Round 1 to end...');
    await sleep(AUCTION_DURATION_MS + 2000); // Wait duration + buffer for engine

    // --- ROUND 2 ---
    console.log('\n--- ROUND 2 START ---');
    // Reload auction to check index
    let currentAuction = await Auction.findById(auction._id.toString());
    if (!currentAuction || currentAuction.currentRoundIndex !== 1) {
        console.warn('Warning: Round 1 might not have finished yet or auction finished early.');
    } else {
        console.log(`Current Round Index: ${currentAuction.currentRoundIndex}`);
    }

    // New Users Bidding
    const promisesR2 = [];
    for (let i = 20; i < 35; i++) {
        const amount = 500 + Math.floor(Math.random() * 1000);
        promisesR2.push(
            BidService.placeBid(users[i]._id.toString(), auction._id.toString(), amount)
                .catch((e: any) => console.error(`R2 New Bid Error: ${e.message}`))
        );
    }

    // Existing Users Upgrading (From Round 1 Losers/Winners)
    for (let i = 0; i < 10; i++) {
        const amount = 2000 + Math.floor(Math.random() * 500);
        promisesR2.push(
            BidService.placeBid(users[i]._id.toString(), auction._id.toString(), amount)
                .catch((e: any) => console.error(`R2 Upgrade Error: ${e.message}`))
        );
    }
    await Promise.all(promisesR2);
    console.log('Round 2 Bids Placed.');

    // Wait for end of round 2
    console.log('Waiting for Round 2 to end...');
    await sleep(AUCTION_DURATION_MS + 2000);

    // --- ROUND 3 (FINAL) ---
    console.log('\n--- ROUND 3 START ---');
    // Heavy Concurrency
    const promisesR3 = [];
    for (let i = 0; i < NUM_USERS; i++) {
        const amount = 3000 + Math.floor(Math.random() * 5000);
        promisesR3.push(
            BidService.placeBid(users[i]._id.toString(), auction._id.toString(), amount)
                .catch((e: any) => { }) // Ignore errors (e.g. low bid)
        );
    }
    await Promise.all(promisesR3);
    console.log('Round 3 Concurrency Bids Placed.');

    // Wait for finalization
    console.log('Waiting for Auction to Finish...');
    await sleep(AUCTION_DURATION_MS + 5000);

    // --- VERIFICATION ---
    console.log('\n>>> VERIFICATION <<<');

    // 1. Financial Integrity
    let totalDeposited = NUM_USERS * INITIAL_BALANCE;

    const allUsers = await User.find({});
    let totalBalances = 0;
    let totalLocked = 0;

    for (const u of allUsers) {
        totalBalances += u.balance;
        totalLocked += u.lockedBalance;
    }

    // We also need to count money that was "Captured" (Spent)
    // We can sum up WIN_CAPTURE transactions
    const captures = await Transaction.find({ type: 'WIN_CAPTURE' });
    let totalSpent = 0;
    for (const tx of captures) {
        totalSpent += Math.abs(tx.amount); // amount is negative usually
    }

    console.log(`Total Initial System Funds: ${totalDeposited}`);
    console.log(`Total User Balances Now:    ${totalBalances}`);
    console.log(`Total Locked Funds Now:     ${totalLocked}`);
    console.log(`Total Spent (Captured):     ${totalSpent}`);

    const currentSum = totalBalances + totalLocked + totalSpent;
    const diff = totalDeposited - currentSum;

    if (Math.abs(diff) < 0.01) {
        console.log('✅ FINANCIAL CHECK PASSED: Sum is exact.');
    } else {
        console.error(`❌ FINANCIAL CHECK FAILED: Diff is ${diff}`);
    }

    // 2. Winners Verification
    const winningBids = await Bid.find({ status: BidStatus.WINNER });
    console.log(`Total Winners: ${winningBids.length}`);
    // Should be Rounds * WinnersPerRound = 3 * 3 = 9. 
    // Unless not enough bidders? We had 50 users.
    if (winningBids.length === (ROUNDS_COUNT * auctionConfig.winnersCount)) {
        console.log('✅ WINNER COUNT CHECK PASSED');
    } else {
        console.warn(`⚠️ WINNER COUNT MISMATCH: Expected ${ROUNDS_COUNT * auctionConfig.winnersCount}, got ${winningBids.length}`);
    }

    // 3. Round Finalization
    // Auction should be deleted or marked finished?
    // Engine deletes it.
    const checkAuction = await Auction.findById(auction._id.toString());
    if (!checkAuction) {
        console.log('✅ AUCTION CLEANUP CHECK PASSED: Auction logic deleted document.');
    } else {
        console.log(`ℹ️ Auction document still exists (Status: ${checkAuction.status})`);
    }

    console.log('>>> SIMULATION COMPLETE <<<');
    process.exit(0);
}

runSimulation().catch(console.error);
