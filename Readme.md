# Spine API

A production-grade, event-driven REST API for tracking physical book collections, sharing libraries with friends, and discovering new books.

🌐 **Live API:** *(Coming in Sprint 7)*  
💻 **Frontend Repo:** *(Coming in Sprint 7)*

---

## 📌 Current Status

**Phase:** Backend Complete — Ready for Frontend
**Progress:** `[██████████] 100%` (Backend MVP)

### ✅ Done
- Solution architecture created: `Spine.Api`, `Spine.Core`, `Spine.Infrastructure`, `Spine.Tests`, `Spine.Worker`.
- **Database Schema Designed:** Created 9 core entities and configured the `AppDbContext` with relationships and indexes.
- **JWT Authentication:** Register, Login (Email or DisplayName), and Account Management (Update Profile, Email, Password).
- **Email Verification:** Uses Kafka and Resend to send verification links. Users cannot log in until verified.
- **Password Reset:** Event-driven reset flow (API -> Kafka -> Worker -> Email).
- **Integration Tests:** 9 tests passing (Auth, Account Management, JWT Authorization).
- **Open Library Integration:** Fetch book metadata (Title, Author, Cover, Description, Page Count) by ISBN.
- **Global Catalog:** Deduplicates books across users in the database.
- **Library Management:** Users can add books to their personal library, update reading progress, and track status.
- **Reviews System:** Users can leave a 1-5 star public rating and written review for any book.
- **Social Layer (Friendships):** Send, accept, and view friends.
- **Privacy Enforcement:** Friends can only view a user's library if their privacy setting is `FriendsOnly` or `Public`.
- **Activity Feed (Kafka):** When a user finishes a book, an event is published. The Worker writes a feed entry for the user and all their friends.
- **Background Worker:** `Spine.Worker` consumes Kafka topics and handles emails and activity feed fan-out.

### 🚧 In Progress
- Preparing for Sprint 7: React Web Frontend, AI Chatbot, and Mobile App.

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

```text
Spine/
├── Spine.Api/                  # HTTP endpoints, controllers, DTOs, Services
│   ├── Controllers/            # Auth, Account, UserBooks, Books, Reviews, Friendships
│   ├── Models/                 # DTOs
│   ├── Services/               # TokenService, KafkaProducerService, OpenLibraryService
│   ├── Events/                 # Event DTOs (PasswordReset, UserRegistered, BookFinished)
│   ├── Validators/             # FluentValidation rules
│   └── Program.cs
├── Spine.Core/                 # Domain entities
│   └── Entities/
├── Spine.Infrastructure/       # EF Core, DbContext, Migrations
│   ├── Data/
│   └── Migrations/
├── Spine.Worker/               # Background service (Kafka Consumer)
│   ├── Services/               # EmailService (Resend)
│   └── KafkaConsumerWorker.cs
├── Spine.Tests/                # Integration tests
│   └── Integration/
├── docker-compose.yml          # Local Kafka & Kafka-UI setup
└── README.md

## 🚀 Setup

### Prerequisites
- .NET 10 SDK
- A Neon account (free tier)

### 1. Clone & Restore
```bash
git clone https://github.com/ShravanAshok16/Spine.git
cd Spine
dotnet restore

# Initialize user secrets for the API
dotnet user-secrets init --project Spine.Api

# Add your Neon PostgreSQL connection string (convert to key-value format)
dotnet user-secrets set "ConnectionStrings:Default" "Host=ep-xxx.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true" --project Spine.Api

# Add a secure JWT Key (at least 32 characters)
dotnet user-secrets set "Jwt:Key" "thisIsMySuperSecretJwtKeyForSpineApp_ChangeMe_12345" --project Spine.Api

# Apply Database Migrations
dotnet ef database update --project Spine.Infrastructure --startup-project Spine.Api

# Start Kafka
docker compose up -d

# Run Application
## The API
dotnet run --project Spine.Api
## The Background Worker
dotnet run --project Spine.Worker

# Run Automated Tests
dotnet test

---

