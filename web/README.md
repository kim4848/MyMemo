# MyMemo — Web

React SPA frontend for MyMemo. Records audio, uploads chunks to the API, and displays transcription memos.

## Tech Stack

- React + TypeScript + Vite
- Tailwind CSS v4
- Zustand (state management)
- React Router (routing)
- Clerk (authentication)
- IndexedDB (offline chunk caching)

## Getting Started

```bash
npm install
cp .env.example .env
# Fill in VITE_CLERK_PUBLISHABLE_KEY and VITE_API_URL in .env
npm run dev
```

## Scripts

| Command | Description |
|---------|-------------|
| `npm run dev` | Start dev server |
| `npm run build` | Production build |
| `npm run preview` | Preview production build |
| `npm test` | Run tests once |
| `npm run test:watch` | Run tests in watch mode |

## Environment Variables

| Variable | Description |
|----------|-------------|
| `VITE_CLERK_PUBLISHABLE_KEY` | Clerk publishable key |
| `VITE_API_URL` | Backend API base URL |
