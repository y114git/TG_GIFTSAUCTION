
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

// directConnection помогает обойти проблемы с обнаружением replica set при запуске скрипта с хоста.
process.env.MONGO_URI = process.env.MONGO_URI || 'mongodb://127.0.0.1:27017/auction_db?directConnection=true';

const NAMES = [
    "Alex", "Sam", "Jordan", "Taylor", "Casey", "Morgan", "Riley", "Quinn", "Avery", "Peyton",
    "Elon", "Jeff", "Bill", "Mark", "Satya", "Sundar", "Tim", "Jensen", "Sam A.", "Vitalik"
];

const DEFAULT_BOT_COUNT = 10;
const INITIAL_BALANCE = 50000;
const MIN_SLEEP = 2000;
const MAX_SLEEP = 8000;
const AGGRESSION_FACTOR = 0.3;

const sleep = (ms: number) => new Promise(r => setTimeout(r, ms));

async function runSwarm() {
    console.log('>>> STARTING BOT SWARM <<<');

    await connectDB();
    console.log('✅ Connected to MongoDB');

    // Количество ботов можно передать первым аргументом командной строки.
    const botCount = process.argv[2] ? parseInt(process.argv[2]) : DEFAULT_BOT_COUNT;
    console.log(`🤖 Initializing ${botCount} bots...`);

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
            await PaymentService.deposit(user._id.toString(), INITIAL_BALANCE);
            console.log(`   + Created ${name}`);
        } else {
            if (user.balance < 1000) {
                await PaymentService.deposit(user._id.toString(), INITIAL_BALANCE);
                console.log(`   $ Refilled ${name}`);
            }
        }
        bots.push(user);
    }
    console.log(`✅ ${bots.length} active bots ready.`);

    let running = true;
    process.on('SIGINT', () => { running = false; console.log('\nStopping swarm...'); });

    while (running) {
        try {
            const activeAuctions = await Auction.find({ status: AuctionStatus.ACTIVE });

            if (activeAuctions.length === 0) {
                console.log('zzz No active auctions. Sleeping 5s...');
                await sleep(5000);
                continue;
            }

            const activeBots = [...bots].sort(() => Math.random() - 0.5);

            for (const bot of activeBots) {
                if (!running) break;

                const fresherBot = await User.findById(bot._id);
                if (!fresherBot) continue;

                const auction = activeAuctions[Math.floor(Math.random() * activeAuctions.length)];

                const currentRound = auction.rounds[auction.currentRoundIndex];
                if (!currentRound || currentRound.isFinalized) continue;

                const freshAuction = await Auction.findById(auction._id);
                if (!freshAuction) continue;

                const winnersCount = freshAuction.rounds[freshAuction.currentRoundIndex].winnersCount;

                const topBids = await Bid.find({
                    auctionId: freshAuction._id,
                    status: BidStatus.ACTIVE,
                    roundIndex: freshAuction.currentRoundIndex
                })
                    .sort({ amount: -1 })
                    .limit(winnersCount + 5)
                    .populate('userId');

                const myKey = fresherBot._id.toString();
                const myRank = topBids.findIndex((b: any) => {
                    const u = b.userId;
                    const uid = u._id ? u._id.toString() : u.toString();
                    return uid === myKey;
                });

                const amIWinning = myRank !== -1 && myRank < winnersCount;

                // Решение «ставить или нет» зависит от текущего места бота в топе.
                let shouldBid = false;
                if (myRank === -1) {
                    shouldBid = Math.random() < 0.5;
                } else if (amIWinning) {
                    shouldBid = Math.random() < AGGRESSION_FACTOR;
                } else {
                    shouldBid = Math.random() < 0.8;
                }

                if (shouldBid) {
                    const minBid = freshAuction.rounds[freshAuction.currentRoundIndex].minBid;

                    let baseTarget = minBid;
                    if (topBids.length > 0) {
                        const topAmount = (topBids[0] as any).amount;
                        baseTarget = Math.max(minBid, topAmount);
                    }

                    const increment = Math.floor(Math.random() * 500) + 10;
                    const bidAmount = baseTarget + increment;

                    if (fresherBot.balance + fresherBot.lockedBalance >= bidAmount) {
                        try {
                            const result = await BidService.placeBid(fresherBot._id.toString(), freshAuction._id.toString(), bidAmount);
                            console.log(`   📝 ${fresherBot.username} bid ${bidAmount} on "${freshAuction.title}" (Round ${freshAuction.currentRoundIndex + 1})`);
                            await sleep(Math.random() * 500 + 200);
                        } catch (e: any) {
                            if (e.message.includes("funds")) {
                                console.log(`   ⚠️ ${fresherBot.username} out of funds for bid ${bidAmount}.`);
                            }
                        }
                    } else {
                        console.log(`   📉 ${fresherBot.username} too poor to compete (Bal: ${fresherBot.balance}).`);
                    }
                }
            }

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
