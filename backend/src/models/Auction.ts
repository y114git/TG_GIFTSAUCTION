import mongoose, { Schema, Document } from 'mongoose';

export enum AuctionStatus {
    PENDING = 'PENDING',
    ACTIVE = 'ACTIVE',
    FINISHED = 'FINISHED'
}

export interface IRound {
    index: number;
    duration: number; // in ms
    startTime?: Date;
    endTime?: Date;
    winnersCount: number; // How many winners in this round
    minBid: number;
    isFinalized: boolean; // True if round logic has processed
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
}, { timestamps: true });

export const Auction = mongoose.model<IAuction>('Auction', AuctionSchema);
