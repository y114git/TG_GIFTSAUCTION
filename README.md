# Telegram Gifts Auction

Конкурсная работа по Backend-разработке. Реализация механики аукционов Telegram Gift с обработкой конкурентных запросов и финансовой целостностью.

---

## Демо

Видео: [[КЛИК](https://youtu.be/grpvQfVUXA8)]

Сайт: [[КЛИК](https://tggiftsauction-production.up.railway.app)]

---

## Оглавление

1. [Механика аукционов](#механика-аукционов)
2. [Архитектура](#архитектура)
3. [Выбор технологий](#выбор-технологий)
4. [Запуск](#запуск)
5. [API](#api)
6. [Тестирование](#тестирование)
7. [Демо](#демо)

---

## Механика аукционов

### Многораундовая структура

Аукцион состоит из N раундов. В каждом раунде фиксированное количество победителей (например, 10). Побеждают участники с наибольшими ставками. Проигравшие автоматически переносятся в следующий раунд — повторная ставка не требуется.

```
Аукцион: "Premium Gift Box" — 10 штук, 3 раунда

Раунд 1: топ-10 по ставкам получают подарки
Раунд 2: топ-10 из оставшихся получают подарки
Раунд 3: топ-10 из оставшихся получают подарки
```

### Блокировка средств

При ставке средства переходят из свободного баланса в заблокированный. При повышении ставки блокируется только разница.

```
Баланс: 1000, Заблокировано: 0

Ставка 100 -> Баланс: 900, Заблокировано: 100
Повышение до 150 -> Баланс: 850, Заблокировано: 150 (заблокировано +50, не +150)

Победа -> Баланс: 850, Заблокировано: 0, Инвентарь: +1 подарок
```

### Anti-Sniping

Ставка в последние 30 секунд раунда продлевает таймер на 30 секунд. Это предотвращает тактику "снайпинга" — перебивания ставок в последний момент.

### Перенос ставок

Ставки проигравших участников остаются активными в следующем раунде. Средства остаются заблокированными до победы или завершения аукциона.

---

## Архитектура

```
Frontend (React + Vite)
    |
    | HTTP REST API, polling 2 сек
    v
Backend (Node.js + Fastify)
    |
    +-- AuthService      — авторизация
    +-- BidService       — ставки, anti-sniping, валидация
    +-- PaymentService   — балансы, транзакции
    +-- AuctionEngine    — фоновый процесс завершения раундов
    |
    v
MongoDB (Replica Set)
    +-- Users        — баланс, lockedBalance
    +-- Auctions     — раунды, статус, items
    +-- Bids         — ставки с версионированием
    +-- Transactions — журнал операций
```

### Backend

```
backend/src/
├── models/
│   ├── User.ts          — пользователь, баланс
│   ├── Auction.ts       — аукцион, раунды
│   ├── Bid.ts           — ставка
│   └── Transaction.ts   — запись в журнале
├── services/
│   ├── AuctionEngine.ts — завершение раундов, распределение выигрышей
│   ├── BidService.ts    — размещение ставок, блокировка средств
│   ├── PaymentService.ts— депозит, вывод, списание
│   └── AuthService.ts   — регистрация и вход
├── routes/              — REST эндпоинты
└── scripts/             — bot_swarm, simulate_auction
```

### Frontend

```
frontend/src/
├── components/   — React компоненты
├── App.tsx       — состояние приложения, вкладки
├── api.ts        — HTTP клиент
└── main.tsx      — точка входа
```

---

## Выбор технологий

### Fastify вместо Express

Fastify в 2-3 раза быстрее Express за счёт схема-ориентированной сериализации. Встроенная валидация через JSON Schema. TypeScript из коробки.

Express медленнее и требует дополнительных пакетов для валидации. NestJS избыточен для проекта такого масштаба.

### MongoDB с Replica Set

Аукционы имеют вложенные структуры (раунды, items) — документная модель подходит лучше реляционной. Replica Set необходим для транзакций.

Без транзакций возможна ситуация: средства списаны, но ставка не создана (сбой между операциями). С транзакциями — атомарность: либо обе операции выполнены, либо ни одна.

Optimistic Concurrency Control через поле `__v` в Mongoose предотвращает race conditions при одновременных ставках.

PostgreSQL подошёл бы, но потребовал бы больше кода для работы с вложенными структурами.

### Polling вместо WebSocket

Для демо-версии polling каждые 2 секунды достаточен. Упрощает деплой — не нужны sticky sessions. В production заменяется на Socket.IO без изменения архитектуры.

### Docker Compose

Mongo Replica Set требует специфичной инициализации. Docker Compose гарантирует воспроизводимость окружения и правильный порядок запуска сервисов.

---

## Запуск

### Требования

Docker и Docker Compose. Или: Node.js 22+, MongoDB 6.0+ с Replica Set.

### Стек

**Backend:**

- Node.js 22
- Fastify 4.26
- Mongoose 9.1
- TypeScript 5.9

**Frontend:**

- React 19.2
- Vite 7.2
- TypeScript 5.9

**Инфраструктура:**

- MongoDB 6.0 (Replica Set)
- Docker + Docker Compose

### Быстрый запуск (Docker Compose)

```bash
git clone https://github.com/y114git/TG_GIFTSAUCTION.git
cd TG_GIFTSAUCTION
docker-compose up --build
```

Frontend: <http://localhost:5173>  
Backend: <http://localhost:3000>  
MongoDB: localhost:27017

### Локальный запуск

Запуск MongoDB:

```bash
docker run -d --name mongo-rs -p 27017:27017 mongo:6.0 --replSet rs0
docker exec -it mongo-rs mongosh --eval "rs.initiate()"
```

Backend:

```bash
cd backend
npm install
npm run dev
```

Frontend:

```bash
cd frontend
npm install
npm run dev
```

### Переменные окружения

| Переменная | Значение по умолчанию | Назначение |
|------------|----------------------|------------|
| MONGO_URI | mongodb://localhost:27017/auction_db | URI MongoDB |
| PORT | 3000 | Порт backend |
| VITE_API_URL | <http://localhost:3000> | URL backend для frontend |

---

## API

### Авторизация

```
POST /api/auth/login
{ "username": "player123" }

Ответ: { "user": { "id": "...", "username": "player123", "balance": 0 } }
```

### Аукционы

```
GET /api/auctions              — список активных
GET /api/auctions/:id          — конкретный аукцион

POST /api/auctions
{
  "title": "Premium Gift",
  "description": "Описание",
  "itemsPerRound": 5,
  "totalRounds": 3,
  "roundDuration": 300,
  "minBid": 10
}
```

### Ставки

```
POST /api/bids
{
  "auctionId": "...",
  "userId": "...",
  "amount": 100
}
```

### Платежи

```
POST /api/payments/deposit
{ "userId": "...", "amount": 1000 }

POST /api/payments/withdraw
{ "userId": "...", "amount": 500 }
```

### Транзакции

```
GET /api/transactions/:userId

Ответ: [
  { "type": "deposit", "amount": 1000, "createdAt": "..." },
  { "type": "bid_lock", "amount": -100, "metadata": { "auctionId": "..." } },
  { "type": "win", "amount": -100, "metadata": { "item": "Premium Gift" } }
]
```

---

## Тестирование

### Нагрузочный тест

50 ботов с реалистичным поведением:

```bash
cd backend
npx ts-node src/scripts/bot_swarm.ts 50
```

Боты проверяют баланс, делают ставки с задержками, повышают ставки случайным образом.

### Симуляция аукциона

Headless-тест без UI:

```bash
cd backend
npx ts-node --transpile-only src/scripts/simulate_auction.ts
```

Проверяет:

- Финансовый баланс: сумма депозитов равна сумме балансов + заблокировано + списано
- Отсутствие двойных списаний при конкурентных запросах
- Запись всех операций в журнал транзакций

---

## Структура репозитория

```
TG_GIFTSAUCTION/
├── backend/
│   ├── src/
│   │   ├── models/
│   │   ├── services/
│   │   ├── routes/
│   │   └── scripts/
│   ├── Dockerfile
│   └── package.json
├── frontend/
│   ├── src/
│   ├── Dockerfile
│   └── package.json
├── docker-compose.yml
└── README.md
```

---

Автор: [Y114]
