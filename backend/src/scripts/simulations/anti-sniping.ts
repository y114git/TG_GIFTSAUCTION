import axios from 'axios';
import { connectDB } from '../../db';
import { Auction, AuctionStatus } from '../../models/Auction';
import dotenv from 'dotenv';
import mongoose from 'mongoose';

dotenv.config();

const API_URL = 'http://localhost:3000';

async function antiSnipingTest() {
    await connectDB();
    console.log('--- Тест анти-снайпинга ---');

    const now = new Date();
    const END_SEC = 15;
    // Раунд создаётся заранее запущенным, чтобы можно было точно попасть в «окно» анти-снайпинга.
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
    console.log(`Создан аукцион ${auction.id}, окончание через ${END_SEC}s`);

    console.log('Ждём 5 секунд...');
    await new Promise(r => setTimeout(r, 5000));

    const { User } = await import('../../models/User');
    const user = await User.create({ username: `sniper_${Date.now()}`, balance: 1000, lockedBalance: 0 });

    console.log(`Ставка будет отправлена при ${(auction.rounds[0].endTime!.getTime() - Date.now()) / 1000}s до конца...`);

    await axios.post(`${API_URL}/auctions/${auction.id}/bid`, { amount: 50 }, {
        headers: { 'x-user-id': user.id }
    });

    const updatedAuction = await Auction.findById(auction.id);
    const newEndTime = updatedAuction?.rounds[0].endTime;
    console.log('Старый EndTime:', auction.rounds[0].endTime!.toISOString());
    console.log('Новый EndTime:', newEndTime?.toISOString());

    if (newEndTime!.getTime() > auction.rounds[0].endTime!.getTime()) {
        console.log('OK: раунд продлён.');
    } else {
        console.error('ОШИБКА: раунд не продлился.');
        process.exit(1);
    }

    process.exit(0);
}

antiSnipingTest();
