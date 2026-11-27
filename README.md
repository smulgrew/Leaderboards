# LeadersBoards

A leaderboard app for Hyrox, CrossFit and similar workouts. Create a board, invite your crew, and post scores. **You can't see anyone's results until you've posted your own**, while admins see everything.

```
leadersboards/
├── backend/LeadersBoards.Api/   .NET 10 minimal API + EF Core + Postgres
├── mobile/                      Expo (React Native) app, TypeScript
├── docs/DATA_MODEL.md           Tables, ER diagram, score-type rules, assumptions
├── docs/schema.sql              Generated DDL (for reference; EF migrations are the source of truth)
├── scripts/smoke-test.sh        End-to-end API test with curl
├── start.bat / stop.bat         Windows: double-click to start/stop everything
└── docker-compose.yml           Local Postgres
```

**Why .NET 10 and not 9:** .NET 10 is the current LTS release, supported until November 2028. .NET 11 came out this month but is the shorter-support track. Moving up later is a one-line change in the `.csproj` plus package bumps.

---

## 0. Quick start on Windows (double-click)

Once the three tools below are installed, **double-click `start.bat`** in the repo folder. It:

1. checks Docker Desktop, the .NET 10 SDK and Node.js are installed, and if one is missing tells you which and offers to open its download page,
2. starts Docker Desktop if it isn't running, then starts the Postgres database,
3. runs `npm install` the first time,
4. opens a window called **LeadersBoards API** (`dotnet run`) and one called **LeadersBoards Expo** (`npx expo start`, with the QR code for your phone),
5. waits until the API answers on `http://localhost:5271/health`.

**Double-click `stop.bat`** to close both windows and stop the database. Your data is kept for next time.

