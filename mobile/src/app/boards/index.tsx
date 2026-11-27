// "My boards": pending invites (accept/decline) on top, then boards I'm in.
import { Link, router, Stack, useFocusEffect } from 'expo-router';
import { useCallback, useState } from 'react';
import { FlatList, Pressable, RefreshControl, Text, View } from 'react-native';
import { api, type Invite, type LeaderboardSummary } from '../../api/client';
import { useSession } from '../../lib/session';
import { Button, s } from '../../lib/ui';

export default function BoardsScreen() {
  const { user, signOut } = useSession();
  const [boards, setBoards] = useState<LeaderboardSummary[]>([]);
  const [invites, setInvites] = useState<Invite[]>([]);
  const [loading, setLoading] = useState(false);

  // useCallback memoises the function so it isn't recreated on every render.
  const load = useCallback(async () => {
    setLoading(true);
    try {
      // Promise.all = Task.WhenAll: fire both requests at once, wait for both.
      const [b, i] = await Promise.all([api.myBoards(), api.myInvites()]);
      setBoards(b);
      setInvites(i);
    } finally {
      setLoading(false);
    }
  }, []);

  // Runs `load` every time this screen comes into view (incl. coming "back" to it),
  // so a board you just created or joined shows up straight away.
  useFocusEffect(useCallback(() => { if (user) load(); }, [user, load]));

  async function respond(invite: Invite, accept: boolean) {
    await (accept ? api.acceptInvite(invite.leaderboardId) : api.declineInvite(invite.leaderboardId));
    load();
  }

  return (
    <View style={s.screen}>
      {/* Screens can tweak their own header. Here: a sign-out button. */}
      <Stack.Screen options={{
        headerRight: () => <Button variant="ghost" title="Sign out" onPress={() => { signOut(); router.replace('/'); }} />,
      }} />

      {/* .map() turns each invite into a card. React needs a unique `key` per item in a list. */}
      {invites.map((inv) => (
        <View key={inv.leaderboardId} style={s.card}>
          <Text style={s.h2}>{inv.leaderboardName}</Text>
          <Text style={s.muted}>{inv.invitedBy} invited you</Text>
          <View style={s.row}>
            <Button title="Accept" onPress={() => respond(inv, true)} />
            <Button title="Decline" variant="ghost" onPress={() => respond(inv, false)} />
          </View>
        </View>
      ))}

      {/* FlatList is the efficient scrolling list (only renders what's on screen). */}
      <FlatList
        data={boards}
        keyExtractor={(b) => b.id}
        contentContainerStyle={{ gap: 10 }}
        refreshControl={<RefreshControl refreshing={loading} onRefresh={load} tintColor="#fff" />}
        ListEmptyComponent={<Text style={s.muted}>No boards yet. Create one or wait for an invite.</Text>}
        renderItem={({ item }) => (
          // <Link asChild> makes the Pressable inside navigate to /boards/<id> when tapped.
          <Link href={`/boards/${item.id}`} asChild>
            <Pressable style={s.card}>
              <Text style={s.h2}>{item.name}</Text>
              <Text style={s.muted}>
                {item.eventCount} events · {item.status}{item.myRole !== 'Member' ? ` · ${item.myRole}` : ''}
              </Text>
            </Pressable>
          </Link>
        )}
      />
      <Button title="+ New board" onPress={() => router.push('/boards/new')} />
    </View>
  );
}
