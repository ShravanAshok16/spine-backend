# Spine API

A production-grade, event-driven REST API for tracking physical book collections, sharing libraries with friends, and discovering new books.

🌐 **Live API:** *(Coming in Sprint 6)*  
💻 **Frontend Repo:** *(Coming in Sprint 5)*

---

## 📌 Current Status

**Phase:** Sprint 1 — Core Backend
**Progress:** `[█░░░░░░░░░] 10%` (Sprint 1, Day 1 Complete)

### ✅ Done
- Solution architecture created: `Spine.Api`, `Spine.Core`, `Spine.Infrastructure`, `Spine.Tests`.
- Installed core packages: EF Core, Npgsql, Serilog, JWT, FluentValidation, and Confluent.Kafka.
- **Database Schema Designed:** Created 9 core entities and configured the `AppDbContext`.
- **Relationships Configured:** Set up One-to-Many, Many-to-Many (Tags), and self-referencing graph tables (Friendships).
- **Database Provisioned:** Initial migration generated and applied to Neon (PostgreSQL).

### 🚧 In Progress
- Sprint 1, Day 2: JWT Authentication & Account Management.

### 📝 Next Up
- Implement `TokenService` and `AuthController` (Register/Login).
- Implement `AccountController` (Update Profile, Email, Password).
- Add FluentValidation for Auth DTOs.

---

## 🗄️ Data Model

The database is designed using a **Global Catalog** pattern to prevent duplicating book data across users.

| Table | Purpose | Key Relationships |
|---|---|---|
| `Users` | Accounts & privacy settings | Has many `UserBooks`, `Reviews` |
| `Books` | Global catalog of books | Belongs to `Series` (optional) |
| `Series` | Book series metadata | Has many `Books` |
| `UserBooks` | A user's personal library | Belongs to `User` and `Book` |
| `Tags` | Custom shelves (e.g., "Sci-Fi") | Many-to-Many with `UserBooks` |
| `Reviews` | Public reviews and ratings | Belongs to `User` and `Book` |
| `Friendships` | Social graph connections | Two FKs to `User` (Requester/Receiver) |
| `Activities` | Feed of user actions | Belongs to `User` |
| `PasswordResetTokens` | Secure reset tokens | Belongs to `User` |

---

## 🏗️ Project Structure



---

## 🛠️ Tech Stack

- **.NET 10** (LTS)
- **Entity Framework Core 10** + **Npgsql** (PostgreSQL)
- **Neon** (Serverless PostgreSQL)
- **Serilog** (Structured logging)
- **FluentValidation** (Request validation)
- **BCrypt.Net-Next** (Password hashing)
- **Confluent.Kafka** (Event streaming — Sprint 2)

---

## 🗓️ Daily Log

### 2026-10-19 (Sprint 1, Day 1 — Schema & Foundation)
- Created the `Spine` solution and 4 projects.
- Installed EF Core, JWT, and Kafka packages.
- Created 9 Domain Entities: `User`, `Book`, `Series`, `UserBook`, `Tag`, `Review`, `Friendship`, `Activity`, `PasswordResetToken`.
- Configured `AppDbContext` with complex relationships:
  - Composite indexes for fast dashboard queries (`UserId`, `Status`).
  - Unique index to prevent users from adding the same book twice (`UserId`, `BookId`).
  - **Self-referencing Foreign Keys** for the `Friendships` table (Requester and Receiver).
  - Many-to-Many join table for `UserBookTags`.
- Generated `InitialCreate` migration and applied it to Neon. Verified all 11 tables exist.

---

## 🔐 Security Decisions (Planned)
- **JWT:** Short-lived access tokens.
- **Password Reset:** Secure, single-use hashed tokens sent via Kafka -> Email.
- **Data Privacy:** `PrivateNotes` will be filtered out at the API level when a friend views a library.
- **Ownership:** Users can only modify their own `UserBooks` and `Reviews`.

---

## 📅 Roadmap

- [x] **Sprint 1 (Days 1–4):** Foundation, DB Schema, JWT Auth, Account Management.
- [ ] **Sprint 2 (Days 5–8):** Kafka integration for Password Resets & Email Worker.
- [ ] **Sprint 3 (Days 9–13):** Open Library API, Redis Caching, Library CRUD.
- [ ] **Sprint 4 (Days 14–17):** Book Details (Series, Where to Buy), Reviews API.
- [ ] **Sprint 5 (Days 18–22):** Social Layer (Friendships, Privacy), Kafka Activity Feed.
- [ ] **Sprint 6 (Days 23–28):** React Web Frontend (Dashboard, Book Details).
- [ ] **Sprint 7 (Days 29–35):** Mobile App (Expo Scanner), Image Uploads, Deployment.

---
## 🏗️ Project Structure
```
Spine/
├── Spine.Api/                  # HTTP endpoints, controllers, DTOs, Services
│   ├── Controllers/
│   ├── Models/
│   ├── Services/
│   ├── Validators/
│   ├── Middleware/
│   └── Program.cs
├── Spine.Core/                 # Domain entities (no external dependencies)
│   └── Entities/
│       ├── User.cs
│       ├── Book.cs
│       ├── Series.cs
│       ├── UserBook.cs
│       ├── Tag.cs
│       ├── Review.cs
│       ├── Friendship.cs
│       ├── Activity.cs
│       └── PasswordResetToken.cs
├── Spine.Infrastructure/       # EF Core, DbContext, Migrations
│   ├── Data/
│   │   └── AppDbContext.cs
│   └── Migrations/
└── Spine.Tests/                # Unit & Integration tests

## 🚀 Setup

### Prerequisites
- .NET 10 SDK
- A Neon account (free tier)

### 1. Clone & Restore
```bash
git clone https://github.com/ShravanAshok16/Spine.git
cd Spine
dotnet restore

