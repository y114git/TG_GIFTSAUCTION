import mongoose from 'mongoose';

export const connectDB = async () => {
    const uri = process.env.MONGO_URI || 'mongodb://127.0.0.1:27017/auction_db';

    let retries = 5;
    while (retries > 0) {
        try {
            await mongoose.connect(uri);
            console.log('MongoDB Connected');
            return;
        } catch (error) {
            console.error(`MongoDB connection error. Retries left: ${retries}`, error);
            retries -= 1;
            await new Promise(res => setTimeout(res, 5000));
        }
    }
    console.error('Could not connect to MongoDB after multiple retries.');
    process.exit(1);
};
