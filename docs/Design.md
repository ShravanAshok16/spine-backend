# Spine API — Design System

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
- **Layout:** Vertical stack.
- **Image:** Book cover (aspect ratio 2:3), rounded top corners.
- **Title:** Serif font, 2 lines max, truncated with ellipsis.
- **Author:** Sans-serif, muted text.
- **Status:** Small pill badge (e.g., Green for "Finished", Blue for "Reading").

### 4.2 The Scanner (Mobile)
- **View:** Full-screen camera.
- **Overlay:** Semi-transparent black with a clear square in the middle.
- **Feedback:** When a barcode is detected, flash a green border and vibrate the phone.

## 5. Layout

- **Mobile:** Bottom navigation bar (Library, Scan, Social, Profile).
- **Desktop (Web):** Left sidebar navigation. Main content max-width of 1200px.