import { User, IUser } from '../models/User';
import { PaymentService } from './PaymentService';

export class AuthService {
    /**
     * Find or create a user by username.
     */
    static async login(username: string): Promise<IUser> {
        let user = await User.findOne({ username });
        if (!user) {
            user = await User.create({ username });
            // Give some initial funds for demo
            // In real app, this would be a separate flow/payment
            await PaymentService.deposit(user.id, 1000);
        }
        return user;
    }
}
