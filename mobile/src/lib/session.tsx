// ─────────────────────────────────────────────────────────────────────────────
// "Who is logged in", shared with every screen.
//
// React concepts used here:
//  • A component is just a function that returns UI (JSX: the HTML-looking syntax).
//  • `useState` gives a component a variable that, when you change it, re-renders
//    the UI. Think of it as a property with INotifyPropertyChanged built in.
//  • Context is React's dependency injection: <SessionProvider> at the top of the
//    app "provides" the user, and any screen below calls useSession() to get it.
// ─────────────────────────────────────────────────────────────────────────────
import { createContext, useContext, useState, type ReactNode } from 'react';
import { setApiUser, type User } from '../api/client';

type Session = { user: User | null; signIn: (u: User) => void; signOut: () => void };

const SessionContext = createContext<Session | null>(null);

// `{ children }: { children: ReactNode }` = destructure the props object.
// `children` is whatever JSX is nested inside <SessionProvider>...</SessionProvider>.
export function SessionProvider({ children }: { children: ReactNode }) {
  // [value, setter]. Calling setUser(...) triggers a re-render of everything using it.
  // Note: this lives in memory only; restarting the app logs you out. Swap in
  // expo-secure-store when real auth arrives.
  const [user, setUser] = useState<User | null>(null);

  const session: Session = {
    user,
    signIn: (u) => { setApiUser(u.id); setUser(u); },
    signOut: () => { setApiUser(null); setUser(null); },
  };
  return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>;
}

// A "hook" is any function starting with `use` that calls other hooks.
// Rule: only call hooks at the top level of a component, never inside if/loops.
export function useSession(): Session {
  const s = useContext(SessionContext);
  if (!s) throw new Error('useSession must be used inside <SessionProvider>');
  return s;
}
