// Turns what the athlete types into the numbers the API stores.
// Pure functions, no React: the easiest kind of JS to read and test.
import type { ScoreType, SubmitScore } from '../api/client';

/** "4:05.3" -> 245.3, "1:02:10" -> 3730, "95" -> 95. Returns NaN if it can't parse. */
export function parseTime(text: string): number {
  const parts = text.trim().split(':').map(Number); // ["4","05.3"] -> [4, 5.3]
  if (parts.some(Number.isNaN)) return NaN;
  // reduce = LINQ Aggregate. Each ':' shifts the running total up by 60.
  return parts.reduce((total, part) => total * 60 + part, 0);
}

/** What placeholder text to show in the score box. */
export function scoreHint(type: ScoreType): string {
  switch (type) {
    case 'Time': return 'mm:ss, e.g. 4:05.3';
    case 'RoundsReps': return 'rounds+reps, e.g. 5+12';
    case 'Weight': return 'kg, e.g. 102.5';
    case 'Distance': return 'metres';
    case 'Calories': return 'calories';
    default: return 'reps';
  }
}

/** Builds the request body, or returns an error string for the user. */
export function toSubmitScore(type: ScoreType, text: string): SubmitScore | string {
  if (type === 'Time') {
    const seconds = parseTime(text);
    return Number.isNaN(seconds) ? 'Use mm:ss, e.g. 4:05' : { primaryValue: seconds };
  }
  if (type === 'RoundsReps') {
    const [rounds, reps = '0'] = text.split('+'); // array destructuring with a default
    const r = parseInt(rounds, 10), x = parseInt(reps, 10);
    return Number.isNaN(r) || Number.isNaN(x) ? 'Use rounds+reps, e.g. 5+12' : { primaryValue: r, secondaryValue: x };
  }
  const n = Number(text);
  return Number.isNaN(n) || text.trim() === '' ? 'Enter a number' : { primaryValue: n };
}
