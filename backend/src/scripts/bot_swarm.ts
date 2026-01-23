
import { Auction, AuctionStatus } from '../models/Auction';
import { User, IUser } from '../models/User';
import { Bid, BidStatus } from '../models/Bid';
import { BidService } from '../services/BidService';
import { PaymentService } from '../services/PaymentService';
import dotenv from 'dotenv';
import { connectDB } from '../db';

dotenv.config();

process.env.MONGO_URI = process.env.MONGO_URI || 'mongodb://127.0.0.1:27017/auction_db?directConnection=true';

const NAMES = [
    "Alex", "Sam", "Jordan", "Taylor", "Casey", "Morgan", "Riley", "Quinn", "Avery", "Peyton",
    "Elon", "Jeff", "Bill", "Mark", "Satya", "Sundar", "Tim", "Jensen", "Sam A.", "Vitalik"
];

const DEFAULT_BOT_COUNT = 50;
const INITIAL_BALANCE = 500000;

const sleep = (ms: number) => new Promise(r => setTimeout(r, ms));

interface RPSMetrics {
    totalRequests: number;
    successfulRequests: number;
    failedRequests: number;
    startTime: number;
    lastSecondRequests: number;
    lastSecondTimestamp: number;
    currentRPS: number;
    maxRPS: number;
    responseTimes: number[];
    activeBots: number;
    sleepingBots: number;
}

const metrics: RPSMetrics = {
    totalRequests: 0,
    successfulRequests: 0,
    failedRequests: 0,
    startTime: Date.now(),
    lastSecondRequests: 0,
    lastSecondTimestamp: Date.now(),
    currentRPS: 0,
    maxRPS: 0,
    responseTimes: [],
    activeBots: 0,
    sleepingBots: 0
};

function updateRPSMetrics() {
    const now = Date.now();
    const timeSinceLastSecond = now - metrics.lastSecondTimestamp;

    if (timeSinceLastSecond >= 1000) {
        metrics.currentRPS = Math.round((metrics.lastSecondRequests / timeSinceLastSecond) * 1000);
        if (metrics.currentRPS > metrics.maxRPS) {
            metrics.maxRPS = metrics.currentRPS;
        }
        metrics.lastSecondRequests = 0;
        metrics.lastSecondTimestamp = now;
    }
}

function recordRequest(success: boolean, responseTime: number) {
    metrics.totalRequests++;
    metrics.lastSecondRequests++;
    
    if (success) {
        metrics.successfulRequests++;
    } else {
        metrics.failedRequests++;
    }
    
    metrics.responseTimes.push(responseTime);
    if (metrics.responseTimes.length > 1000) {
        metrics.responseTimes.shift();
    }
    
    updateRPSMetrics();
}

function getAverageResponseTime(): number {
    if (metrics.responseTimes.length === 0) return 0;
    const sum = metrics.responseTimes.reduce((a, b) => a + b, 0);
    return Math.round(sum / metrics.responseTimes.length);
}

function displayMetrics() {
    const elapsedSeconds = Math.floor((Date.now() - metrics.startTime) / 1000);
    const avgRPS = elapsedSeconds > 0 ? Math.round(metrics.totalRequests / elapsedSeconds) : 0;
    const avgResponseTime = getAverageResponseTime();

    process.stdout.write('\x1b[2J\x1b[H');
    
    const output = [
        `🚀 RPS LOAD TEST | Current: ${metrics.currentRPS} | Max: ${metrics.maxRPS} | Avg: ${avgRPS} | Elapsed: ${elapsedSeconds}s`,
        `🤖 Bots Status: Active ${metrics.activeBots} | Sleeping ${metrics.sleepingBots} | Avg Response: ${avgResponseTime}ms`,
        'Press Ctrl+C to stop\n'
    ];
    
    process.stdout.write(output.join('\n'));
}

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
    console.log('╔════════════════════════════════════════════════════════════════╗');
    console.log('║              🚀 RPS LOAD TESTING TOOL - STARTING              ║');
    console.log('╚════════════════════════════════════════════════════════════════╝\n');

    await connectDB();
    console.log('✅ Connected to MongoDB');

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
        } else {
            if (user.balance < 1000) {
                await PaymentService.deposit(user._id.toString(), INITIAL_BALANCE);
            }
        }
        bots.push(user);
    }
    console.log(`✅ ${bots.length} active bots ready.`);

    let running = true;
    process.on('SIGINT', () => { 
        running = false; 
        console.log('\n\n🛑 Stopping test...');
        displayMetrics();
        console.log('\n✅ Test completed. Final results displayed above.');
        process.exit(0);
    });

    const cliConcurrency = process.argv[3] ? parseInt(process.argv[3]) : null;
    const concurrency = cliConcurrency && !Number.isNaN(cliConcurrency) ? cliConcurrency : botCount;
    console.log(`⚙️ Concurrency Level: ${concurrency}`);
    console.log('\n🔥 Starting aggressive RPS test...\n');
    
    await sleep(2000);
    metrics.startTime = Date.now();
    metrics.lastSecondTimestamp = Date.now();

    let displayInterval = setInterval(() => {
        if (running) displayMetrics();
    }, 500);

    while (running) {
        try {
            const activeAuctions = await Auction.find({ status: AuctionStatus.ACTIVE });

            if (activeAuctions.length === 0) {
                await sleep(2000);
                continue;
            }

            metrics.activeBots = Math.min(concurrency, bots.length);
            metrics.sleepingBots = Math.max(0, bots.length - metrics.activeBots);

            await runWithConcurrency(bots, concurrency, async (bot) => {
                if (!running) return;

                const requestStart = Date.now();
                
                try {
                    const auction = activeAuctions[Math.floor(Math.random() * activeAuctions.length)];
                    const freshAuction = await Auction.findById(auction._id);
                    
                    if (!freshAuction) {
                        recordRequest(false, Date.now() - requestStart);
                        return;
                    }

                    const currentRound = freshAuction.rounds[freshAuction.currentRoundIndex];
                    if (!currentRound || currentRound.isFinalized) {
                        recordRequest(false, Date.now() - requestStart);
                        return;
                    }

                    const minBid = currentRound.minBid;
                    const bidAmount = minBid + Math.floor(Math.random() * 100) + 1;

                    const fresherBot = await User.findById(bot._id);
                    if (!fresherBot || fresherBot.balance + fresherBot.lockedBalance < bidAmount) {
                        recordRequest(false, Date.now() - requestStart);
                        return;
                    }

                    await BidService.placeBid(fresherBot._id.toString(), freshAuction._id.toString(), bidAmount);
                    recordRequest(true, Date.now() - requestStart);
                    
                } catch (e: any) {
                    recordRequest(false, Date.now() - requestStart);
                }
            });

            await sleep(1);

        } catch (e: any) {
            recordRequest(false, 0);
            await sleep(100);
        }
    }

    clearInterval(displayInterval);
}

runSwarm();