Install these once (restart VS Code afterwards so it sees them; that's the usual cause of "dotnet.exe not found"):

| Tool | Download | Notes |
|---|---|---|
| Docker Desktop | https://www.docker.com/products/docker-desktop/ | Open it once after installing so it finishes setup |
| .NET 10 SDK | https://dotnet.microsoft.com/download/dotnet/10.0 | Pick the **SDK** (x64), not "Runtime" |
| Node.js LTS | https://nodejs.org/en/download | Version 20 or newer |

The first time, start.bat asks Windows for admin permission (click **Yes**) to let your phone through the firewall to the API on port 5271 (`scripts/windows/allow-firewall.ps1`). That rule only covers **Private** networks, so if start.bat warns that your Wi-Fi is marked Public, switch it to Private in Settings > Network & internet. If the app says it can't reach the API, open `http://<your-PC's-IP>:5271/health` in your phone's browser: it should show `Healthy`.

**Why a `.bat` and not an `.exe`?** A `.bat` is a plain-text list of commands that Windows runs when you double-click it, so it behaves like an exe but you can open it in VS Code and see exactly what it does. A real exe would have to be compiled, would be flagged by antivirus and SmartScreen because it isn't signed, and couldn't include Docker, .NET or Node anyway (they're big installers that need admin rights). `start.bat` is two lines that hand over to `scripts/windows/start.ps1`, a PowerShell script (PowerShell is Windows' built-in scripting language, roughly "C#-flavoured shell"), where all the checks live with comments.

If Windows shows "Windows protected your PC" when you double-click, it's because the files came from a ZIP download: click **More info, Run anyway**, or right-click the file, **Properties**, tick **Unblock**.

---

## 1. Run the backend (by hand, any OS, about 5 minutes)

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and either Docker or a free hosted Postgres.

```bash
docker compose up -d                      # Postgres on localhost:5432 (user/pass: postgres)
cd backend/LeadersBoards.Api
dotnet run                                # listens on http://0.0.0.0:5271
```

In Development the API **applies migrations on startup**, so there are no manual DB steps. Check it with `curl localhost:5271/health`. The OpenAPI spec is at `/openapi/v1.json`.

To use **Neon or Supabase** instead of Docker, put their connection string in `appsettings.Development.json` under `ConnectionStrings:Default` (or set the `ConnectionStrings__Default` env var).

Then run `scripts/smoke-test.sh` to exercise the whole flow: invites, the hidden-results rule, AMRAP and time-cap ranking, hearts, seen-by, comments, member caps, paying for public boards, the 30-day deletion of public one-offs, and deleting a board. Start the API with `Plans__FreePrivateMemberLimit=3 dotnet run` so the cap test triggers. It passed against Postgres when this skeleton was built.

### How the backend is laid out

| File | What it does |
|---|---|
| `Program.cs` | Wires up DI, EF Core (snake_case names), JSON enums as strings, CORS, endpoints |
| `Domain/Entities.cs`, `Enums.cs` | The tables as C# classes |
| `Domain/ScoreRules.cs` | **All score-type logic**: default sort, validation, ranking with ties and caps, display formatting |
| `Domain/BoardRules.cs`, `PlanOptions.cs` | Board kind, visibility and pricing rules: member caps, pay-to-publish, closing scores, what a payment unlocks, expiry dates |
| `Jobs/ExpiredBoardCleanup.cs` | Hourly background job that deletes public one-off boards 30 days after the event |
| `Data/AppDbContext.cs` | Keys, indexes, relationships |
| `Data/Access.cs` | Permission checks, including **`CanSeeResults`**, the hidden-until-you-post rule |
| `Endpoints/*.cs` | Minimal API route groups: boards, invites, events/scores/hearts/comments |
| `Auth/CurrentUser.cs` | ⚠️ **Dev-only auth**: trusts an `X-User-Id` header. Replace before real users. |

After changing entities, run `dotnet ef migrations add <Name>` (install the tool once with `dotnet tool install -g dotnet-ef`).

### Endpoints

```
POST /dev/login                          {email, displayName} -> user  (dev only)
GET  /me/leaderboards                    boards I've joined
GET  /me/invites                         pending invites
GET  /leaderboards/public?q=             discover public boards
POST /leaderboards/{id}/join             join a public board
POST /leaderboards                       create draft {name, kind, visibility, startsAt?, endsAt?, events[]}
GET  /leaderboards/{id}                  board + events (+ "did I post?")
POST /leaderboards/{id}/events           add event (admin)
DEL  /events/{id}                        remove event with no scores (admin)
POST /leaderboards/{id}/publish          draft -> published (admin; public boards must be paid: 402)
POST /leaderboards/{id}/checkout         start a payment {kind: PublicOneOff | PublicOngoing | PrivateUnlimited}
DEL  /leaderboards/{id}                  delete a board and everything in it (owner)
POST /dev/payments/{id}/mark-paid        dev stand-in for the payment webhook
POST /dev/cleanup-expired?asOf=          run the expiry job now (optionally "as of" a future date)
POST /leaderboards/{id}/invites          {email, role} (admin; 402 when the board is full)
POST /leaderboards/{id}/invites/accept | /decline
PUT  /events/{id}/scores/me              submit/update my score
GET  /events/{id}/results                ranked results, or {locked:true, entryCount}
GET  /events/{id}/seen-by
PUT|DELETE /scores/{id}/heart
GET|POST /events/{id}/comments
```

---

## 2. Run the mobile app

You need Node 20+ and the **Expo Go** app on your phone (App Store / Play Store).

```bash
cd mobile
npm install          # downloads dependencies into node_modules/ (like a NuGet restore)
npx expo start       # starts the dev server and shows a QR code
```

Scan the QR code with your phone's camera (iOS) or the Expo Go app (Android). Your phone and computer must be on the **same Wi-Fi**. The app finds the API automatically at `http://<your-computer's-IP>:5271`. If that doesn't work (for example on a VPN or with a firewall), create `mobile/.env` with `EXPO_PUBLIC_API_URL=http://192.168.x.x:5271` and restart.

Press `r` in the terminal to reload, or just save a file: changes appear on the phone in about a second ("Fast Refresh").

Try it: sign in as `alex@example.com`, create a board with a couple of events and publish it. Then invite `sam@example.com`, sign out, sign in as Sam, accept the invite, and open an event to see the 🔒.

---

## 3. A React Native crash course for a C# developer

You know how to program, so this covers only what's *different*.

**TypeScript ≈ C# with structural typing.** `type User = { id: string }` describes a shape. Any object with an `id` string "is" a User, and nothing checks it at runtime. `string | null` is like `string?`. Union types like `'Time' | 'Reps'` act as enums. `async`/`await` and `Promise<T>` work like `Task<T>`.

**A component is a function that returns UI.**
```tsx
function Greeting({ name }: { name: string }) {   // props = parameters, passed as one object
  return <Text>Hello {name}</Text>;                // JSX: markup inside JS. {…} drops a value in.
}
// used as: <Greeting name="Charlie" />
```
`<View>` ≈ `<div>`, `<Text>` is the only place text may appear, `<Pressable>` is a button, `<TextInput>` is an input, and `<FlatList>` is a virtualised list.

**State + re-render is the core idea.** There's no `INotifyPropertyChanged` and no manual UI updates:
```tsx
const [count, setCount] = useState(0);   // a variable React watches
<Button onPress={() => setCount(count + 1)} />   // change it -> React calls your function again -> new UI
```
Your component function runs again on **every** render, top to bottom, so it should be fast and have no side effects. Fetching data goes in hooks like `useEffect` or `useFocusEffect` instead.

**Never mutate state, replace it.** `setEvents([...events, newOne])` builds a new array, whereas `events.push(newOne)` won't re-render. `...` is the spread operator: "copy everything from here".

**Hooks** are functions starting with `use` (`useState`, `useCallback`, `useSession`). Call them only at the top level of a component, never inside `if` or loops.

**Context** is React's DI container. `src/lib/session.tsx` provides the logged-in user, and any screen calls `useSession()` to get it.

**Expo Router: files are routes.** `src/app/boards/[id].tsx` is the screen for `/boards/123`. It reads `id` with `useLocalSearchParams()` and navigates with `router.push('/events/456')` or `<Link href=…>`. `_layout.tsx` wraps the screens in a navigator.

**Styling** uses `StyleSheet.create({...})` with CSS-like camelCase properties. Layout is flexbox, and the default direction is **column**.

### Where to look first in `mobile/`

| File | Read it for |
|---|---|
| `src/api/client.ts` | How `fetch` calls the API; types mirroring the C# DTOs |
| `src/app/index.tsx` | Simplest screen: a form with `useState` |
| `src/app/boards/index.tsx` | Loading a list, `Promise.all`, invites accept/decline |
| `src/app/events/[id].tsx` | The locked/unlocked results screen and hearts |
| `src/lib/scoreInput.ts` | Turning "4:05.3" or "5+12" into numbers (plain functions, no React) |

Useful commands: `npm run typecheck` (like a build with no output) and `npx expo install <pkg>`. **Always use this instead of `npm install` for new libraries**, because it picks the version that matches your Expo SDK.

---

## 4. Keeping it free

| Piece | Free option |
|---|---|
| Postgres | Neon or Supabase free tier |
| API hosting | Fly.io / Railway / Azure App Service F1 (small container) |
| App builds | EAS Build free tier; Expo Go needs no build at all during dev |
| Push notifications | Expo Push (free) |
| AI image import | Vision model per photo, fractions of a cent each |
| Payments | Stripe: no monthly fee, about 1.5% + €0.25 per EU card payment (about €0.55 on €20) |

**A catch with app-store rules:** Apple and Google generally require their own in-app purchase system (15% for small developers) for paid digital features bought *inside* the app, and unlocking a leaderboard probably counts. The cheapest route is usually to let organisers pay on a simple web page with Stripe Checkout, which the API already supports via the checkout endpoint. Whether and how the iOS app may link to that page varies by country, so check the current rules before launching paid features. If you'd rather keep everything in the app, RevenueCat is the usual way to wrap in-app purchases.

## 5. Not built yet (in suggested order)

1. **Real auth.** Swap `Auth/CurrentUser.cs` for JWT bearer auth (Supabase Auth is free and the simplest option) and store the token with `expo-secure-store`.
2. Capped-time entry and Rx/Scaled toggle in the app. The API already supports both.
3. Seen-by and comments screens. The API endpoints exist.
4. AI import from a photo into a draft board.
5. Weekly recap job and push notifications.
