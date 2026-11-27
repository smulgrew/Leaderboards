// One board: its events, plus admin actions (publish, invite).
import { Link, Stack, useFocusEffect, useLocalSearchParams } from 'expo-router';
import { useCallback, useState } from 'react';
import { Pressable, ScrollView, Text, TextInput, View } from 'react-native';
import { api, type Leaderboard } from '../../api/client';
import { Button, colors, s } from '../../lib/ui';

export default function BoardScreen() {
  // Reads the [id] part of the URL. Generic param = the shape we expect.
  const { id } = useLocalSearchParams<{ id: string }>();
  const [board, setBoard] = useState<Leaderboard | null>(null);
  const [inviteEmail, setInviteEmail] = useState('');
  const [message, setMessage] = useState<string | null>(null);

  const load = useCallback(() => { api.board(id).then(setBoard); }, [id]);
  useFocusEffect(load);

  // Until the first fetch finishes, render a placeholder (board is null).
  if (!board) return <View style={s.screen}><Text style={s.muted}>Loading…</Text></View>;

  const isAdmin = board.myRole !== 'Member';

  async function run(action: () => Promise<void>, done: string) {
    try { await action(); setMessage(done); load(); }
    catch (e) { setMessage((e as Error).message); }
  }

  return (
    <ScrollView style={{ backgroundColor: colors.bg }} contentContainerStyle={s.screen}>
      <Stack.Screen options={{ title: board.name }} />
      {board.status === 'Draft' && <Text style={s.muted}>Draft: only admins can see this board.</Text>}

      {board.events.map((ev) => (
        <Link key={ev.id} href={`/events/${ev.id}`} asChild>
          <Pressable style={s.card}>
            <Text style={s.h2}>{ev.name}</Text>
            <Text style={s.muted}>{ev.scoreType}{ev.iSubmitted ? ' · ✅ you posted' : ' · 🔒 post to see results'}</Text>
          </Pressable>
        </Link>
      ))}

      {isAdmin && (
        <View style={s.card}>
          <Text style={s.h2}>Admin</Text>
          {board.status === 'Draft' && (
            <Button title="Publish board" onPress={() => run(() => api.publishBoard(board.id), 'Published!')} />
          )}
          <TextInput style={s.input} placeholder="Invite by email" placeholderTextColor="#666" autoCapitalize="none"
            value={inviteEmail} onChangeText={setInviteEmail} />
          <Button variant="ghost" title="Send invite" disabled={!inviteEmail.includes('@')}
            onPress={() => run(() => api.invite(board.id, inviteEmail), `Invited ${inviteEmail}`)} />
        </View>
      )}
      {message && <Text style={s.muted}>{message}</Text>}
    </ScrollView>
  );
}
