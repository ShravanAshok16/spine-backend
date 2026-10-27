=====================================================================
SPINE API — DAILY NOTES, SETUP & RETROSPECTIVE
=====================================================================
Author: Shravan Ashok
Project: Spine (Book Library & Social Tracker)
Coverage: Sprint 1 through Sprint 4
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
  dotnet user-secrets set "Resend:ApiKey" "re_YOUR_API_KEY" --project Spine.Worker

--- 0.3 Start Kafka ---
We use Docker to run a local Kafka broker and UI.
  docker compose up -d
Access Kafka UI at: http://localhost:8080

--- 0.4 Apply Migrations ---
  dotnet ef database update --project Spine.Infrastructure --startup-project Spine.Api

--- 0.5 Run the Application ---
Because of Event-Driven Architecture, you need TWO terminals:
Terminal 1 (API):    dotnet run --project Spine.Api
Terminal 2 (Worker): dotnet run --project Spine.Worker


=====================================================================
PART 1 — SPRINT 1: DATABASE & AUTHENTICATION
=====================================================================

--- Day 1: Database Schema & Foundation ---
Key Architectural Decisions:
1. **Global Catalog Pattern:** We created a `Books` table (Global) and a `UserBooks` table (Join). This prevents saving "Harry Potter" 1,000 times for 1,000 users. It's saved once, and users link to it.
2. **Soft Deletes:** `UserBooks` has `IsDeleted` and `DeletedAt`. We never hard-delete user data immediately.
3. **Self-Referencing Graph:** The `Friendships` table has TWO foreign keys to `Users` (`RequesterId` and `ReceiverId`). We used `DeleteBehavior.Restrict` so deleting a user doesn't cascade delete their friends.
4. **Many-to-Many:** `Tags` to `UserBooks` requires an automatic join table (`UserBookTags`).

--- Day 2 & 3: Auth & Account Management ---
Key Architectural Decisions:
1. **JWT:** Stateless auth. Claims: `sub` (UserId), `email`.
2. **Login via Email OR Username:** We changed the `LoginRequest` to use `Identifier` and check `u.Email == identifier || u.DisplayName.ToLower() == identifier`.
3. **FluentValidation:** Kept validation strictly to *formatting*. Database checks (like "is this email already taken?") were moved into the Controller because the auto-validation pipeline is synchronous and cannot handle async database calls.


=====================================================================
PART 2 — SPRINT 2: EVENT-DRIVEN ARCHITECTURE & EMAILS
=====================================================================

--- Day 1–4: Kafka, Resend, and Email Verification ---
Why Event-Driven?
If the API sends an email synchronously, the user waits 2–3 seconds. If Resend is down, the request crashes. 
With Kafka:
1. API saves token to DB.
2. API drops a JSON message in Kafka and returns `200 OK` instantly.
3. `Spine.Worker` reads Kafka and sends the email.

Key Features:
- **Producer:** `KafkaProducerService` publishes to `password-reset-events` and `user-registered-events`.
- **Consumer:** `Spine.Worker` listens to both topics.
- **Email Verification:** Users cannot log in until they click the link in the email sent by Resend.
- **Resend Sandbox:** On the free tier, you can only send emails to the email address you used to sign up for Resend.


=====================================================================
PART 3 — SPRINT 3 & 4: EXTERNAL APIS & LIBRARY MANAGEMENT
=====================================================================

--- Day 1–2: Open Library Integration ---
- **OpenLibraryService:** Calls `https://openlibrary.org/isbn/{isbn}.json`.
- **The 3-Call Problem:** Open Library splits its data. To get Title + Author + Description, we had to make 3 HTTP calls (1 for the book, 1 for the author key, 1 for the work key).
- **Adding a Book:** `POST /api/UserBooks` takes an ISBN, checks if the `Book` exists in our Global Catalog. If not, it saves it. Then it links it to the user in `UserBooks`.

--- Day 3–4: Library Management & Reviews ---
- **GET /api/UserBooks:** Supports filtering (`?status=Reading`), searching (`?search=hobbit`), and sorting (`?sort=title_asc`).
- **PUT /api/UserBooks/{id}:** Updates `Status`, `CurrentPage`, `PersonalRating` (1-5 int), `PrivateNotes`, and `LentTo`.
- **Reviews:** Users can post a 1-5 star review. The API calculates the `AverageRating` for the book using LINQ `Average()`.


=====================================================================
PART 4 — PROBLEMS FACED AND HOW WE FIXED THEM
=====================================================================

1. **FluentValidation Error: `AsyncValidatorInvokedSynchronouslyException`**
   - Cause: Used `.MustAsync()` to check DB for existing emails.
   - Fix: Removed it from the Validator. Moved the DB check to `AuthController` using `await _db.Users.AnyAsync(...)`.

2. **Test Error: `Npgsql` and `InMemory` providers registered together**
   - Cause: `WebApplicationFactory` registered both the real DB and the test DB.
   - Fix: Added `if (!builder.Environment.IsEnvironment("Testing"))` around the Npgsql registration in `Program.cs`.

3. **Test Error: `Register 200` followed by `Login 401`**
   - Cause: `options.UseInMemoryDatabase("Db_" + Guid.NewGuid())` created a fresh DB for every request.
   - Fix: Captured the DB name *outside* the lambda: `var dbName = "Db_" + Guid.NewGuid();`.

4. **Test Error: Kafka Hangs for 2 minutes**
   - Cause: The tests tried to connect to `localhost:9092`, which wasn't running.
   - Fix: Created `MockKafkaProducerService` and used `services.RemoveAll` / `AddSingleton` in the test factory to swap the real Kafka service for the mock. Also marked the real methods as `virtual`.

5. **Test Error: `MockKafkaProducerService` NullReferenceException**
   - Cause: The mock called `base(null!)`, causing the base constructor to crash when trying to read `config["..."]`.
   - Fix: Changed the base constructor to use `config?["..."]` to handle null gracefully.

6. **Kafka Worker Error: `Subscribed topic not available`**
   - Cause: The Worker started before the topics (`user-registered-events`) were created by the API.
   - Fix: Moved `consumer.Consume()` inside the `try` block and caught `ConsumeException` when `ex.Error.Code == ErrorCode.UnknownTopicOrPart`. Added a 2-second delay and retry.

7. **Resend Error: `You can only send testing emails to your own email address`**
   - Cause: Resend's free sandbox restricts sending to any email other than the signup email.
   - Fix: Register a user using the exact email used to sign up for Resend. (For production, a custom domain must be verified).

8. **Build Error: `GetCurrentUserId` does not exist**
   - Cause: Pasted the protected `AccountController` code into `AuthController` (which has no `[Authorize]`).
   - Fix: Moved the helper method back to the correct controller.

9. **README File Encoding (Mojibake)**
   - Cause: Saving with Windows-1252 turned emojis into gibberish.
   - Fix: In VS Code, changed encoding to UTF-8 (bottom right).


=====================================================================
PART 5 — WHAT WENT WELL & KEY TAKEAWAYS
=====================================================================

- **Architecture:** The separation of `Spine.Api` and `Spine.Worker` works perfectly. It's a real-world microservice pattern.
- **Resilience:** Even if Resend goes down, the API returns 200 OK, and the message is safe in Kafka.
- **Testing:** The integration tests caught several bugs before they hit production.
- **External APIs:** We learned how to normalize messy third-party data (Open Library) into our own DTOs.

=====================================================================
END OF NOTES
=====================================================================