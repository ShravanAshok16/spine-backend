=====================================================================
SPINE API — DAILY NOTES, SETUP & RETROSPECTIVE
=====================================================================
Author: Shravan Ashok
Project: Spine (Book Library & Social Tracker)
Coverage: Sprint 1 through Sprint 6 (Backend Complete)
=====================================================================


=====================================================================
PART 0 — COMPLETE SETUP INSTRUCTIONS FROM SCRATCH
=====================================================================

--- 0.1 Prerequisites ---
1. .NET 10 SDK (dotnet --version should print 10.x.x)
2. Docker Desktop (Required for local Kafka)
3. Neon Account (Free Serverless PostgreSQL)
4. Resend Account (For email delivery)

--- 0.2 Configure Secrets (User Secrets) ---
Run these in the root `Spine` folder:
  dotnet user-secrets init --project Spine.Api
  dotnet user-secrets set "ConnectionStrings:Default" "Host=...;Database=neondb;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true" --project Spine.Api
  dotnet user-secrets set "Jwt:Key" "thisIsMySuperSecretJwtKeyForSpineApp_ChangeMe_12345" --project Spine.Api
  
  dotnet user-secrets init --project Spine.Worker
  dotnet user-secrets set "ConnectionStrings:Default" "Host=...;Database=neondb;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true" --project Spine.Worker
  dotnet user-secrets set "Resend:ApiKey" "re_YOUR_API_KEY" --project Spine.Worker

--- 0.3 Start Kafka ---
  docker compose up -d
Access Kafka UI at: http://localhost:8080

--- 0.4 Apply Migrations ---
  dotnet ef database update --project Spine.Infrastructure --startup-project Spine.Api

--- 0.5 Run the Application ---
Terminal 1 (API):    dotnet run --project Spine.Api
Terminal 2 (Worker): dotnet run --project Spine.Worker


=====================================================================
PART 1 — SPRINT 1: DATABASE & AUTHENTICATION
=====================================================================

--- Day 1: Database Schema & Foundation ---
Key Architectural Decisions:
1. **Global Catalog Pattern:** `Books` table (Global) and `UserBooks` (Join). "Harry Potter" is saved ONCE. Users link to it.
2. **Soft Deletes:** `UserBooks` has `IsDeleted`. Never hard-delete user data immediately.
3. **Self-Referencing Graph:** `Friendships` has TWO FKs to `Users` (`RequesterId`, `ReceiverId`). We used `DeleteBehavior.Restrict` so deleting a user doesn't cascade delete their friends.
4. **Many-to-Many:** `Tags` to `UserBooks` requires `UserBookTags`.

--- Day 2 & 3: Auth & Account Management ---
Key Decisions:
1. **JWT Auth:** Stateless. Claims: `sub` (UserId), `email`.
2. **Login Identifier:** Login accepts either Email OR DisplayName.
3. **FluentValidation:** Kept strictly to *formatting*. Database checks (like "is this email taken?") were moved to the Controller because the auto-validation pipeline is synchronous and crashes on async DB calls.

--- Day 4: Email Verification ---
- When a user registers, `IsEmailVerified` is set to false.
- `AuthController` publishes a `UserRegisteredEvent` to Kafka.
- The user cannot login until they verify their email.


=====================================================================
PART 2 — SPRINT 2: KAFKA & EMAILS
=====================================================================

--- Kafka & Resend Integration ---
Why Event-Driven?
Synchronous emails block the API thread. If Resend is slow, the API hangs.
With Kafka: API drops a message and returns instantly. The Worker handles the email.

- **Producer:** `KafkaProducerService` publishes to `password-reset-events` and `user-registered-events`.
- **Consumer:** `Spine.Worker` listens to both topics and calls `EmailService`.
- **Email Service:** Uses Resend API. Note: Resend's sandbox restricts sending to the email you signed up with.


=====================================================================
PART 3 — SPRINT 3 & 4: LIBRARY & EXTERNAL APIS
=====================================================================

