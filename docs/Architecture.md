# Spine API — Architecture

> Version: 1.0
> Last Updated: 2026-10-24

## 1. System Context

The system is split into an API layer and a Background Worker layer to handle asynchronous, event-driven tasks (like email delivery).

```mermaid
flowchart TD
    %% Clients
    Web[React 19 Web App]
    Mobile[React Native Mobile App]
    
    %% Backend Services
    API[Spine API\nASP.NET Core 10]
    Worker[Spine.Worker\nBackground Service]
    
    %% Infrastructure
    DB[(Neon PostgreSQL)]
    Kafka{Kafka Broker}
    
    %% Connections
    Web -->|HTTP / JSON| API
    Mobile -->|HTTP / JSON| API
    
    API -->|EF Core| DB
    API -->|Publishes Events| Kafka
    
    Kafka -->|Consumes Events| Worker
    Worker -->|Sends Emails| EmailProvider[Resend / SendGrid]
    
    %% Styling
    classDef client fill:#eff6ff,stroke:#3b82f6,stroke-width:2px;
    classDef backend fill:#f0fdf4,stroke:#22c55e,stroke-width:2px;
    classDef infra fill:#fef2f2,stroke:#ef4444,stroke-width:2px;
    
    class Web,Mobile client;
    class API,Worker backend;
    class DB,Kafka infra;


---

### 2. `docs/Design.md`

```markdown
# Spine — Design System

> Version: 1.0
> Last Updated: 2026-10-24

## 1. Purpose

This design system ensures the visual language of Spine (Web and Mobile) remains consistent. While Spine is currently a backend project, the UI tokens and component rules are defined here for the upcoming React frontend.

## 2. Design Principles

1. **Book-First:** Covers and titles are the heroes of the UI.
2. **Warm & Inviting:** Avoid sterile grays. Use warm paper tones and deep ink colors.
3. **Accessibility:** High contrast text, large tap targets for mobile scanning.

## 3. Visual Identity

### 3.1 Color Palette (Light Mode)

| Token | Hex | Usage |
|---|---|---|
| `--color-bg` | `#FDFBF7` | Page background (warm paper) |
| `--color-surface` | `#FFFFFF` | Cards, panels |
| `--color-border` | `#E5E0D8` | Dividers, input borders |
| `--color-text` | `#1C1917` | Primary text (dark ink) |
| `--color-text-muted` | `#78716C` | Secondary text, placeholders |
| `--color-primary` | `#4338CA` | Primary actions (Indigo) |
| `--color-primary-hover` | `#3730A3` | Button hover state |
| `--color-success` | `#10B981` | Finished books, success toasts |
| `--color-warning` | `#F59E0B` | Lent out books, warnings |
| `--color-error` | `#EF4444` | Errors, destructive actions |

### 3.2 Typography

- **Sans:** Inter (UI, body text)
- **Serif:** Merriweather or Lora (Book Titles, Headings for a classic literary feel)
- **Mono:** JetBrains Mono (ISBNs, Code)

## 4. Components

### 4.1 The Book Card

```mermaid
block-beta
  columns 1
    cover["Book Cover Image (Aspect 2:3)"]
    block:details
      columns 1
      title["Title (Serif, 2 lines max)"]
      author["Author (Sans, muted)"]
      status["Status Badge (e.g., Finished)"]
    end


---

### 3. `docs/FrontendArchitecture.md`

```markdown
# Spine Frontend — Architecture

> Version: 1.0
> Last Updated: 2026-10-24

## 1. Overview

The Spine frontend will consist of two separate React applications that share the same design system and API:
1. **Web App (React + Vite):** For managing your library, writing reviews, and viewing friends' collections on a large screen.
2. **Mobile App (React Native + Expo):** Focused primarily on the barcode scanner for adding books to your library while at a bookstore.

## 2. Tech Stack

