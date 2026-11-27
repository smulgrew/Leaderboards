// ─────────────────────────────────────────────────────────────────────────────
// API client: every call the app makes to the .NET backend goes through here.
//
// JS/TS notes for a C# dev:
//  • `type X = {...}` is like a C# record/interface, but it only exists at compile
//    time. At runtime the JSON is just a plain object; nothing checks it.
//  • `async`/`await` work the same as C#. A `Promise<T>` is JS's `Task<T>`.
//  • `fetch` is the built-in HTTP client (like HttpClient), available in RN.
// ─────────────────────────────────────────────────────────────────────────────
import Constants from 'expo-constants';

// Which URL is the API on?
//  1. If you set EXPO_PUBLIC_API_URL (e.g. in a .env file), use that. Any env var
//     starting with EXPO_PUBLIC_ is baked into the app at build time.
//  2. Otherwise, in dev, reuse the IP of the computer running `npx expo start`.
//     Your phone can't reach "localhost" (that's the phone itself), but it can reach
//     your laptop's LAN IP, which Expo already knows as hostUri ("192.168.1.20:8081").
function resolveApiUrl(): string {
  if (process.env.EXPO_PUBLIC_API_URL) return process.env.EXPO_PUBLIC_API_URL;
  const host = Constants.expoConfig?.hostUri?.split(':')[0] ?? 'localhost';
  return `http://${host}:5271`;
}
export const API_URL = resolveApiUrl();

// ── Types that mirror backend/Contracts/Dtos.cs ──────────────────────────────
// A union of string literals is TS's version of an enum: only these values compile.
export type ScoreType = 'Time' | 'Reps' | 'Weight' | 'RoundsReps' | 'Distance' | 'Calories';
export type MemberRole = 'Member' | 'Admin' | 'Owner';
export type LeaderboardKind = 'OneOff' | 'Ongoing';   // dated comp vs never-ending (daily WODs)
export type Visibility = 'Private' | 'Public';         // invite-only vs tap-to-join

export type User = { id: string; email: string; displayName: string };
export type LeaderboardSummary = {
  id: string; name: string; kind: LeaderboardKind; visibility: Visibility; status: string;
  myRole: MemberRole; eventCount: number; startsAt: string | null;
};
export type PublicLeaderboard = {
  id: string; name: string; description: string | null; kind: LeaderboardKind;
  startsAt: string | null; endsAt: string | null; memberCount: number; isFull: boolean; amMember: boolean;
};
export type EventInfo = {
  id: string; name: string; description: string | null; scoreType: ScoreType;
  timeCapSeconds: number | null; position: number; scheduledOn: string | null; iSubmitted: boolean;
};
export type Leaderboard = {
  id: string; name: string; description: string | null; kind: LeaderboardKind; visibility: Visibility; status: string;
  startsAt: string | null; endsAt: string | null; plan: 'Free' | 'OneOffEvent' | 'Pro';
  memberLimit: number | null; memberCount: number; needsPaymentToPublish: boolean;
  myRole: MemberRole; events: EventInfo[];
};
export type Invite = { leaderboardId: string; leaderboardName: string; invitedBy: string; invitedAt: string };
export type ResultEntry = {
  rank: number; scoreId: string; userId: string; displayName: string; display: string;
  isCapped: boolean; isRx: boolean; hearts: number; heartedByMe: boolean;
};
export type Results = {
  eventId: string; eventName: string; scoreType: ScoreType; locked: boolean; entryCount: number; entries: ResultEntry[];
};
export type NewEvent = { name: string; scoreType: ScoreType; timeCapSeconds?: number };
export type SubmitScore = { primaryValue: number; secondaryValue?: number; isCapped?: boolean };

// ── The one function that actually talks HTTP ────────────────────────────────
// `userId` is sent as the dev-only X-User-Id header (see backend Auth/CurrentUser.cs).
// The `<T>` is a generic, exactly like C#.
let currentUserId: string | null = null;
export function setApiUser(id: string | null) {
  currentUserId = id;
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      // `...(cond ? {a:1} : {})` = "spread" this object in, or nothing. Adds the header only if logged in.
      ...(currentUserId ? { 'X-User-Id': currentUserId } : {}),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!res.ok) {
    // The API returns error messages as JSON strings, e.g. "Rounds must be a whole number..."
    const text = await res.text();
    throw new Error(text.replace(/^"|"$/g, '') || `HTTP ${res.status}`);
  }
  // 204 No Content has no body to parse. `as T` is a cast; TS trusts us here.
  return (res.status === 204 ? undefined : await res.json()) as T;
}

// ── One small function per endpoint. Screens import `api` and call these. ────
export const api = {
  login: (email: string, displayName: string) => request<User>('POST', '/dev/login', { email, displayName }),
  myBoards: () => request<LeaderboardSummary[]>('GET', '/me/leaderboards'),
  myInvites: () => request<Invite[]>('GET', '/me/invites'),
  acceptInvite: (boardId: string) => request<void>('POST', `/leaderboards/${boardId}/invites/accept`),
  declineInvite: (boardId: string) => request<void>('POST', `/leaderboards/${boardId}/invites/decline`),
  createBoard: (name: string, events: NewEvent[], kind: LeaderboardKind = 'Ongoing', visibility: Visibility = 'Private', startsAt?: string) =>
    request<{ id: string }>('POST', '/leaderboards', { name, events, kind, visibility, startsAt }),
  publicBoards: (q = '') => request<PublicLeaderboard[]>('GET', `/leaderboards/public?q=${encodeURIComponent(q)}`),
  join: (boardId: string) => request<void>('POST', `/leaderboards/${boardId}/join`),
  publishBoard: (boardId: string) => request<void>('POST', `/leaderboards/${boardId}/publish`),
  invite: (boardId: string, email: string) => request<void>('POST', `/leaderboards/${boardId}/invites`, { email }),
  board: (boardId: string) => request<Leaderboard>('GET', `/leaderboards/${boardId}`),
  results: (eventId: string) => request<Results>('GET', `/events/${eventId}/results`),
  submitScore: (eventId: string, score: SubmitScore) => request<{ id: string }>('PUT', `/events/${eventId}/scores/me`, score),
  heart: (scoreId: string, on: boolean) => request<void>(on ? 'PUT' : 'DELETE', `/scores/${scoreId}/heart`),
};
