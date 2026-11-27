// ─────────────────────────────────────────────────────────────────────────────
// Expo Router: every file in src/app/ is a screen, and its path is its URL.
//   src/app/index.tsx          -> "/"              (login)
//   src/app/boards/index.tsx   -> "/boards"        (my boards + invites)
//   src/app/boards/new.tsx     -> "/boards/new"    (create a board)
//   src/app/boards/[id].tsx    -> "/boards/123"    ([id] = route parameter)
//   src/app/events/[id].tsx    -> "/events/456"    (results + submit score)
// _layout.tsx wraps all of them. Here: a Stack navigator (push/pop screens with a
// back button), wrapped in our SessionProvider so every screen can see the user.
// ─────────────────────────────────────────────────────────────────────────────
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { SessionProvider } from '../lib/session';
import { colors } from '../lib/ui';

export default function RootLayout() {
  return (
    <SessionProvider>
      <StatusBar style="light" />
      <Stack
        screenOptions={{
          headerStyle: { backgroundColor: colors.bg },
          headerTintColor: colors.text,
          contentStyle: { backgroundColor: colors.bg },
        }}
      >
        {/* Titles per screen. Screens can also set their own with <Stack.Screen options=.../> */}
        <Stack.Screen name="index" options={{ title: 'LeadersBoards', headerShown: false }} />
        <Stack.Screen name="boards/index" options={{ title: 'My boards', headerBackVisible: false }} />
        <Stack.Screen name="boards/new" options={{ title: 'New board', presentation: 'modal' }} />
      </Stack>
    </SessionProvider>
  );
}
