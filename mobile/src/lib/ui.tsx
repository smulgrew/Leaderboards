// Tiny shared UI pieces and styles, so screens stay short.
// StyleSheet.create is like CSS, but camelCase and numbers are density-independent pixels.
// Layout is flexbox, and the default direction is a COLUMN (unlike the web).
import { Pressable, StyleSheet, Text, type PressableProps } from 'react-native';

export const colors = { bg: '#0f1115', card: '#1a1d24', text: '#f2f3f5', muted: '#8b90a0', accent: '#ffcc00', danger: '#ff5c5c' };

export const s = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.bg, padding: 16, gap: 12 },
  card: { backgroundColor: colors.card, borderRadius: 12, padding: 14, gap: 6 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  h1: { color: colors.text, fontSize: 24, fontWeight: '700' },
  h2: { color: colors.text, fontSize: 17, fontWeight: '600' },
  text: { color: colors.text, fontSize: 15 },
  muted: { color: colors.muted, fontSize: 13 },
  error: { color: colors.danger, fontSize: 14 },
  input: { backgroundColor: colors.card, color: colors.text, borderRadius: 10, padding: 12, fontSize: 16 },
});

// `{ title, ...rest }` pulls `title` out and collects every other prop into `rest`,
// which we pass straight through to Pressable (so onPress, disabled, etc. just work).
export function Button({ title, variant = 'primary', ...rest }: PressableProps & { title: string; variant?: 'primary' | 'ghost' }) {
  const primary = variant === 'primary';
  return (
    <Pressable
      {...rest}
      style={({ pressed }) => [{
        backgroundColor: primary ? colors.accent : 'transparent',
        borderColor: colors.accent, borderWidth: 1, borderRadius: 10,
        paddingVertical: 10, paddingHorizontal: 14, alignItems: 'center',
        opacity: pressed || rest.disabled ? 0.6 : 1,
      }]}
    >
      <Text style={{ color: primary ? '#000' : colors.accent, fontWeight: '600' }}>{title}</Text>
    </Pressable>
  );
}