--- Open Library Integration ---
- **The 3-Call Problem:** Open Library splits data. To get Title + Author + Description, we call `/isbn/...`, then `/authors/...`, then `/works/...`.
- **Adding a Book:** `POST /api/UserBooks` checks the Global Catalog first. Only fetches from Open Library if the book doesn't exist.

--- Library Management ---
- **GET /api/UserBooks:** Supports `?status=Reading`, `?search=`, and `?sort=title_asc`.
- **PUT /api/UserBooks/{id}:** Updates `Status`, `CurrentPage`, `PersonalRating`, `PrivateNotes`, and `LentTo`.


=====================================================================
PART 4 — SPRINT 5 & 6: SOCIAL LAYER & ACTIVITY FEED
=====================================================================

--- Friendships & Privacy (Sprint 5) ---
- **The Graph:** When User A requests User B, `RequesterId = A`, `ReceiverId = B`, `Status = "Pending"`. When accepted, `Status = "Accepted"`.
- **Privacy Enforcement:** `GET /api/Friendships/{friendId}/library` checks:
  1. Are they friends? (If no -> 403 Forbidden).
  2. Is the friend's `PrivacySetting` "Private"? (If yes -> 403 Forbidden).
  3. If allowed, return books BUT set `PrivateNotes = null`. (Friends shouldn't see private notes).

--- Activity Feed (Sprint 6) ---
- **The Fan-Out:** When User A finishes a book, the API publishes a `BookFinishedEvent` to Kafka.
- **The Worker:** Reads the event, finds all of User A's friends, and inserts a new `Activity` row for the user AND every friend.
- **The DB Context Issue:** Because the Worker is a Singleton `BackgroundService`, it cannot inject `AppDbContext` (Scoped). We must use `IServiceScopeFactory` to create a scope for every message.


=====================================================================
PART 5 — PROBLEMS FACED AND HOW WE FIXED THEM
=====================================================================

1. **FluentValidation Async Error**
   - Cause: `.MustAsync()` checking DB.
   - Fix: Removed from Validator. Moved to Controller.

2. **Tests: Npgsql & InMemory clash**
   - Cause: Both registered in `WebApplicationFactory`.
   - Fix: Added `if (!builder.Environment.IsEnvironment("Testing"))` in `Program.cs`.

3. **Tests: Fresh DB on every request**
   - Cause: `Guid.NewGuid()` inside the options lambda.
   - Fix: Captured DB name OUTSIDE the lambda.

4. **Tests: Kafka 2-minute hang**
   - Cause: Tests tried to connect to `localhost:9092`.
   - Fix: Created `MockKafkaProducerService` (marked real methods `virtual`) and injected it via `services.RemoveAll`.

5. **Kafka Worker Crash: Topic not found**
   - Cause: Worker starts before the topic exists.
   - Fix: Caught `ConsumeException` (UnknownTopicOrPart) inside the `while` loop and added `await Task.Delay(2000)`.

6. **Resend Sandbox Error**
   - Cause: Trying to send to an email other than the Resend signup email.
   - Fix: Use the Resend email for testing.

7. **Worker: ConnectionString not initialized**
   - Cause: `dotnet watch` ran from root, Worker didn't find `appsettings.json`.
   - Fix: Used `dotnet user-secrets` on the `Spine.Worker` project and removed the direct `AppDbContext` injection in favor of `IServiceScopeFactory`.

8. **Build Error: `GetCurrentUserId` missing**
   - Cause: Helper method was accidentally pasted into the wrong controller.
   - Fix: Moved it to the correct class.

9. **README Encoding (Mojibake)**
   - Cause: Saved as Windows-1252.
   - Fix: Switched to UTF-8 in VS Code.


=====================================================================
PART 6 — WHAT WENT WELL
=====================================================================
- **Architecture:** `Spine.Api` and `Spine.Worker` are perfectly decoupled.
- **Resilience:** If Resend goes down, the API stays up, and Kafka holds the message.
- **Testing:** 9 integration tests caught crucial bugs (like the DB reset and Kafka hang).
- **Data Integrity:** The Global Catalog saves massive amounts of DB space.
- **Social Logic:** Implemented a true Graph relationship with strict privacy rules.

=====================================================================
END OF NOTES
=====================================================================