import mongoose, { Schema, Document } from 'mongoose';

export enum BidStatus {
    ACTIVE = 'ACTIVE',
    WINNER = 'WINNER',
    OUTBID = 'OUTBID',
    LOST = 'LOST'
}

export interface IBid extends Document {
    auctionId: mongoose.Types.ObjectId;
    userId: mongoose.Types.ObjectId;
    amount: number;
    roundIndex: number;
    status: BidStatus;
    snapshotTitle?: string;
    transferredFromUserId?: mongoose.Types.ObjectId;
    transferredAt?: Date;
    createdAt: Date;
}

const BidSchema: Schema = new Schema({
    auctionId: { type: Schema.Types.ObjectId, ref: 'Auction', required: true, index: true },
    userId: { type: Schema.Types.ObjectId, ref: 'User', required: true, index: true },
    amount: { type: Number, required: true },
    roundIndex: { type: Number, required: true },
    status: { type: String, enum: Object.values(BidStatus), default: BidStatus.ACTIVE },
    snapshotTitle: { type: String },
    transferredFromUserId: { type: Schema.Types.ObjectId, ref: 'User' },
    transferredAt: { type: Date }
}, { timestamps: true });

BidSchema.index({ auctionId: 1, roundIndex: 1, amount: -1 });

export const Bid = mongoose.model<IBid>('Bid', BidSchema);
