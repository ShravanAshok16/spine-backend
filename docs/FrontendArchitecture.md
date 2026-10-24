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


## 4. Key Flows

### 4.1 Authentication Flow
- User logs in.
- JWT is stored in `localStorage` (Web) or `SecureStore` (Mobile).
- Axios interceptor attaches JWT to all requests.
- 401 responses trigger a global logout event.

### 4.2 The Scanner Flow (Mobile)
1. User taps "Scan" on the bottom nav.
2. `expo-camera` mounts and requests permissions.
3. Camera reads an EAN-13 (ISBN) barcode.
4. Mobile app calls `GET /api/Books/isbn/{isbn}`.
5. If the book exists, show "Add to Library" button.
6. If the book doesn't exist, call Open Library API via the backend to enrich data, then add it.

## 5. State Management Rules

- **Server State:** Never use `useState` for data coming from the database (like the list of books). Always use TanStack Query. This gives us free caching, background refetching, and loading states.
- **Client State:** Use `useState` or `Context` for UI state (e.g., is the modal open, is the user logged in).