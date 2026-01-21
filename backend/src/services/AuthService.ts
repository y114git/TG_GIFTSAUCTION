import { User, IUser } from '../models/User';
import { PaymentService } from './PaymentService';

export class AuthService {
    static async login(username: string): Promise<IUser> {
        // Логин по имени: если пользователя нет, он создаётся и получает стартовый баланс.
        let user = await User.findOne({ username });
        if (!user) {
            user = await User.create({ username });
            await PaymentService.deposit(user.id, 1000);
        }
        return user;
    }
}
