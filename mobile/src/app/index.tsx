// Login screen (dev-only: email + name, no password).
import { router } from 'expo-router';
import { useState } from 'react';
import { Text, TextInput, View } from 'react-native';
import { api, API_URL } from '../api/client';
import { useSession } from '../lib/session';
import { Button, s } from '../lib/ui';

export default function LoginScreen() {
  const { signIn } = useSession();
  // One useState per form field. TextInput is "controlled": it shows `email`, and
  // every keystroke calls setEmail, which re-renders with the new value.
  const [email, setEmail] = useState('');
  const [name, setName] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit() {
    setBusy(true);
    setError(null);
    try {
      const user = await api.login(email, name || email.split('@')[0]);
      signIn(user);
      router.replace('/boards'); // replace (not push) so "back" doesn't return to login
    } catch (e) {
      setError(`Couldn't reach the API at ${API_URL}. Is it running? (${(e as Error).message})`);
    } finally {
      setBusy(false);
    }
  }

  // JSX: <View> is a <div>, <Text> is the ONLY place text may appear in React Native.
  // `{expr}` drops a JS value into the markup. `cond && <X/>` renders X only if cond is true.
  return (
    <View style={[s.screen, { justifyContent: 'center' }]}>
      <Text style={s.h1}>LeadersBoards</Text>
      <Text style={s.muted}>Dev sign-in. No password yet.</Text>
      <TextInput style={s.input} placeholder="Email" placeholderTextColor="#666" autoCapitalize="none"
        keyboardType="email-address" value={email} onChangeText={setEmail} />
      <TextInput style={s.input} placeholder="Display name" placeholderTextColor="#666"
        value={name} onChangeText={setName} />
      {error && <Text style={s.error}>{error}</Text>}
      <Button title={busy ? 'Signing in…' : 'Sign in'} onPress={onSubmit} disabled={busy || !email.includes('@')} />
    </View>
  );
}
