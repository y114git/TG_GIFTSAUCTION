import mongoose, { Schema, Document } from 'mongoose';

export interface IUser extends Document {
    username: string;
    balance: number;
    lockedBalance: number;
    createdAt: Date;
}

const UserSchema: Schema = new Schema({
    username: { type: String, required: true, unique: true },
    balance: { type: Number, required: true, default: 0 },
    lockedBalance: { type: Number, required: true, default: 0 },
}, { timestamps: true });

export const User = mongoose.model<IUser>('User', UserSchema);
