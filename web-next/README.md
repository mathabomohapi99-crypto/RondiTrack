# RondiTrack: web-next (Next.js 16)

The RondiTrack frontend rebuilt on Next.js 16.4.1 (App Router). Pages are Server Components. The browser never calls the API: the Next.js server does, with a token kept in an encrypted, HttpOnly cookie that the browser cannot read.

## Running it

1. Start the API: `dotnet run --project API --launch-profile http` (port 5134).
2. Copy `.env.example` to `.env.local` and fill in:
   - `API_BASE_URL` (e.g. `http://localhost:5134`)
   - `SESSION_SECRET`, made with `node -e "console.log(require('crypto').randomBytes(32).toString('base64'))"`
3. In `web-next/`:
   ```bash
   npm install
   npm run gen:api   # regenerates src/types/schema.d.ts from the running API
   npm run dev       # http://localhost:3000
   npm test
   npm run build
   ```

Demo users (created by registering, then roles set in the database): `lerato.demo@example.com` (Treasurer of Ubuntu Savers, Member of Festive Grocery Club) and `sipho.demo@example.com` (Member of Ubuntu Savers only). Password for both: `Demo-password-123` (a throwaway demo value).

**Why neither variable starts with `NEXT_PUBLIC_`:** Next.js copies every `NEXT_PUBLIC_` variable into the JavaScript sent to the browser. The API address and the encryption secret must stay on the server. A leaked `SESSION_SECRET` would let anyone forge a session cookie.

## API check

| # | Question | My answer |
|---|----------|-----------|
| 1 | Does the list return my role per stokvel? What for a user in no stokvels? | **No, so I fixed the API.** `GET /api/stokvels` is platform-admin only (403 for everyone else) and has no role. I added `GET /api/me/stokvels`: every stokvel I belong to with `myRole`, paid and owed this cycle and the next payout date. Lerato gets `Treasurer` for Ubuntu Savers and `Member` for Festive Grocery Club. A user in no stokvels gets **200 with `[]`** (tested). |
| 2 | Non-member vs missing id vs `not-a-guid`: same status? | **Yes, all three are 404.** A non-member and a missing id return the same problem+json (an existing test compares them). `not-a-guid` does not match the `{stokvelId:guid}` route, so it is a plain 404 with an empty body. The page shows the same not-found page for all three. |
| 3 | Which header carries the refresh token? | `Set-Cookie: rondi_refresh=<token>; expires=...; path=/api/auth; secure; samesite=strict; httponly`, read with `getSetCookie()` in `scripts/check-login.mjs`. Output: status 200, body keys `accessToken`, `expiresAtUtc`, `tokenType`. The refresh token is not in the body. |
| 4 | How long does the access token live? What when expired? | **10 minutes** (`Jwt:AccessTokenMinutes`). The API then returns 401 problem+json and the app sends the person to `/login?expired=1` (no refresh until Day 3). The session cookie lasts 7 days, but the access token inside it only works for 10 minutes. |
| 5 | Which IP does the API see for ten different people? | The Next.js server's IP, every time. The API has no login rate limit yet (6.1 gap), so nothing breaks today. See Known gaps. |
| 6 | Does `GET /stokvels/{id}` return member count and a cycle summary? | Member count: yes already. My role and the cycle summary: **no, so I added them** (`myRole` and `currentCycle` with collected amount and next payout date). |

