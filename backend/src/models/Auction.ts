import mongoose, { Schema, Document } from 'mongoose';

export enum AuctionStatus {
    PENDING = 'PENDING',
    ACTIVE = 'ACTIVE',
    FINISHED = 'FINISHED'
}

export interface IRound {
    index: number;
    duration: number;
    startTime?: Date;
    endTime?: Date;
    winnersCount: number;
    minBid: number;
    isFinalized: boolean;
}

export interface IAuction extends Document {
    title: string;
    status: AuctionStatus;
    rounds: IRound[];
    currentRoundIndex: number;
    totalWinnersNeeded: number;
    createdAt: Date;
}

const RoundSchema = new Schema({
    index: { type: Number, required: true },
    duration: { type: Number, required: true, default: 60000 },
    startTime: { type: Date },
    endTime: { type: Date },
    winnersCount: { type: Number, required: true },
    minBid: { type: Number, required: true, default: 1 },
    isFinalized: { type: Boolean, default: false }
});

const AuctionSchema: Schema = new Schema({
    title: { type: String, required: true },
    status: { type: String, enum: Object.values(AuctionStatus), default: AuctionStatus.PENDING },
    rounds: [RoundSchema],
    currentRoundIndex: { type: Number, default: 0 },
    totalWinnersNeeded: { type: Number, default: 0 },
}, { timestamps: true });

export const Auction = mongoose.model<IAuction>('Auction', AuctionSchema);
