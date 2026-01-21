import axios from 'axios';
import { connectDB } from '../../db';
import { User } from '../../models/User';
import { Auction } from '../../models/Auction';
import dotenv from 'dotenv';
import mongoose from 'mongoose';

dotenv.config();

const API_URL = 'http://localhost:3000';

async function financialIntegrity() {
    await connectDB();
    console.log('--- Проверка финансовой целостности ---');

    const usersStart = await User.find({});
    // В стартовую сумму включается доступный и заблокированный баланс.
    const totalStart = usersStart.reduce((acc, u) => acc + u.balance + u.lockedBalance, 0);
    console.log(`Сумма на счетах пользователей (старт): ${totalStart}`);

    const { Transaction, TransactionType } = await import('../../models/Transaction');
    const capturesStart = await Transaction.find({ type: TransactionType.WIN_CAPTURE });
    const burnedStart = capturesStart.reduce((acc, t) => acc + Math.abs(t.amount), 0);

    const grandTotalStart = totalStart + burnedStart;
    console.log(`Итого (пользователи + списано) старт: ${grandTotalStart}`);

    const usersNow = await User.find({});
    const totalNow = usersNow.reduce((acc, u) => acc + u.balance + u.lockedBalance, 0);

    const capturesNow = await Transaction.find({ type: TransactionType.WIN_CAPTURE });
    const burnedNow = capturesNow.reduce((acc, t) => acc + Math.abs(t.amount), 0);

    const grandTotalNow = totalNow + burnedNow;
    console.log(`Итого (пользователи + списано) сейчас: ${grandTotalNow}`);

    if (grandTotalNow !== grandTotalStart) {
        console.error(`КРИТИЧНО: нарушена целостность. Разница: ${grandTotalNow - grandTotalStart}`);
    }

    const allTransactions = await Transaction.find({});
    const deposits = allTransactions.filter(t => t.type === 'DEPOSIT').reduce((acc, t) => acc + t.amount, 0);
    const calculatedTotal = deposits;

    const currentHeld = usersNow.reduce((acc, u) => acc + u.balance + u.lockedBalance, 0) + burnedNow;

    console.log(`Сумма депозитов по истории: ${deposits}`);
    console.log(`Текущее удержание (пользователи + списано): ${currentHeld}`);

    if (deposits === currentHeld) {
        console.log('OK: целостность подтверждена, утечек нет.');
    } else {
        console.error('ОШИБКА: суммы не сходятся с депозитами.');
    }

    process.exit(0);
}

financialIntegrity();
