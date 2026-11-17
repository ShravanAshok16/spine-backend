=====================================================================
SPINE API — MASTER ARCHITECTURE & DAILY NOTES
=====================================================================
Author: Shravan Ashok
Project: Spine (Book Library & Social Tracker)
Coverage: Sprints 1 through 7 (Backend Core Complete)
=====================================================================

=====================================================================
PART 0 — COMPLETE SETUP INSTRUCTIONS FROM SCRATCH
=====================================================================

--- 0.1 Prerequisites ---
1. .NET 10 SDK (Verify with `dotnet --version`)
2. Docker Desktop (Required for local Kafka)
3. Neon Account (Free Serverless PostgreSQL)
4. Resend Account (Email Delivery)

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
1. **Global Catalog Pattern:** `Books` table (Global) and `UserBooks` (Join). "Harry Potter" is saved ONCE in the global catalog. Users link to it via `UserBooks`. This saves massive database space.
2. **Soft Deletes:** `UserBooks` has `IsDeleted` and `DeletedAt`. We never hard-delete user data immediately. A background job (planned for later) will hard-delete after 30 days.
3. **Self-Referencing Graph:** `Friendships` has TWO FKs to `Users` (`RequesterId`, `ReceiverId`). We used `DeleteBehavior.Restrict` so deleting a user doesn't cascade-delete their friends.
4. **Explicit Many-to-Many:** `Tags` to `UserBooks` requires a join entity `UserBookTag` because we eventually want to add metadata to the link (e.g., when the tag was added).

--- Day 2 & 3: Auth & Account Management ---
Key Decisions:
1. **JWT Auth:** Claims include `sub` (UserId), `email`, `DisplayName`, `FirstName`, `LastName`. The `FirstName` and `LastName` are needed for the personalized dashboard greeting.
2. **Login Identifier:** Accepts either Email OR DisplayName.
3. **FluentValidation:** Kept strictly to *formatting* (e.g., Email format, Password length). Database checks (like "is this email taken?") were moved to the Controller because the auto-validation pipeline is synchronous.

--- Day 4: Email Verification ---
- When a user registers, `IsEmailVerified = false`.
- `AuthController` publishes a `UserRegisteredEvent` to Kafka.
- The user cannot login until they verify their email via the link sent by the Worker.

=====================================================================
PART 2 — SPRINT 2: KAFKA & EMAILS
=====================================================================

--- Kafka & Resend Integration ---
Why Event-Driven?
Synchronous emails block the API thread. If Resend is slow, the API hangs.
With Kafka, the API drops a message and returns instantly. The Worker handles the email asynchronously.

- **Producer:** `KafkaProducerService` publishes to `password-reset-events` and `user-registered-events`.
- **Consumer:** `Spine.Worker` listens to both topics and calls `EmailService`.
- **Email Service:** Uses Resend API. Note: Resend's sandbox restricts sending to the email you signed up with.

=====================================================================
PART 3 — SPRINT 3 & 4: LIBRARY & EXTERNAL APIS
=====================================================================

--- Open Library Integration ---
- **The 3-Call Problem:** Open Library splits data. To get Title + Author + Description, we call `/isbn/...`, then `/authors/...`, then `/works/...`.
- **Cover Fallback:** If the JSON metadata lacks a `cover_i`, we construct `https://covers.openlibrary.org/b/isbn/{isbn}-L.jpg` directly. This fixes the 80% missing-cover problem.
- **Adding a Book:** `POST /api/UserBooks` checks the Global Catalog first. Only fetches from Open Library if the book doesn't exist.

--- Library Management ---
- **GET /api/UserBooks:** Supports `?status=Reading`, `?search=`, and `?sort=title_asc`.
- **PUT /api/UserBooks/{id}:** Updates `Status`, `CurrentPage`, `PersonalRating`, `PrivateNotes`, and `LentTo`.

--- Day 5: Resilience & Regional Support ---
- **Google Books Fallback:** If Open Library is missing Author, Description, Genres, PageCount, or Cover, we query Google Books API to fill in the blanks.
- **Language Detection:** We detect Kannada, Telugu, and Tamil using Unicode character ranges in the title.
- **Manual Entry:** Added `POST /api/UserBooks/manual` for regional books that lack ISBNs in external APIs.
- **Auto-Complete Pages:** If a book is marked "Finished", `CurrentPage` auto-snaps to `TotalPages`.
- **Duplicate Guard:** Prevents adding a book twice. Checks ISBN AND Title + Author.

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
PART 5 — SPRINT 7 (DAY 5): GRANULAR TRACKING & SERIES
=====================================================================

