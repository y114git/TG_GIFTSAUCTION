
import mongoose from 'mongoose';
import { Auction, AuctionStatus } from '../models/Auction';
import { User, IUser } from '../models/User';
import { Bid, BidStatus } from '../models/Bid';
import { BidService } from '../services/BidService';
import { PaymentService } from '../services/PaymentService';
import dotenv from 'dotenv';
import { connectDB } from '../db';
import { ObjectId } from 'mongodb';

dotenv.config();

// FORCE Direct Connection for host-side script to bypass Replica Set discovery issues (mongo vs localhost)
process.env.MONGO_URI = process.env.MONGO_URI || 'mongodb://127.0.0.1:27017/auction_db?directConnection=true';

const NAMES = [
    "Alex", "Sam", "Jordan", "Taylor", "Casey", "Morgan", "Riley", "Quinn", "Avery", "Peyton",
    "Elon", "Jeff", "Bill", "Mark", "Satya", "Sundar", "Tim", "Jensen", "Sam A.", "Vitalik"
];

// Configuration
const DEFAULT_BOT_COUNT = 10;
const INITIAL_BALANCE = 50000;
const MIN_SLEEP = 2000;
const MAX_SLEEP = 8000;
const AGGRESSION_FACTOR = 0.3; // 30% chance to bid even if winning (sniper/assurance)

const sleep = (ms: number) => new Promise(r => setTimeout(r, ms));

