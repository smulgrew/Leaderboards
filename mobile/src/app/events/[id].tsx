// One event: submit your score, then see the ranked results (hidden until you post).
import { Stack, useFocusEffect, useLocalSearchParams } from 'expo-router';
import { useCallback, useState } from 'react';
import { FlatList, Pressable, Text, TextInput, View } from 'react-native';
import { api, type ResultEntry, type Results } from '../../api/client';
import { scoreHint, toSubmitScore } from '../../lib/scoreInput';
import { useSession } from '../../lib/session';
import { Button, colors, s } from '../../lib/ui';

export default function EventScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { user } = useSession();
  const [results, setResults] = useState<Results | null>(null);
  const [scoreText, setScoreText] = useState('');
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => { api.results(id).then(setResults); }, [id]);
  useFocusEffect(load);

  if (!results) return <View style={s.screen}><Text style={s.muted}>Loading…</Text></View>;

  async function submit() {
    const body = toSubmitScore(results!.scoreType, scoreText); // `!` = "I promise it's not null"
    if (typeof body === 'string') return setError(body);
    try {
      await api.submitScore(id, body);
      setError(null);
      setScoreText('');
      load(); // re-fetch: the API now unlocks the results for us
    } catch (e) {
      setError((e as Error).message);
    }
  }

  async function toggleHeart(entry: ResultEntry) {
    await api.heart(entry.scoreId, !entry.heartedByMe);
    load();
  }

  const mine = results.entries.find((e) => e.userId === user?.id);

  return (
    <View style={s.screen}>
      <Stack.Screen options={{ title: results.eventName }} />

      {/* Submit / update box. Always shown, so you can correct your score. */}
      <View style={s.card}>
        <Text style={s.h2}>{mine ? `Your score: ${mine.display}` : 'Post your score'}</Text>
        <View style={s.row}>
          <TextInput style={[s.input, { flex: 1 }]} placeholder={scoreHint(results.scoreType)} placeholderTextColor="#666"
            value={scoreText} onChangeText={setScoreText} keyboardType="numbers-and-punctuation" />
          <Button title={mine ? 'Update' : 'Submit'} onPress={submit} disabled={!scoreText.trim()} />
        </View>
        {error && <Text style={s.error}>{error}</Text>}
      </View>

      {results.locked ? (
        // The core mechanic: no peeking until you've posted.
        <View style={[s.card, { alignItems: 'center', paddingVertical: 30 }]}>
          <Text style={{ fontSize: 40 }}>🔒</Text>
          <Text style={s.h2}>{results.entryCount} {results.entryCount === 1 ? 'person has' : 'people have'} posted</Text>
          <Text style={s.muted}>Post your score to see the leaderboard.</Text>
        </View>
      ) : (
        <FlatList
          data={results.entries}
          keyExtractor={(e) => e.scoreId}
          contentContainerStyle={{ gap: 8 }}
          renderItem={({ item }) => (
            <View style={[s.card, s.row, item.userId === user?.id && { borderColor: colors.accent, borderWidth: 1 }]}>
              <Text style={[s.h2, { width: 32 }]}>{item.rank}</Text>
              <View style={{ flex: 1 }}>
                <Text style={s.text}>{item.displayName}</Text>
                <Text style={s.muted}>{item.display}{item.isRx ? '' : ' · scaled'}</Text>
              </View>
              <Pressable onPress={() => toggleHeart(item)} hitSlop={10}>
                <Text style={s.text}>{item.heartedByMe ? '❤️' : '🤍'} {item.hearts}</Text>
              </Pressable>
            </View>
          )}
        />
      )}
    </View>
  );
}
