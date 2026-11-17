# Spine API

A production-grade, event-driven REST API for tracking physical book collections, sharing libraries with friends, and discovering new books.

🌐 **Live API:** *(Coming soon)*  
💻 **Frontend Repo:** [ShravanAshok16/spine-ui](https://github.com/ShravanAshok16/spine-ui)

---

## 📌 Current Status

**Phase:** Sprint 8 — Resilience & Discovery (Backend Complete)
**Progress:** `[██████████] 100%` (Backend Core)

### ✅ Done
- **Architecture:** `.NET 10`, EF Core, PostgreSQL (Neon), Kafka, Worker Service.
- **Data Model:** Global Catalog, Granular Tracking (Ownership, Favourites, Paused).
- **Auth:** JWT, Email Verification, Password Reset (Kafka).
- **Provider Pattern:** `BookLookupService` orchestrates Google Books and OpenLibrary.
- **Performance:** `IMemoryCache` (12h TTL), `Ipv4HttpHandler` (fixes 19s hangs), 10s API timeouts.
- **Data Quality:** High-res covers (`zoom=3`), `?default=false` fallbacks, HTML stripping via `TextCleaner`.
- **Social:** Friendships, Privacy enforcement, Kafka Activity Feed.

---

## 🗄️ Data Model

| Table | Purpose |
|---|---|
| `Users` | Accounts, JWT, Privacy settings. |
| `Books` | Global catalog (deduplicated, `PublishYear` added). |
| `Series` | Groups books together. |
| `UserBooks` | Personal library. Includes Status, Ownership, Favourite, Progress. |
| `ReadingSessions` | Planned for Sprint 9. |
| `Friendships` | Social graph. |
| `Activities` | Kafka-driven feed. |

---

## 🏗️ Project Structure

```text
Spine/
├── Spine.Api/                  # Controllers, Services, Middleware
│   ├── Controllers/            # Auth, UserBooks, Books, Quotes, Friendships, etc.
│   ├── Services/               # BookLookup, GoogleBooks, OpenLibrary, Token
│   └── Program.cs
├── Spine.Core/                 # Domain Entities & Enums
├── Spine.Infrastructure/       # EF Core & Migrations
├── Spine.Worker/               # Kafka Consumer (Emails & Feed)
└── Spine.Tests/                # Integration Tests

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

