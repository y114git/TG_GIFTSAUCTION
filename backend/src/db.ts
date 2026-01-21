import mongoose from 'mongoose';

export const connectDB = async () => {
    // Приоритет: явный MONGO_URI. Для docker-compose поддерживается MONGO_URL.
    let uri = process.env.MONGO_URI || 'mongodb://127.0.0.1:27017/auction_db';
    if (!process.env.MONGO_URI && process.env.MONGO_URL) {
        uri = process.env.MONGO_URL + '/auction_db?authSource=admin';
    }

    // Подключение к Mongo может быть не готово при старте контейнеров, поэтому есть несколько повторов.
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