**API fixes, with tests:** `GET /api/me`, `GET /api/me/stokvels` (new `MeController` and `MyStokvelsService`) and the extra fields on `GET /api/stokvels/{id}`. Tests are in `RondiTrack.Tests/MyStokvelsTests.cs`: me, 401 without a token, an empty list for a user in no stokvels, the role per stokvel (and nobody else's stokvels), paid/owed, and detail with role and cycle.

```
Test summary: total: 66, failed: 0, succeeded: 66, skipped: 0
```

**Assumption:** the data model has no payout schedule, so "next payout date" is the open cycle's start plus one period (weekly 7 days, fortnightly 14 days, monthly 1 month).

## Routes

| Route | What it shows | When there is nothing / not theirs |
|-------|---------------|------------------------------------|
| `/` | Redirects to `/dashboard/member` when signed in, `/login` when not | n/a |
| `/login` | Sign-in (Server Action + `useActionState`) | Wrong password shows the message and the reference |
| `/dashboard/member` | Every stokvel I belong to: paid, owed, next payout | Empty state |
| `/dashboard/treasurer` | Only stokvels where I am Treasurer, each linking to its detail page | Empty state (not an error, not a 403) |
| `/stokvels` | My stokvels as cards with my role | Empty state |
| `/stokvels/[id]` | Name, contribution, my role, member count, current cycle summary | `not-found.tsx` (same page for non-member, missing and malformed id) |

Data: `GET /api/me/stokvels` (one call builds the stokvel list and both dashboards, because it returns my role per stokvel), `GET /api/stokvels/{id}`, and `GET /api/me` at sign-in.

Because `loading.tsx` lets the page start streaming first, `notFound()` arrives as HTTP 200 with a `noindex` meta tag. That is how streaming works in Next.js 16, so I checked what the person sees, not the status code.

The sidebar has four links: Home, My stokvels, Member dashboard and Treasurer dashboard. `/stokvels/[id]` needs an id, so it is reached from the lists.

## The treasurer dashboard

RondiTrack has no "treasurer" users. It has users who are Treasurer of *some* stokvels, so one person can be Treasurer of one stokvel and Member of another. `/dashboard/treasurer` is therefore a filtered view that everyone may open, not a page behind a role check. It uses the pure function `stokvelsWhereTreasurer(list)` (`src/lib/stokvelsWhereTreasurer.ts`), tested in Vitest for a mixed list, a Treasurer-nowhere list and an empty list.

**Why the session stores no role:** a role belongs to a person *and a stokvel*. One role in the cookie or the token would be wrong for at least one of their stokvels, and it would go stale when a chairperson changes it. (The API's access token has no stokvel role either, for the same reason.) So the role comes from the API's answer for each stokvel, every time.

The sidebar shows all four links to everyone. Hiding "Treasurer" from people who are Treasurer nowhere would only be a convenience. The page is safe either way, because the API decides what each person may see.

## The client boundary

Files with `'use client'`, and the one reason each needs it:

- `src/components/StokvelCard.tsx`: the Show details toggle uses `useState`.
- `src/app/(auth)/login/LoginForm.tsx`: it uses `useActionState`, a React hook.
- `src/app/(app)/error.tsx`: error pages must be Client Components, and Try again uses `onClick`.

No Client Component imports `session.ts` or `api.ts`, and both start with `import 'server-only'`, so doing it breaks the build. `LoginForm` imports the Server Action from `actions.ts`. A `'use server'` import becomes only a reference to the server function, so the session code does not enter the browser bundle.

## Server-only proof

**Build error with `getSession` imported into `StokvelCard.tsx` (a Client Component):**

```
> Build error occurred
Error: Turbopack build failed with 3 errors:

./src/lib/session.ts:1:1
Error: 'server-only' cannot be imported from a Client Component module
> 1 | import 'server-only'; // Build error if a Client Component ever imports this file.
    | ^^^^^^^^^^^^^^^^^^^^^
  2 | import { cookies } from 'next/headers';

It should only be used from a Server Component.

Import traces:
  #3 [Client Component Browser]:
    ./src/lib/session.ts [Client Component Browser]
    ./src/components/StokvelCard.tsx [Client Component Browser]
    ./src/components/StokvelCard.tsx [Server Component]
    ./src/app/(app)/stokvels/page.tsx [Server Component]
```

**Build passing after I removed the import:**

```
▲ Next.js 16.4.1 (Turbopack)
✓ Compiled successfully in 9.7s
  Finished TypeScript in 11.0s ...
✓ Generating static pages using 1 worker (8/8) in 3.7s

Route (app)
┌ ƒ /
├ ○ /_not-found
├ ƒ /dashboard/member
├ ƒ /dashboard/treasurer
├ ƒ /login
├ ƒ /stokvels
└ ƒ /stokvels/[id]
```

Without the guard, `session.ts` (the code that decrypts the cookie and builds the Bearer header, and needs the server-only `SESSION_SECRET`) would have been bundled into the JavaScript sent to every browser. The guard turns that mistake into a build error instead of a code review comment.

## Known gaps

- **Login rate limit (Q5):** the API has no login rate limit yet. Once it has a per-IP one, every sign-in will come from the Next.js server's IP, so all users would share one limit and could lock each other out. Not fixed this week.
- **Error messages in production:** Next.js hides the message of errors thrown in Server Components in production builds and shows only a digest. The friendly message and reference show fully in `npm run dev`; in `npm start` the page may show only the generic text and the digest as the reference.
- **Next payout date** is calculated (cycle start + one period) because the data model has no payout schedule.
- **Seed data:** my development database has leftover test data (for example Ubuntu Savers has 8 members with a maximum of 6, and several open cycles). The API treats the open cycle with the highest cycle number as the current one.
- **Platform admins:** the API lets a platform admin open any stokvel, so `myRole` can be null there and the page says so.
- **Roles** are set directly in the database, because there is still no endpoint to change a member's role (6.1 gap).
- No refresh, registration or route protection yet (Day 3). An expired token sends the person to `/login?expired=1`.
- API responses are cast to my own types (`src/lib/types.ts`), because the generated OpenAPI says the role and frequency enums are integers while the API sends strings.
- The `@theme` warning in `npm run build` comes from the Tailwind CSS setup of the generated template. The build succeeds.