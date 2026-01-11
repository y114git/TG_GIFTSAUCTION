# Telegram Digital Gifts Auction Clone

> **Contest Submission for Backend Development**
> This project implements the backend mechanics of Telegram Gift Auctions with a focus on financial integrity, concurrency, and product logic.

## 🎯 Mechanics Understanding

Based on the contest description and analysis of Telegram's product, I implemented the following mechanics:

### 1. Multi-Round Auction
- **Logic**: Each auction consists of $R$ rounds.
- **Winners**: In each round, the top $N$ bidders win.
- **Carry-Over**: Users who place a bid but do not win in Round $i$ are automatically carried over to Round $i+1$.
  - *My Implementation*: A user has **one active bid** per auction. If they bid again, they "upgrade" their existing bid. If they lose a round, their bid remains "Active" and counts towards the next round ranking automatically.

### 2. Bidding & Financials
- **One Bid Per User**: A user cannot have multiple active bids in the same auction to prevent accidental double-spending.
- **Upgrades**: Users can increase their bid at any time. The system only locks the difference (`NewAmount - OldAmount`).
- **Funds Locking**: 
  - When bidding, funds are moved from `Balance` to `LockedBalance`.
  - **Safety**: This ensures users cannot withdraw or spend funds locked in active bids.
- **Settlement**:
  - **Win**: Locked funds are "Captured" (removed from LockedBalance).
  - **Loss**: Locked funds may be refunded if the user drops out or the auction ends without them winning.

### 3. Anti-Sniping
- **Rule**: If a bid is placed in the last 30 seconds of a round, the round is extended by 60 seconds.
- **Implementation**: Handled atomically in `BidService`.

---

## 🏗 Architecture & Stack

| Component | Technology | Reasoning |
|-----------|------------|-----------|
| **Runtime** | **Node.js + Fastify** | High-performance async I/O, lower overhead than Express. |
| **Language** | **TypeScript** | Type safety for financial calculations and business logic. |
| **Database** | **MongoDB (Replica Set)** | Validated choice for document storage. **Transactions** are strictly used for all money-related operations. |
| **Concurrency** | **Optimistic Locking** | Used for Auction state updates (e.g. extending rounds) to prevent race conditions. |

### Key Design Decisions
1.  **Monolith Service**: For this scope, a single service (modularized into `AuctionEngine`, `BidService`, `PaymentService`) reduces complexity while maintaining clean boundaries.
2.  **Pull-based Engine**: The `AuctionEngine` runs a short-interval loop to check for round endings. In a production massive-scale system, this would be replaced by a priority queue (e.g. BullMQ/Redis), but for the contest requirements, this is robust and simpler to deploy.
3.  **Idempotency**: All financial transactions utilize a `referenceId` (e.g., `BID_LOCK:auctionId_userId`) to prevent double-charging.

---

## ✅ Verification & Load Testing

The repository includes a dedicated load-testing script to prove the system handles concurrency and maintains financial accuracy.

### What it tests:
1.  **Concurrency**: Simulates 50+ bots placing hundreds of bids simultaneously.
2.  **Financial Integrity**: Calculates `Total System Deposit - (Sum of User Balances + Locked Funds + Spent Funds)`. The result must be **exactly 0**.
3.  **Game Loop**: Verifies rounds transition correctly and winners are selected.

### running the Test:
```bash
# 1. Ensure DB is running
docker-compose up -d mongo mongo-init

# 2. Run the simulation
cd backend
npx ts-node --transpile-only src/scripts/simulate_auction.ts
```

---

## 🚀 How to Run

### Option A: Docker Compose (Recommended)
This requires no local Node/Mongo installation.

1.  **Start the System**:
    ```bash
    docker-compose up --build
    ```
2.  **Access the App**:
    - Frontend: [http://localhost:5173](http://localhost:5173)
    - API: [http://localhost:3000](http://localhost:3000)

### Option B: Local Dev
1.  **Backend**:
    ```bash
    cd backend
    npm install
    npm run dev
    ```
2.  **Frontend**:
    ```bash
    cd frontend
    npm install
    npm run dev
    ```

---

## 📂 Project Structure
- `backend/src/models`: Mongoose schemas (Critical: `Transaction` model for audit).
- `backend/src/services`: Core logic (`AuctionEngine`, `PaymentService`, `BidService`).
- `backend/src/scripts`: Verification scripts.
- `frontend`: React demo UI.
