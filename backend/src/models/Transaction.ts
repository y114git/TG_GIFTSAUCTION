import mongoose, { Schema, Document } from 'mongoose';

export enum TransactionType {
    DEPOSIT = 'DEPOSIT',
    BID_LOCK = 'BID_LOCK',
    BID_REFUND = 'BID_REFUND',
    WIN_CAPTURE = 'WIN_CAPTURE',
    WITHDRAWAL = 'WITHDRAWAL'
}

export interface ITransaction extends Document {
    userId: mongoose.Types.ObjectId;
    amount: number;
    type: TransactionType;
    referenceId?: string;
    createdAt: Date;
}

const TransactionSchema: Schema = new Schema({
    userId: { type: Schema.Types.ObjectId, ref: 'User', required: true },
    amount: { type: Number, required: true },
    type: { type: String, enum: Object.values(TransactionType), required: true },
    referenceId: { type: String },
}, { timestamps: true });

export const Transaction = mongoose.model<ITransaction>('Transaction', TransactionSchema);
