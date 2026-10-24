# Spine API

A production-grade, event-driven REST API for tracking physical book collections, sharing libraries with friends, and discovering new books.

🌐 **Live API:** *(Coming in Sprint 6)*  
💻 **Frontend Repo:** *(Coming in Sprint 5)*

---

## 📌 Current Status

**Phase:** Sprint 2 — Event-Driven Architecture
**Progress:** `[██░░░░░░░░] 20%`

### ✅ Done
- Solution architecture created: `Spine.Api`, `Spine.Core`, `Spine.Infrastructure`, `Spine.Tests`, `Spine.Worker`.
- **Database Schema Designed:** Created 9 core entities and configured the `AppDbContext` with relationships and indexes.
- **JWT Authentication:** Register, Login, and Account Management (Update Profile, Email, Password).
- **Integration Tests:** 9 tests passing (Auth, Account Management, JWT Authorization).
- **Kafka Setup:** Docker Compose configured with Kafka broker and Kafka UI.
- **Event-Driven Producer:** API publishes `PasswordResetRequested` events to Kafka.
- **Background Consumer:** `Spine.Worker` service listens to Kafka and processes password reset payloads asynchronously.

### 🚧 In Progress
- Sprint 2, Day 4: Integrating Resend (Email Provider) into the Background Worker.

### 📝 Next Up
- Send actual HTML password reset emails.
- Token validation and password reset endpoint.

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
│   ├── Controllers/
│   ├── Models/
│   ├── Services/               # TokenService, KafkaProducerService
│   ├── Events/                 # Event DTOs (PasswordResetRequestedEvent)
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
├── Spine.Worker/               # Background service (Kafka Consumer)
│   └── Worker.cs
├── Spine.Tests/                # Unit & Integration tests
│   ├── Integration/
│   └── Unit/
├── docker-compose.yml          # Local Kafka & Kafka-UI setup
└── README.md

---

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