async function runSwarm() {
    console.log('>>> STARTING BOT SWARM <<<');

    // 1. Connect
    await connectDB();
    console.log('✅ Connected to MongoDB');

    // 2. Parse Args or Default
    const botCount = process.argv[2] ? parseInt(process.argv[2]) : DEFAULT_BOT_COUNT;
    console.log(`🤖 Initializing ${botCount} bots...`);

    // 3. Create/Get Bots
    const bots: IUser[] = [];
    for (let i = 0; i < botCount; i++) {
        const name = `Bot_${NAMES[i % NAMES.length]}_${i}`;
        let user = await User.findOne({ username: name });
        if (!user) {
            user = await User.create({
                username: name,
                balance: 0,
                lockedBalance: 0
            });
            // Initial funding
            await PaymentService.deposit(user._id.toString(), INITIAL_BALANCE);
            console.log(`   + Created ${name}`);
        } else {
            // Top up if broke
            if (user.balance < 1000) {
                await PaymentService.deposit(user._id.toString(), INITIAL_BALANCE);
                console.log(`   $ Refilled ${name}`);
            }
        }
        bots.push(user);
    }
    console.log(`✅ ${bots.length} active bots ready.`);

    // 4. Main Loop
    let running = true;
    process.on('SIGINT', () => { running = false; console.log('\nStopping swarm...'); });

    while (running) {
        try {
            // Find Active Auctions
            const activeAuctions = await Auction.find({ status: AuctionStatus.ACTIVE });

            if (activeAuctions.length === 0) {
                console.log('zzz No active auctions. Sleeping 5s...');
                await sleep(5000);
                continue;
            }

            // Shuffle bots so they don't act in same order
            const activeBots = [...bots].sort(() => Math.random() - 0.5);

            for (const bot of activeBots) {
                if (!running) break;

                // Refresh bot state
                const fresherBot = await User.findById(bot._id);
                if (!fresherBot) continue;

                // Pick a random auction to consider
                const auction = activeAuctions[Math.floor(Math.random() * activeAuctions.length)];

                // --- DECISION LOGIC ---

                // 1. Check if auction round is valid
                const currentRound = auction.rounds[auction.currentRoundIndex];
                if (!currentRound || currentRound.isFinalized) continue;

                // 2. Check if we are already winning enough?
                // Get top bids for this round? 
                // The Auction model stores topBids but they might need population or fetching.
                // Depending on schema, topBids might be just Ids or objects. 
                // Let's assume we need to be careful. Ideally we use BidService to check state or trusting the Auction doc.
                // For simplicity, we'll blindly bid if we feel like it, but let's try to be smart.
                // Let's just bid. If we are the top bidder, maybe we skip?

                // We'll read the latest auction state (re-fetch to be sure)
                const freshAuction = await Auction.findById(auction._id);
                if (!freshAuction) continue;

                const winnersCount = freshAuction.rounds[freshAuction.currentRoundIndex].winnersCount;

                // Fetch Top Bids manually
                const topBids = await Bid.find({
                    auctionId: freshAuction._id,
                    status: BidStatus.ACTIVE,
                    roundIndex: freshAuction.currentRoundIndex // Filter by current round!
                })
                    .sort({ amount: -1 })
                    .limit(winnersCount + 5) // Get enough to see if we are close
                    .populate('userId'); // We need username/id


                // Am I in top N?
                const myKey = fresherBot._id.toString();
                const myRank = topBids.findIndex((b: any) => {
                    const u = b.userId;
                    const uid = u._id ? u._id.toString() : u.toString();
                    return uid === myKey;
                });

                const amIWinning = myRank !== -1 && myRank < winnersCount;

                // Decision: 
                // If winning -> 30% chance to upgrade (aggression). 70% pass.
                // If losing -> 80% chance to bid.
                // If not participating -> 50% chance to join.

                let shouldBid = false;
                if (myRank === -1) {
                    // Not participating yet
                    shouldBid = Math.random() < 0.5;
                } else if (amIWinning) {
                    shouldBid = Math.random() < AGGRESSION_FACTOR;
                } else {
                    // Losing (bidded but rank too low)
                    shouldBid = Math.random() < 0.8;
                }

                if (shouldBid) {
                    // Calculate Bid Amount
                    const minBid = freshAuction.rounds[freshAuction.currentRoundIndex].minBid;

                    // If we have a previous bid, we need to top it up + delta, or just replace?
                    // The BidService usually handles "new bid higher than old". 
                    // Let's just calculate a target amount.

                    // Aim for: (MinBid OR CurrentTopBid) + Random Increment
                    let baseTarget = minBid;
                    if (topBids.length > 0) {
                        const topAmount = (topBids[0] as any).amount; // Cast to any to get amount
                        baseTarget = Math.max(minBid, topAmount);
                    }

                    // Random increment: 10 to 500
                    const increment = Math.floor(Math.random() * 500) + 10;
                    const bidAmount = baseTarget + increment;

                    // Can we afford it?
                    // Note: If we already have a bid, we only pay the difference, but let's check total balance 
                    // assuming worst case or let the service handle errors.
                    // Actually, if we upgrade, we need (NewAmount - OldAmount) in balance. 
                    // Simplified: just check if balance > 0 and hope for best. 
                    // Better: Check total available.

                    if (fresherBot.balance + fresherBot.lockedBalance >= bidAmount) {
                        try {
                            const result = await BidService.placeBid(fresherBot._id.toString(), freshAuction._id.toString(), bidAmount);
                            console.log(`   📝 ${fresherBot.username} bid ${bidAmount} on "${freshAuction.title}" (Round ${freshAuction.currentRoundIndex + 1})`);

                            // Sleep a bit so one bot doesn't spam all auctions instantly
                            await sleep(Math.random() * 500 + 200);

                        } catch (e: any) {
                            if (e.message.includes("funds")) {
                                console.log(`   ⚠️ ${fresherBot.username} out of funds for bid ${bidAmount}.`);
                            } else {
                                // console.error(`   ❌ ${fresherBot.username} failed: ${e.message}`);
                            }
                        }
                    } else {
                        console.log(`   📉 ${fresherBot.username} too poor to compete (Bal: ${fresherBot.balance}).`);
                    }
                }
            }

            // Global sleep between swarm cycles
            const cycleSleep = Math.floor(Math.random() * (MAX_SLEEP - MIN_SLEEP) + MIN_SLEEP);
            console.log(`... Swarm resting for ${cycleSleep / 1000}s ...`);
            await sleep(cycleSleep);

        } catch (e) {
            console.error("Swarm Error:", e);
            await sleep(5000);
        }
    }
}

runSwarm();