- **Framework:** React 19
- **Language:** TypeScript
- **Web Build Tool:** Vite
- **Mobile Build Tool:** Expo (EAS)
- **Styling:** Tailwind CSS v4 (Web) + NativeWind (Mobile)
- **State Management:** TanStack Query (Server State) + React Context (Auth)
- **Routing:** React Router v7 (Web) + Expo Router (Mobile)

## 3. Folder Structure (Web & Mobile)

```text
src/
├── api/            # Axios instance and endpoint definitions
├── components/     # UI primitives (Button, Input, BookCard)
├── features/       # Feature-based modules
│   ├── auth/       # Login, Register, Forgot Password
│   ├── library/    # User's personal books
│   ├── social/     # Friends, Activity Feed
│   ├── reviews/    # Book reviews
│   └── scanner/    # (Mobile only) Barcode camera logic
├── hooks/          # Shared hooks (useDebounce)
├── lib/            # Utilities (formatting, helpers)
├── pages/          # Route components (Web)
├── app/            # Route screens (Mobile)
└── types/          # TypeScript interfaces matching backend DTOs


---

### 4. `docs/Requirement.md`

```markdown
# Spine — Requirements Document

> Version: 1.0
> Last Updated: 2026-10-24

## 1. Project Overview

Spine is a full-stack, mobile-first application designed to help avid readers digitize their physical book collections, track reading progress, and share their libraries with friends. It consists of a .NET 10 REST API, a React Web App, and a React Native Mobile App (for barcode scanning).

## 2. Functional Requirements

### 2.1 User Management & Auth
- **Registration:** Email, Password, First Name, Last Name, Phone Number.
- **Login:** JWT-based.
- **Account Management:** Users can update their display name, email, and password.
- **Privacy:** Users can set their library to `Public`, `FriendsOnly`, or `Private`.
- **Password Reset:** Event-driven reset link sent via Kafka and Email.

### 2.2 Library Management
- **Add Books:**
  - Scan ISBN barcode via mobile app.
  - Search by Title/Author.
  - Manual entry (if the Open Library API fails).
- **Track Progress:** Mark books as Unread, Reading, Finished, or Did Not Finish (DNF). Track current page.
- **Tags/Shelves:** Users can create custom tags (e.g., "Sci-Fi", "Borrowed", "To Buy").
- **Lending:** Mark a book as "Lent to [Friend's Name]".
- **Export:** Export the library to a CSV file.

### 2.3 Book Details & Discovery
- **Metadata:** View Summary, Page Count, Series info, and Cover Image.
- **Where to Buy:** Generate search links for Amazon and Flipkart using the book's ISBN.
- **Reviews:** Users can leave a public 1-5 star rating and written review. Private notes stay private.

### 2.4 Social Features
- **Friendships:** Send, accept, and decline friend requests.
- **Shared Library:** View friends' libraries based on their privacy settings.
- **Activity Feed:** See when friends finish a book or leave a 5-star review.

## 3. Non-Functional Requirements

### 3.1 Performance
- Barcode scanning must resolve to a book in < 2 seconds (using Redis caching for repeated ISBN scans).
- API responses must be paginated to prevent heavy payloads.

### 3.2 Scalability & Reliability
- Password reset emails must be asynchronous (via Kafka) so the API never blocks.
- Database must handle many-to-many relationships efficiently (Global Catalog pattern).

### 3.3 Security
- Passwords hashed with BCrypt.
- JWT expiration (30 mins).
- Rate limiting on the Forgot Password endpoint.
- Strict data filtering (Private notes must never be sent to the frontend if a friend is viewing the library).

## 4. Out of Scope (For V1)
- E-reader integration.
- Audiobook tracking.
- Selling or trading books directly in the app.
- Admin panel.

## 5. Success Metrics
- Users can scan a book and add it to their library in under 5 seconds.
- Lighthouse score > 90 for the Web Frontend.
- 9 backend integration tests passing in CI/CD.