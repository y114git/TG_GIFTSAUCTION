import mongoose from 'mongoose';
import { User, IUser } from '../models/User';
import { Transaction, TransactionType } from '../models/Transaction';

export class PaymentService {

    /**
     * Применяет изменение баланса и записывает операцию в историю транзакций
     */
    static async deposit(userId: string, amount: number, session: mongoose.ClientSession | null = null): Promise<IUser> {
        const localSession = session ? session : await mongoose.startSession();
        if (!session) localSession.startTransaction();

        try {
            const user = await User.findById(userId).session(localSession);
            if (!user) throw new Error('User not found');

            // Баланс меняется только вместе с записью в Transaction, чтобы история совпадала с фактом.
            user.balance += amount;
            await user.save({ session: localSession });

            await Transaction.create([{
                userId,
                amount,
                type: amount < 0 ? TransactionType.WITHDRAWAL : TransactionType.DEPOSIT
            }], { session: localSession });

            if (!session) await localSession.commitTransaction();
            return user;
        } catch (error) {
            if (!session) await localSession.abortTransaction();
            throw error;
        } finally {
            if (!session) localSession.endSession();
        }
    }

    /**
     * Переносит средства из доступного баланса в заблокированный
     */
    static async lockFunds(userId: string, amount: number, referenceId: string, session: mongoose.ClientSession | null = null): Promise<void> {
        const localSession = session ? session : await mongoose.startSession();
        if (!session) localSession.startTransaction();

        try {
            const user = await User.findById(userId).session(localSession);
            if (!user) throw new Error('User not found');

            if (user.balance < amount) {
                throw new Error('Insufficient funds');
            }

            // Заблокированные средства используются для ставок: они больше не доступны для новых действий.
            user.balance -= amount;
            user.lockedBalance += amount;
            await user.save({ session: localSession });

            await Transaction.create([{
                userId,
                amount: -amount,
                type: TransactionType.BID_LOCK,
                referenceId
            }], { session: localSession });

            if (!session) await localSession.commitTransaction();
        } catch (error) {
            if (!session) await localSession.abortTransaction();
            throw error;
        } finally {
            if (!session) localSession.endSession();
        }
    }

    /**
     * Возвращает средства из заблокированного баланса обратно в доступный
     */
    static async unlockFunds(userId: string, amount: number, referenceId: string, session: mongoose.ClientSession | null = null): Promise<void> {
        const localSession = session ? session : await mongoose.startSession();
        if (!session) localSession.startTransaction();

        try {
            const user = await User.findById(userId).session(localSession);
            if (!user) throw new Error('User not found');

            if (user.lockedBalance < amount) {
                throw new Error('Inconsistent locked balance state');
            }

            // Сценарий возврата: ставка проиграла или была отменена.
            user.lockedBalance -= amount;
            user.balance += amount;
            await user.save({ session: localSession });

            await Transaction.create([{
                userId,
                amount: amount,
                type: TransactionType.BID_REFUND,
                referenceId
            }], { session: localSession });

            if (!session) await localSession.commitTransaction();
        } catch (error) {
            if (!session) await localSession.abortTransaction();
            throw error;
        } finally {
            if (!session) localSession.endSession();
        }
    }

    /**
     * Списывает средства из заблокированного баланса при подтверждённой победе
     */
    static async captureFunds(userId: string, amount: number, referenceId: string, session: mongoose.ClientSession | null = null): Promise<void> {
        const localSession = session ? session : await mongoose.startSession();
        if (!session) localSession.startTransaction();

        try {
            const user = await User.findById(userId).session(localSession);
            if (!user) throw new Error('User not found');

            if (user.lockedBalance < amount) {
                console.error(`Capture failed: userId=${userId} locked=${user.lockedBalance} required=${amount} referenceId=${referenceId}`);
                throw new Error(`Insufficient locked funds to capture. Locked: ${user.lockedBalance}, Needed: ${amount}`);
            }

            // Здесь доступный баланс не меняется: средства уже были сняты при lockFunds.
            user.lockedBalance -= amount;
            await user.save({ session: localSession });

            await Transaction.create([{
                userId,
                amount: -amount,
                type: TransactionType.WIN_CAPTURE,
                referenceId
            }], { session: localSession });

            if (!session) await localSession.commitTransaction();
        } catch (error) {
            if (!session) await localSession.abortTransaction();
            throw error;
        } finally {
            if (!session) localSession.endSession();
        }
    }
}
