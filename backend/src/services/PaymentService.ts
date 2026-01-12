import mongoose from 'mongoose';
import { User, IUser } from '../models/User';
import { Transaction, TransactionType } from '../models/Transaction';

export class PaymentService {

    /**
     * Deposit funds to a user's balance.
     */
    static async deposit(userId: string, amount: number, session: mongoose.ClientSession | null = null): Promise<IUser> {
        // if (amount <= 0) throw new Error('Deposit amount must be positive'); // Removed constraint

        const localSession = session ? session : await mongoose.startSession();
        if (!session) localSession.startTransaction();

        try {
            const user = await User.findById(userId).session(localSession);
            if (!user) throw new Error('User not found');

            // Overdraft check removed as requested
            // if (amount < 0 && (user.balance + amount) < 0) {
            //    throw new Error('Insufficient funds');
            // }

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
     * Locks funds for a bid.
     * Throws error if insufficient balance.
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
     * Unlocks funds (refund) - e.g. when outbid or round ends without win.
     */
    static async unlockFunds(userId: string, amount: number, referenceId: string, session: mongoose.ClientSession | null = null): Promise<void> {
        const localSession = session ? session : await mongoose.startSession();
        if (!session) localSession.startTransaction();

        try {
            const user = await User.findById(userId).session(localSession);
            if (!user) throw new Error('User not found');

            // We assume correct logic elsewhere ensured lockedBalance >= amount.
            // But safety check:
            if (user.lockedBalance < amount) {
                // This is a critical error state usually, but we proceed to fix ledger if possible
                // or throw error? Let's throw for now to catch bugs.
                throw new Error('Inconsistent locked balance state');
            }

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
     * Captures funds (win). Moves from locked to "spent" (disappears from user).
     */
    static async captureFunds(userId: string, amount: number, referenceId: string, session: mongoose.ClientSession | null = null): Promise<void> {
        const localSession = session ? session : await mongoose.startSession();
        if (!session) localSession.startTransaction();

        try {
            const user = await User.findById(userId).session(localSession);
            if (!user) throw new Error('User not found');

            if (user.lockedBalance < amount) {
                console.error(`CAPTURE ERROR: User ${userId} has locked ${user.lockedBalance} but needs ${amount}. RefId: ${referenceId}`);
                // Attempt auto-fix in dev/demo mode? 
                // For now just log.
                throw new Error(`Insufficient locked funds to capture. Locked: ${user.lockedBalance}, Needed: ${amount}`);
            }

            user.lockedBalance -= amount;
            // balance was already deducted during lock.
            // So we just reduce lockedBalance. Effectivly burning the tokens from user's perspective.

            await user.save({ session: localSession });

            await Transaction.create([{
                userId,
                amount: -amount, // It's a spend, but technically the deduction happened at lock. 
                // However, for pure audit, we might want to record the "event". 
                // But the balance change is 0 here (already moved out of available).
                // Let's record it with 0 user-balance-impact or just specific type.
                // But for `Transaction` model usage as a ledger of "Balance", the balance changed at LOCK.
                // So we might log this with 0 amount but specific type to indicate finality?
                // Or we treat "Locked" as part of User Wealth?
                // Decision: "Balance" = Available. "Locked" = Reserved.
                // Lock: Available -> Locked.
                // Capture: Locked -> Burned.
                // So at capture, we record the "use" of funds.
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
