# Spine API

A production-grade, event-driven REST API for tracking physical book collections, sharing libraries with friends, and discovering new books.

🌐 **Live API:** *(Coming in Sprint 7)*  
💻 **Frontend Repo:** [ShravanAshok16/spine-ui](https://github.com/ShravanAshok16/spine-ui)

---

## 📌 Current Status

**Phase:** Sprint 7 — Web Frontend Integration
**Progress:** `[██████████] 100%` (Backend Core)

### ✅ Done
- **Solution architecture:** `Spine.Api`, `Spine.Core`, `Spine.Infrastructure`, `Spine.Tests`, `Spine.Worker`.
- **Database:** Global Catalog pattern (`Books` + `UserBooks`), granular tracking (`OwnershipStatus`, `IsFavourite`, `Paused` status).
- **Auth:** JWT (Email/DisplayName), Email Verification, Password Reset (Kafka).
- **External APIs:** Open Library + Google Books fallback. ISBN Cover Fallback (direct URL generation).
- **Library Management:** Batch Delete, Optimistic Updates support, Manual Series Creation, Duplicate Guard (ISBN + Title/Author).
- **Social:** Friendships, Privacy enforcement, Kafka Activity Feed.
- **Dynamic Data:** Open Library search, Dynamic Quote of the Day (24h cache).

---

## 🗄️ Data Model

| Table | Purpose |
|---|---|
| `Users` | Accounts, JWT, Privacy settings. |
| `Books` | Global catalog (deduplicated). |
| `Series` | Groups books together. |
| `UserBooks` | Personal library. Includes `Status` (Unread, Reading, Paused, Finished, DNF), `Ownership` (Owned, Wishlist, LentOut, etc.), `IsFavourite`, `CurrentPage`. |
| `Tags` / `UserBookTags` | Custom shelves (Many-to-Many). |
| `Reviews` | Public ratings and text. |
| `Friendships` | Social graph (Requester/Receiver). |
| `Activities` | Kafka-driven feed. |
| `Quotes` | (Deprecated in favor of dynamic API, kept for future quote vault). |
| `ReadingSessions` | Planned for Sprint 8. |

---

## 🏗️ Project Structure

```text
Spine/
├── Spine.Api/                  # HTTP endpoints, controllers, DTOs, Services
│   ├── Controllers/            # Auth, Account, UserBooks, Books, Reviews, Friendships, Quotes, Contact
│   ├── Models/                 # DTOs
│   ├── Services/               # TokenService, KafkaProducer, OpenLibrary, Email
│   ├── Events/                 # Event DTOs
│   └── Validators/             # FluentValidation
├── Spine.Core/                 # Domain entities & Enums
│   ├── Entities/
│   └── Enums/                  # OwnershipStatus
├── Spine.Infrastructure/       # EF Core, DbContext, Migrations
├── Spine.Worker/               # Background service (Kafka Consumer)
│   ├── Services/               # EmailService (Resend)
│   └── KafkaConsumerWorker.cs
├── Spine.Tests/                # Integration tests
├── docker-compose.yml          # Local Kafka
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

