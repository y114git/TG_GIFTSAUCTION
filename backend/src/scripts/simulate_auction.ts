
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
const AUCTION_DURATION_MS = 30000;
const ROUNDS_COUNT = 3;

const sleep = (ms: number) => new Promise(r => setTimeout(r, ms));

async function runSimulation() {
    console.log('>>> STARTING AUCTION SIMULATION <<<');

    await connectDB();

    await User.deleteMany({});
    await Auction.deleteMany({});
    await Bid.deleteMany({});
    await Transaction.deleteMany({});
    console.log('Cleaned DB.');

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

    console.log('Creating Auction...');
    const auctionConfig = {
        title: 'Simulation Rare Username',
        minBid: 10,
        winnersCount: 3,
        duration: AUCTION_DURATION_MS,
        roundsCount: ROUNDS_COUNT
    };

    const rounds = [];
    for (let i = 0; i < auctionConfig.roundsCount; i++) {
        rounds.push({
            index: i,
            duration: auctionConfig.duration,
            winnersCount: auctionConfig.winnersCount,
            minBid: auctionConfig.minBid * (i + 1),
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

    AuctionEngine.startEngine(1000);

    console.log('\n--- ROUND 1 START ---');
    await BidService.placeBid(users[0]._id.toString(), auction._id.toString(), 100);
    console.log('Auction Clock Started by User 0');

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

    console.log('Waiting for Round 1 to end...');
    await sleep(AUCTION_DURATION_MS + 2000);

    console.log('\n--- ROUND 2 START ---');
    let currentAuction = await Auction.findById(auction._id.toString());
    if (!currentAuction || currentAuction.currentRoundIndex !== 1) {
        console.warn('Warning: Round 1 might not have finished yet or auction finished early.');
    } else {
        console.log(`Current Round Index: ${currentAuction.currentRoundIndex}`);
    }

    const promisesR2 = [];
    for (let i = 20; i < 35; i++) {
        const amount = 500 + Math.floor(Math.random() * 1000);
        promisesR2.push(
            BidService.placeBid(users[i]._id.toString(), auction._id.toString(), amount)
                .catch((e: any) => console.error(`R2 New Bid Error: ${e.message}`))
        );
    }

    for (let i = 0; i < 10; i++) {
        const amount = 2000 + Math.floor(Math.random() * 500);
        promisesR2.push(
            BidService.placeBid(users[i]._id.toString(), auction._id.toString(), amount)
                .catch((e: any) => console.error(`R2 Upgrade Error: ${e.message}`))
        );
    }
    await Promise.all(promisesR2);
    console.log('Round 2 Bids Placed.');

    console.log('Waiting for Round 2 to end...');
    await sleep(AUCTION_DURATION_MS + 2000);

    console.log('\n--- ROUND 3 START ---');
    const promisesR3 = [];
    for (let i = 0; i < NUM_USERS; i++) {
        const amount = 3000 + Math.floor(Math.random() * 5000);
        promisesR3.push(
            BidService.placeBid(users[i]._id.toString(), auction._id.toString(), amount)
                .catch((e: any) => { })
        );
    }
    await Promise.all(promisesR3);
    console.log('Round 3 Concurrency Bids Placed.');

    console.log('Waiting for Auction to Finish...');
    await sleep(AUCTION_DURATION_MS + 5000);

    console.log('\n>>> VERIFICATION <<<');

    let totalDeposited = NUM_USERS * INITIAL_BALANCE;

    const allUsers = await User.find({});
    let totalBalances = 0;
    let totalLocked = 0;

    for (const u of allUsers) {
        totalBalances += u.balance;
        totalLocked += u.lockedBalance;
    }

    const captures = await Transaction.find({ type: 'WIN_CAPTURE' });
    let totalSpent = 0;
    for (const tx of captures) {
        totalSpent += Math.abs(tx.amount);
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

    const winningBids = await Bid.find({ status: BidStatus.WINNER });
    console.log(`Total Winners: ${winningBids.length}`);
    if (winningBids.length === (ROUNDS_COUNT * auctionConfig.winnersCount)) {
        console.log('✅ WINNER COUNT CHECK PASSED');
    } else {
        console.warn(`⚠️ WINNER COUNT MISMATCH: Expected ${ROUNDS_COUNT * auctionConfig.winnersCount}, got ${winningBids.length}`);
    }

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
