import axios from 'axios';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:3000';

export interface Auction {
    _id: string;
    title: string;
    status: string;
    rounds: Round[];
    currentRoundIndex: number;
    createdAt: string;
    topBids?: Bid[];
}

export interface Round {
    index: number;
    startTime: string;
    endTime: string;
    minBid: number;
    winnersCount: number;
    isFinalized: boolean;
}

export interface Bid {
    _id: string;
    amount: number;
    userId: { _id: string; username: string } | string;
    status: string;
}

export interface Transaction {
    _id: string;
    userId: string;
    amount: number;
    type: string;
    referenceId?: string;
    createdAt: string;
}

export const api = {
    login: async (username: string) => {
        const res = await axios.post(`${API_URL}/auth/login`, { username });
        return res.data;
    },

    getMe: async (userId: string) => {
        const res = await axios.get(`${API_URL}/me/balance`, {
            headers: { 'x-user-id': userId }
        });
        return res.data;
    },

    getAuctions: async () => {
        const res = await axios.get(`${API_URL}/auctions`);
        return res.data;
    },

    getAuctionDetails: async (id: string) => {
        const res = await axios.get(`${API_URL}/auctions/${id}`);
        return res.data;
    },

    placeBid: async (auctionId: string, amount: number, userId: string) => {
        const res = await axios.post(`${API_URL}/auctions/${auctionId}/bid`, { amount }, {
            headers: { 'x-user-id': userId }
        });
        return res.data;
    },

    deposit: async (amount: number, userId: string) => {
        const res = await axios.post(`${API_URL}/me/deposit`, { amount }, {
            headers: { 'x-user-id': userId }
        });
        return res.data;
    },

    getTransactions: async (userId: string) => {
        const res = await axios.get(`${API_URL}/me/transactions`, {
            headers: { 'x-user-id': userId }
        });
        return res.data;
    },

    getInventory: async (userId: string) => {
        const res = await axios.get(`${API_URL}/me/inventory`, {
            headers: { 'x-user-id': userId }
        });
        return res.data;
    },

    getMyBids: async (userId: string): Promise<Record<string, number>> => {
        const res = await axios.get(`${API_URL}/me/bids`, {
            headers: { 'x-user-id': userId }
        });
        return res.data;
    },

    createAuction: async (data: any, userId: string) => {
        
        const res = await axios.post(`${API_URL}/admin/auctions`, data, {
            headers: { 'x-user-id': userId }
        });
        return res.data;
    },

    transferGift: async (bidId: string, recipientUsername: string, userId: string) => {
        const res = await axios.post(`${API_URL}/me/gift/transfer`, { bidId, recipientUsername }, {
            headers: { 'x-user-id': userId }
        });
        return res.data;
    }
};
