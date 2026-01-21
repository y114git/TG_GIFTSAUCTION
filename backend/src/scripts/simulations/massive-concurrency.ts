import axios from 'axios';
import { connectDB } from '../../db';
import { User } from '../../models/User';
import { Auction, AuctionStatus } from '../../models/Auction';
import dotenv from 'dotenv';
import mongoose from 'mongoose';

dotenv.config();

const API_URL = 'http://localhost:3000';
const CONCURRENT_USERS = 500;

async function massiveConcurrency() {
    await connectDB();
    console.log(`Запуск стресс-теста на конкуренцию: ${CONCURRENT_USERS} пользователей...`);

    // Пользователи создаются пачками, чтобы не упереться в лимиты по времени/памяти.
    console.log(' Создание пользователей...');
    const userIds: string[] = [];
    const BATCH_SIZE = 100;

    for (let i = 0; i < CONCURRENT_USERS; i += BATCH_SIZE) {
        const batch = [];
        for (let j = 0; j < BATCH_SIZE && (i + j) < CONCURRENT_USERS; j++) {
            batch.push({ username: `stress_user_${i + j}_${Date.now()}`, balance: 10000, lockedBalance: 0 });
        }
        const created = await User.insertMany(batch);
        userIds.push(...created.map(u => u.id));
    }
    console.log(` Создано пользователей: ${userIds.length}.`);

    const auction = await Auction.create({
        title: 'Stress Test Item',
        status: AuctionStatus.ACTIVE,
        rounds: [{
            index: 0,
            duration: 1000 * 60 * 5,
            startTime: new Date(),
            endTime: new Date(Date.now() + 1000 * 60 * 5),
            winnersCount: 5,
            minBid: 1,
            isFinalized: false
        }],
        currentRoundIndex: 0
    });
    console.log(` Создан аукцион ${auction.id}`);

    console.log(' Отправка ставок...');

    let successCount = 0;
    let failCount = 0;
    const start = Date.now();

    const bidPromises = userIds.map(async (userId, idx) => {
        try {
            await axios.post(`${API_URL}/auctions/${auction.id}/bid`, {
                amount: 10 + (idx % 100)
            }, {
                headers: { 'x-user-id': userId }
            });
            successCount++;
        } catch (e: any) {
            failCount++;
        }
    });

    await Promise.all(bidPromises);
    const duration = Date.now() - start;

    console.log('--- Результаты ---');
    console.log(`Duration: ${duration}ms`);
    console.log(`Успешных ставок: ${successCount}`);
    console.log(`Ошибок: ${failCount}`);
    console.log(`RPS: ${CONCURRENT_USERS / (duration / 1000)}`);

    const dbAuction = await Auction.findById(auction.id);
    console.log('Состояние аукциона:', dbAuction?.status);

    process.exit(0);
}

massiveConcurrency();
