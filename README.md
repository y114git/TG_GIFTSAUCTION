# Telegram Digital Gifts Auction Clone

> **Contest Submission for Backend Development**
> This project implements the complete backend and frontend mechanics of Telegram Gift Auctions, focusing on high-concurrency handling, financial integrity, and a premium user experience.

## Key Features

### 1. Advanced Auction Mechanics
- **Multi-Round Auctions**: Auctions progress through $N$ rounds.
- **Carry-Over Logic**: Bids that don't win in Round $i$ are automatically carried over to Round $i+1$, ensuring users don't need to re-bid constantly.
- **Anti-Sniping**: 
  - **Logic**: If a bid is placed in the last **30 seconds** of a round, the round timer extends by **30 seconds**.
  - **Implementation**: Atomic updates in `BidService` using optimistic locking to prevent race conditions during high concurrency.

### 2. Financial Integrity & Transactions
- **Double-Entry Accounting**: Every action (Deposit, Bid, Refund, Win) is recorded as an immutable `Transaction` record.
- **Funds Locking**:
  - Bidding moves funds from `Balance` to `LockedBalance`.
  - **Bid Upgrades**: Users can increase their bid at any time. The system calculates the difference and only locks the additional amount.
- **Auditability**: Users can view their full transaction history with "Green/Red" indicators for inflows/outflows.

### 3. Real-Time Architecture
- **Tech Stack**: Node.js + Fastify (High Performance), TypeScript, MongoDB (Replica Set).
- **Concurrency**: Tested with 50+ concurrent bots. Uses Mongoose concurrency versions (`__v`) to handle simultaneous bids on the same auction.
- **Frontend**: React + Vite application with polling-based real-time updates (simulating WebSocket behavior for this demo scope).

---

## System Architecture

### Backend Structure (`/backend`)
- **`src/services/`**
  - `AuctionEngine.ts`: The heartbeat of the system. Runs a loop to finalize rounds, distribute winnings, and transition auction states.
  - `BidService.ts`: Handles bid placement, validation, fund locking, and anti-sniping logic.
  - `PaymentService.ts`: Manages user balances and atomic transaction creation.
- **`src/models/`**
  - `User.ts`: Stores balance and locked funds.
  - `Auction.ts`: Complex schema storing rounds, current state, and configuration.
  - `Bid.ts`: Individual bid records.
  - `Transaction.ts`: Immutable ledger of all financial movements.
- **`src/routes/`**
  - `transactions.routes.ts`: Exposes user history.
  - `auction.routes.ts`: Auction interaction endpoints.

### Frontend Structure (`/frontend`)
- **React + TypeScript**: Type-safe component development.
- **`App.tsx`**: Main application state manager. Handles:
  - **Tabs**: Active Auctions, Inventory, History, Create.
  - **Real-time Polling**: Updates balance, auction timer, and leaderboard every 2 seconds.
- **UX/UI**: Dark-themed, premium feel aiming to match Telegram's aesthetic.

---

## Business Logic Deep Dive

### The "Upgrade" Bid Strategy
Unlike traditional auctions where every bid is new, our system treats a user's participation in an auction as a single persistent entity.
1.  **First Bid**: User bids 100 stars. 100 stars are locked.
2.  **Upgrade**: User increases bid to 150 stars.
    -   System checks `LockedBalance`.
    -   System locks **only 50 more stars**.
    -   Total locked: 150.
3.  **Lose Round**: If the user does not win (e.g., Round 1 ends and they are rank #11 for 10 spots), their bid remains **Active** for Round 2.
4.  **Win**: If they win, 150 stars are captured (removed from system), and they receive the item in their Inventory.

### Anti-Sniping Protection
To prevent last-second "sniping" which discourages fair price discovery:
- **Trigger**: `time_left < 30s`
- **Action**: `end_time += 30s`
- **Result**: The auction continues until bidding stabilizes.

---

## Verification & Testing

### Quick Load Test (Bot Swarm)
To see the system in action with multiple concurrent users:
```bash
cd backend
# Starts 50 bots that randomly join auctions and place bids
npx ts-node src/scripts/bot_swarm.ts 50
```
**Bot Behavior:**
-   **Realistic Identity**: Each bot is persistent with a unique name (e.g., "Bot_Alex_42").
-   **Smart Bidding**: Bots check if they are winning before bidding, adhere to financial limits, and have "human" delays.
-   **Observation**: Open the frontend and watch the leaderboard update live!

### Integration Testing
The repository includes a simulation script to stress-test the engine mechanics without the frontend.

#### Run the Headless Simulation
```bash
cd backend
npx ts-node --transpile-only src/scripts/simulate_auction.ts
```
**checks:**
1.  **Financial Zero-Sum**: `TotalDeposits - (UserBalances + Locked + Captured)` must equal 0.
2.  **Concurrency Safety**: No double-spending or negative balances using Optimistic Concurrency Control (`__v`).

---

## How to Run

### Prerequisites
- Docker & Docker Compose

### Fast Start
```bash
docker-compose up --build
```
- **Frontend**: http://localhost:5173
- **Backend API**: http://localhost:3000

### Manual Start (Dev Mode)
**Backend:**
```bash
cd backend
npm install
npm run dev
```
**Frontend:**
```bash
cd frontend
npm install
npm run dev
```

---

## Usage

1.  **Login**: Enter any username (auto-registers).
2.  **Funds**:
    -   Click **(+)** to Deposit stars.
    -   Click **(-)** to Withdraw stars (Simulated payout).
3.  **Bid**: Select an active auction and place a bid.
4.  **History**: Click the 📜 icon in the header to see your financial history (Deposits, Withdrawals, Bids, Wins).
5.  **Inventory**: View won items in the "My Inventory" tab.
