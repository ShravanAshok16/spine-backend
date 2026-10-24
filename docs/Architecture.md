# Spine API — Architecture

> Version: 1.0
> Last Updated: 2026-10-24

## 1. System Context
┌────────────────┐ ┌──────────────────────┐
│ Web Client │───────►│ Spine API │
│ (React 19) │ │ (ASP.NET Core 10) │
└────────────────┘ └──────────┬───────────┘
│
┌────────────────┐ │ EF Core
│ Mobile Client │───────► ▼
│ (React Native) │ ┌──────────────────────┐
└────────────────┘ │ Neon (PostgreSQL) │
└──────────────────────┘
│
┌──────────┴───────────┐
│ Kafka Broker │
└──────────┬───────────┘
│
▼
┌──────────────────────┐
│ Spine.Worker │
│ (Email Consumer) │
└──────────────────────┘


The system is split into an API layer and a Background Worker layer to handle asynchronous, event-driven tasks (like email delivery).

## 2. Application Layers
┌──────────────────────────────────────────────────┐
│ Spine.Api │
│ - Controllers (HTTP entry points) │
│ - DTOs (Request/Response shapes) │
│ - Services (TokenService, KafkaProducerService) │
│ - Program.cs (Composition root) │
└────────────────┬─────────────────────────────────┘
│
┌────────────────┴─────────────────────────────────┐
│ Spine.Worker │
│ - BackgroundService │
│ - Kafka Consumer │
│ - Email Dispatcher │
└────────────────┬─────────────────────────────────┘
│
┌────────────────┴─────────────────────────────────┐
│ Spine.Infrastructure │
│ - AppDbContext (EF Core) │
│ - Migrations │
└────────────────┬─────────────────────────────────┘
│ depends on
▼
┌──────────────────────────────────────────────────┐
│ Spine.Core │
│ - Entities: User, Book, UserBook, Friendship... │
└──────────────────────────────────────────────────┘

## 3. Data Model (Global Catalog Pattern)

User (1) ─────< (many) UserBook (many) >───── (1) Book (1) ─────< (many) Series
│
├─────< (many) Review
├─────< (many) Friendship (Requester/Receiver)
└─────< (many) Activity


**Tables:**
- `Users`: `Id`, `Email`, `PasswordHash`, `DisplayName`, `PrivacySetting`
- `Books` (Global): `Id`, `Isbn`, `Title`, `Author`, `SeriesId`
- `UserBooks` (Personal): `Id`, `UserId`, `BookId`, `Status`, `Rating`
- `Tags` & `UserBookTags`: Many-to-Many custom shelves.
- `Friendships`: Self-referencing table (RequesterId, ReceiverId, Status).

## 4. Event-Driven Architecture (Kafka)

**Topic:** `password-reset-events`

1. User calls `POST /api/Account/forgot-password`.
2. API generates a token, saves it to Postgres, and drops a `PasswordResetRequested` JSON message into Kafka.
3. API returns `200 OK` immediately.
4. `Spine.Worker` consumes the message from Kafka.
5. Worker sends the email via Resend/SendGrid.
6. Worker commits the Kafka offset.

*Why?* If the email provider is down, the API stays fast, and the message stays safe in Kafka until the provider recovers.