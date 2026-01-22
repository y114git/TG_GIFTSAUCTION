
import { Auction, AuctionStatus } from '../models/Auction';
import { User, IUser } from '../models/User';
import { Bid, BidStatus } from '../models/Bid';
import { BidService } from '../services/BidService';
import { PaymentService } from '../services/PaymentService';
import dotenv from 'dotenv';
import { connectDB } from '../db';

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

const clamp01 = (x: number) => Math.max(0, Math.min(1, x));

const sleep = (ms: number) => new Promise(r => setTimeout(r, ms));

async function runWithConcurrency<T>(items: T[], concurrency: number, fn: (item: T) => Promise<void>) {
    const workerCount = Math.max(1, Math.min(concurrency, items.length));
    let idx = 0;

    const worker = async () => {
        while (true) {
            const myIdx = idx++;
            if (myIdx >= items.length) return;
            await fn(items[myIdx]);
        }
    };

    await Promise.all(Array.from({ length: workerCount }, () => worker()));
}

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

    const cliConcurrency = process.argv[3] ? parseInt(process.argv[3]) : null;
    const concurrency = cliConcurrency && !Number.isNaN(cliConcurrency) ? cliConcurrency : botCount;
    console.log(`⚙️ Concurrency: ${concurrency}`);

    const nextActionAtByBotId = new Map<string, number>();
    for (const bot of bots) {
        nextActionAtByBotId.set(bot._id.toString(), Date.now() + Math.floor(Math.random() * (MAX_SLEEP - MIN_SLEEP) + MIN_SLEEP));
    }

    while (running) {
        try {
            const activeAuctions = await Auction.find({ status: AuctionStatus.ACTIVE });

            if (activeAuctions.length === 0) {
                console.log('zzz No active auctions. Sleeping 5s...');
                await sleep(5000);
                continue;
            }

            const now = Date.now();
            const readyBots = bots.filter(b => (nextActionAtByBotId.get(b._id.toString()) ?? 0) <= now);

            if (readyBots.length === 0) {
                await sleep(50);
                continue;
            }

            const activeBots = readyBots.sort(() => Math.random() - 0.5);

            await runWithConcurrency(activeBots, concurrency, async (bot) => {
                if (!running) return;

                const botId = bot._id.toString();
                nextActionAtByBotId.set(botId, Date.now() + Math.floor(Math.random() * (MAX_SLEEP - MIN_SLEEP) + MIN_SLEEP));

                const fresherBot = await User.findById(bot._id);
                if (!fresherBot) return;

                const auction = activeAuctions[Math.floor(Math.random() * activeAuctions.length)];
                const freshAuction = await Auction.findById(auction._id);
                if (!freshAuction) return;

                const currentRound = freshAuction.rounds[freshAuction.currentRoundIndex];
                if (!currentRound || currentRound.isFinalized) return;

                const winnersCount = currentRound.winnersCount;

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

                const minBid = currentRound.minBid;
                const topAmount = topBids.length > 0 ? Number((topBids[0] as any).amount) : 0;

                // Чем дальше цена ушла от minBid, тем меньше мотивация ботов продолжать разгон.
                // ratio=1 -> aggression=1, ratio=5 -> ~0.33, ratio=10 -> ~0.18.
                const ratio = topAmount > 0 ? topAmount / Math.max(1, minBid) : 1;
                const aggression = clamp01(1 / (1 + (Math.max(0, ratio - 1) / 2)));

                // Чем меньше времени до конца раунда, тем меньше шанс ставить.
                // Почти полностью отключаем активность в окне анти-снайпинга.
                let timeFactor = 1;
                const timeLeftMs = currentRound.endTime ? (new Date(currentRound.endTime).getTime() - Date.now()) : null;
                if (timeLeftMs !== null) {
                    if (timeLeftMs <= 0) {
                        timeFactor = 0;
                    } else if (timeLeftMs < 15_000) {
                        timeFactor = 0.03;
                    } else if (timeLeftMs < 30_000) {
                        timeFactor = 0.5;
                    }
                }

                // Решение «ставить или нет» зависит от текущего места бота в топе.
                const baseProb = myRank === -1 ? 0.8 : (amIWinning ? 0.4 : 0.9);
                const bidProb = clamp01(baseProb * aggression * timeFactor);
                const shouldBid = Math.random() < bidProb;

                if (!shouldBid) return;

                let baseTarget = minBid;
                if (topBids.length > 0) {
                    baseTarget = Math.max(minBid, topAmount);
                }

                const maxIncrement = Math.max(10, Math.floor(500 * aggression));
                const increment = Math.floor(Math.random() * maxIncrement) + 10;
                const bidAmount = baseTarget + increment;

                if (fresherBot.balance + fresherBot.lockedBalance < bidAmount) {
                    return;
                }

                try {
                    await BidService.placeBid(fresherBot._id.toString(), freshAuction._id.toString(), bidAmount);
                } catch (e: any) {
                    if (e.message.includes("funds")) {
                        return;
                    }
                }
            });

            await sleep(10);

        } catch (e) {
            console.error("Swarm Error:", e);
            await sleep(5000);
        }
    }
}

runSwarm();