--- Ownership & Favourites ---
- Added `OwnershipStatus` enum (Owned, Wishlist, LentOut, Sold, Donated, Lost).
- Added `IsFavourite` boolean.
- Added "Paused" to valid reading statuses.

--- New Endpoints ---
- `POST /batch-delete`: Takes a list of `UserBookIds`, soft-deletes them all.
- `PATCH /{id}/favourite`: Toggles the favourite status.
- `PATCH /{id}/progress`: Updates page count. Auto-sets to "Finished" if `CurrentPage >= PageCount`.
- `POST /link-series`: Manually groups books into a Series.

--- Series Search Fix ---
- In `SearchBooksAsync`, we now make a secondary call to the Work record to extract the `series` name and parse it with Regex.

=====================================================================
PART 6 — PROBLEMS FACED AND HOW WE FIXED THEM
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
   - Fix: Created `MockKafkaProducerService` and injected it via `services.RemoveAll`.

5. **Kafka Worker Crash: Topic not found**
   - Cause: Worker starts before the topic exists.
   - Fix: Caught `ConsumeException` (UnknownTopicOrPart) inside the `while` loop and added `await Task.Delay(2000)`.

6. **Resend Sandbox Error**
   - Cause: Trying to send to an email other than the Resend signup email.
   - Fix: Use the Resend email for testing.

7. **Worker: ConnectionString not initialized**
   - Cause: `dotnet watch` ran from root, Worker didn't find `appsettings.json`.
   - Fix: Used `dotnet user-secrets` on the `Spine.Worker` project.

8. **JWT Error: `IDX10208: Unable to validate audience`**
   - Cause: `appsettings.json` was missing the `Jwt:Audience` string.
   - Fix: Added `"Audience": "SpineClient"` and updated TokenService.

9. **GitHub Push Protection: Resend API Key Blocked**
   - Cause: The real API key was inside `bin/Debug/appsettings.json`.
   - Fix: Ran `git rm -r --cached` on `bin/` and `obj/`, verified `.gitignore`.

10. **C# Build Error: UserBook missing Tags**
    - Cause: `Tag.cs` and `UserBookTag.cs` were missing.
    - Fix: Created the files and configured the Explicit Join Entity in `AppDbContext`.


--- PART 1: CORE ARCHITECTURE ---
1. **Global Catalog:** `Books` table is shared. `UserBooks` links users to books. Prevents duplicate data.
2. **Soft Deletes:** `UserBooks` uses `IsDeleted`. We don't hard-delete user data immediately.
3. **Self-Referencing Graph:** `Friendships` has two FKs to Users (`RequesterId`, `ReceiverId`).
4. **Event-Driven:** Kafka used for async emails and activity fan-out.

--- PART 2: SPRINT 8 (DAY 8) RESILIENCE & DISCOVERY ---
- **Provider Pattern:** `BookLookupService` orchestrates `OpenLibraryProvider` and `GoogleBooksProvider`. 
- **Google Books Priority:** Reversed logic to hit Google Books first because it has better descriptions and covers.
- **Caching:** Used `IMemoryCache`. Caches complete books for 12 hours, incomplete ones for 45 seconds (so the "Try Again" button works).
- **IPv4 Fix:** Created `Ipv4HttpHandler.cs`. This bypasses the Windows IPv6 DNS bug that caused OpenLibrary to hang for 19 seconds.
- **Data Cleanup:** Added `TextCleaner` to strip HTML tags (from Google) and dashes (from OpenLibrary).
- **High-Res Covers:** Appended `zoom=3` to Google covers and `?default=false` to OpenLibrary covers (forces 404 on missing images).

--- PART 3: PROBLEMS & FIXES ---
1. **429 Rate Limits:** Caused by missing API key. Fixed by adding to User Secrets.
2. **19-Second API Hangs:** Caused by Windows IPv6 timeouts. Fixed with `Ipv4HttpHandler` + 10s timeout.
3. **Missing Descriptions:** OpenLibrary returned partial data. Fixed by prioritizing Google Books and adding the "Try Again" button on the frontend.
4. **Book Not Found:** Caused by sending internal GUIDs to external APIs. Fixed by using the local `ISBN` or `Title` for external lookups.
=====================================================================

=====================================================================
END OF NOTES
=====================================================================