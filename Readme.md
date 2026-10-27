# Spine API

A production-grade, event-driven REST API for tracking physical book collections, sharing libraries with friends, and discovering new books.

🌐 **Live API:** *(Coming in Sprint 6)*  
💻 **Frontend Repo:** *(Coming in Sprint 5)*

---

## 📌 Current Status

**Phase:** Sprint 4 — Core Library & Reviews Complete
**Progress:** `[████████░░] 80%` (Backend)

### ✅ Done
- **Architecture:** Clean Architecture with `Spine.Api`, `Spine.Core`, `Spine.Infrastructure`, `Spine.Worker`, and `Spine.Tests`.
- **Database:** 9 core entities with Global Catalog pattern, Self-referencing Graph (Friendships), and custom tags.
- **Authentication:** JWT Auth, BCrypt hashing, Account Management, and Email Verification enforcement.
- **Event-Driven:** Apache Kafka Producer and Background Consumer Worker.
- **Email:** Resend API integration for actual HTML email delivery (Password Reset & Verification).
- **Testing:** 9 passing Integration Tests using InMemory DB and Mock Kafka.
- **External APIs:** Open Library integration (fetching metadata, covers, authors, and descriptions by ISBN).
- **Library Management:** Add books to personal library, track current page, update reading status (Unread, Reading, Finished, DNF), and write private notes.
- **Reviews:** Users can leave public 1-5 star reviews and text.

### 🚧 In Progress
- Sprint 5: The Social Layer (Friendships & Privacy Settings).

### 📝 Next Up
- Send/Accept friend requests.
- View friends' libraries based on privacy settings.
- Activity Feed generation.

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

### Backend Architecture

```text
Spine/
├── Spine.Api/                  # HTTP endpoints, controllers, DTOs, Services
│   ├── Controllers/            # Auth, Account, UserBooks, Books, Reviews
│   ├── Models/                 # DTOs
│   ├── Services/               # TokenService, KafkaProducerService, OpenLibraryService
│   ├── Events/                 # Event DTOs (PasswordReset, UserRegistered)
│   ├── Validators/             # FluentValidation
│   └── Program.cs
├── Spine.Core/                 # Domain entities (no external dependencies)
│   └── Entities/
├── Spine.Infrastructure/       # EF Core, DbContext, Migrations
│   ├── Data/
│   └── Migrations/
├── Spine.Worker/               # Background service (Kafka Consumer)
│   └── KafkaConsumerWorker.cs
├── Spine.Tests/                # Unit & Integration tests
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

