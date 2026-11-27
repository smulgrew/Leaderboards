// Create a draft board by hand. (Later the AI image import will pre-fill this same form.)
import { router } from 'expo-router';
import { useState } from 'react';
import { ScrollView, Text, TextInput, View } from 'react-native';
import { api, type NewEvent, type ScoreType } from '../../api/client';
import { Button, colors, s } from '../../lib/ui';

const SCORE_TYPES: ScoreType[] = ['Time', 'Reps', 'Weight', 'RoundsReps', 'Distance', 'Calories'];

export default function NewBoardScreen() {
  const [name, setName] = useState('');
  const [events, setEvents] = useState<NewEvent[]>([{ name: '', scoreType: 'Time' }]);
  const [error, setError] = useState<string | null>(null);

  // State must be treated as immutable: build a NEW array instead of editing in place,
  // otherwise React can't tell anything changed and won't re-render.
  function updateEvent(index: number, patch: Partial<NewEvent>) {
    setEvents(events.map((e, i) => (i === index ? { ...e, ...patch } : e)));
  }

  async function save() {
    try {
      const filled = events.filter((e) => e.name.trim() !== '');
      const { id } = await api.createBoard(name, filled);
      router.replace(`/boards/${id}`);
    } catch (e) {
      setError((e as Error).message);
    }
  }

  return (
    <ScrollView style={{ backgroundColor: colors.bg }} contentContainerStyle={s.screen}>
      <TextInput style={s.input} placeholder="Board name, e.g. Friday Hyrox sim" placeholderTextColor="#666"
        value={name} onChangeText={setName} />
      <Text style={s.h2}>Events</Text>
      {events.map((ev, i) => (
        <View key={i} style={s.card}>
          <TextInput style={s.input} placeholder={`Event ${i + 1}, e.g. 1km SkiErg`} placeholderTextColor="#666"
            value={ev.name} onChangeText={(t) => updateEvent(i, { name: t })} />
          {/* A simple "chip" picker for score type. */}
          <View style={[s.row, { flexWrap: 'wrap' }]}>
            {SCORE_TYPES.map((t) => (
              <Button key={t} title={t} variant={ev.scoreType === t ? 'primary' : 'ghost'}
                onPress={() => updateEvent(i, { scoreType: t })} />
            ))}
          </View>
        </View>
      ))}
      <Button variant="ghost" title="+ Add event" onPress={() => setEvents([...events, { name: '', scoreType: 'Time' }])} />
      {error && <Text style={s.error}>{error}</Text>}
      <Button title="Save draft" onPress={save} disabled={!name.trim()} />
      <Text style={s.muted}>Drafts are only visible to you until you publish.</Text>
    </ScrollView>
  );
}
