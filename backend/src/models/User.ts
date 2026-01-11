import mongoose, { Schema, Document } from 'mongoose';

export interface IUser extends Document {
    username: string;
    balance: number;
    lockedBalance: number; // Funds locked in active bids
    createdAt: Date;
}

const UserSchema: Schema = new Schema({
    username: { type: String, required: true, unique: true },
    balance: { type: Number, required: true, default: 0 },
    lockedBalance: { type: Number, required: true, default: 0 },
}, { timestamps: true });

// Optimistic concurrency control is default in Mongoose via __v

export const User = mongoose.model<IUser>('User', UserSchema);
