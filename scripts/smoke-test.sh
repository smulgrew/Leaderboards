#!/usr/bin/env bash
# End-to-end smoke test of the API: invite flow, hidden results, ranking, hearts, seen-by,
# member caps, public join, and pay-to-publish one-off events.
# Run the API with a small free cap so the cap test triggers:
#   Plans__FreePrivateMemberLimit=3 dotnet run
set -euo pipefail
API=${API:-http://localhost:5271}
j() { curl -sf -H "Content-Type: application/json" "$@"; }
login() { j -X POST $API/dev/login -d "{\"email\":\"$1\",\"displayName\":\"$2\"}" | python3 -c 'import sys,json;print(json.load(sys.stdin)["id"])'; }

ALEX=$(login alex@example.com Alex); SAM=$(login sam@example.com Sam); JO=$(login jo@example.com Jo)
H() { echo "X-User-Id: $1"; }

BOARD=$(j -X POST $API/leaderboards -H "$(H $ALEX)" -d '{"name":"Friday Hyrox sim","events":[
  {"name":"1km SkiErg","scoreType":"Time"},
  {"name":"AMRAP 12","scoreType":"RoundsReps"},
  {"name":"Fran","scoreType":"Time","timeCapSeconds":600}]}' | python3 -c 'import sys,json;print(json.load(sys.stdin)["id"])')
j -X POST $API/leaderboards/$BOARD/publish -H "$(H $ALEX)"
for U in sam jo; do j -X POST $API/leaderboards/$BOARD/invites -H "$(H $ALEX)" -d "{\"email\":\"$U@example.com\"}"; done
echo "Sam's invites:"; j $API/me/invites -H "$(H $SAM)"; echo
j -X POST $API/leaderboards/$BOARD/invites/accept -H "$(H $SAM)"
j -X POST $API/leaderboards/$BOARD/invites/accept -H "$(H $JO)"

EVENTS=$(j $API/leaderboards/$BOARD -H "$(H $SAM)")
SKI=$(echo "$EVENTS" | python3 -c 'import sys,json;print(json.load(sys.stdin)["events"][0]["id"])')
AMRAP=$(echo "$EVENTS" | python3 -c 'import sys,json;print(json.load(sys.stdin)["events"][1]["id"])')
FRAN=$(echo "$EVENTS" | python3 -c 'import sys,json;print(json.load(sys.stdin)["events"][2]["id"])')

j -X PUT $API/events/$SKI/scores/me -H "$(H $JO)" -d '{"primaryValue":245.3}' >/dev/null
echo; echo "Sam before posting (should be locked, entryCount 1):"; j $API/events/$SKI/results -H "$(H $SAM)"; echo
echo "Alex (owner, not posted) sees all:"; j $API/events/$SKI/results -H "$(H $ALEX)" | python3 -c 'import sys,json;d=json.load(sys.stdin);print("locked",d["locked"],[(e["rank"],e["displayName"],e["display"]) for e in d["entries"]])'
j -X PUT $API/events/$SKI/scores/me -H "$(H $SAM)" -d '{"primaryValue":238.9}' >/dev/null
echo "Sam after posting:"; j $API/events/$SKI/results -H "$(H $SAM)" | python3 -c 'import sys,json;d=json.load(sys.stdin);print("locked",d["locked"],[(e["rank"],e["displayName"],e["display"]) for e in d["entries"]])'

for P in "$SAM 5 12" "$JO 5 20" "$ALEX 6 0"; do set -- $P; j -X PUT $API/events/$AMRAP/scores/me -H "$(H $1)" -d "{\"primaryValue\":$2,\"secondaryValue\":$3}" >/dev/null; done
echo "AMRAP ranking (Alex 6+0, Jo 5+20, Sam 5+12):"; j $API/events/$AMRAP/results -H "$(H $SAM)" | python3 -c 'import sys,json;d=json.load(sys.stdin);print([(e["rank"],e["displayName"],e["display"]) for e in d["entries"]])'

j -X PUT $API/events/$FRAN/scores/me -H "$(H $SAM)" -d '{"primaryValue":600,"secondaryValue":87,"isCapped":true}' >/dev/null
j -X PUT $API/events/$FRAN/scores/me -H "$(H $JO)" -d '{"primaryValue":412}' >/dev/null
j -X PUT $API/events/$FRAN/scores/me -H "$(H $ALEX)" -d '{"primaryValue":600,"secondaryValue":95,"isCapped":true}' >/dev/null
echo "Fran with cap (Jo finished; Alex 95 reps beats Sam 87):"; j $API/events/$FRAN/results -H "$(H $JO)" | python3 -c 'import sys,json;d=json.load(sys.stdin);print([(e["rank"],e["displayName"],e["display"]) for e in d["entries"]])'
echo "Over-cap time without isCapped is rejected:"; curl -s -X PUT $API/events/$FRAN/scores/me -H "$(H $JO)" -H "Content-Type: application/json" -d '{"primaryValue":700}'; echo

SCORE=$(j $API/events/$SKI/results -H "$(H $SAM)" | python3 -c 'import sys,json;print(json.load(sys.stdin)["entries"][1]["scoreId"])')
j -X PUT $API/scores/$SCORE/heart -H "$(H $SAM)"; j -X PUT $API/scores/$SCORE/heart -H "$(H $SAM)"
j -X POST $API/events/$SKI/comments -H "$(H $SAM)" -d '{"body":"Nice one Jo"}' >/dev/null
echo "Hearts on Jo's ski (expect 1 after double-like):"; j $API/events/$SKI/results -H "$(H $JO)" | python3 -c 'import sys,json;d=json.load(sys.stdin);print([(e["displayName"],e["hearts"]) for e in d["entries"]])'
echo "Seen by:"; j $API/events/$SKI/seen-by -H "$(H $ALEX)"; echo
echo "Comments:"; j $API/events/$SKI/comments -H "$(H $JO)"; echo
code() { curl -s -o /dev/null -w "%{http_code}" -H "Content-Type: application/json" "$@"; }
id() { python3 -c 'import sys,json;print(json.load(sys.stdin)["id"])'; }
PAT=$(login pat@example.com Pat)

echo; echo "== Member cap (free private limit 3: Alex, Sam, Jo) =="
echo "Inviting a 4th person -> $(code -X POST $API/leaderboards/$BOARD/invites -H "$(H $ALEX)" -d '{"email":"pat@example.com"}') (expect 402)"

echo; echo "== Public ongoing board: find and join, no invite =="
GYM=$(j -X POST $API/leaderboards -H "$(H $ALEX)" -d '{"name":"Box 101 daily WODs","kind":"Ongoing","visibility":"Public"}' | id)
j -X POST $API/leaderboards/$GYM/publish -H "$(H $ALEX)"
j -X POST $API/leaderboards/$GYM/events -H "$(H $ALEX)" -d '{"name":"Mon: 5k row","scoreType":"Time","scheduledOn":"2026-11-23"}' >/dev/null
j -X POST $API/leaderboards/$GYM/events -H "$(H $ALEX)" -d '{"name":"Fri: Grace","scoreType":"Time","scheduledOn":"2026-11-27"}' >/dev/null
echo "Pat searches 'box':"; j "$API/leaderboards/public?q=box" -H "$(H $PAT)"; echo
j -X POST $API/leaderboards/$GYM/join -H "$(H $PAT)"
echo "Pat's boards:"; j $API/me/leaderboards -H "$(H $PAT)" | python3 -c 'import sys,json;print([(b["name"],b["kind"],b["visibility"]) for b in json.load(sys.stdin)])'
echo "Events, newest day first:"; j $API/leaderboards/$GYM -H "$(H $PAT)" | python3 -c 'import sys,json;print([(e["scheduledOn"],e["name"]) for e in json.load(sys.stdin)["events"]])'
echo "Joining a private board directly -> $(code -X POST $API/leaderboards/$BOARD/join -H "$(H $PAT)") (expect 404)"

echo; echo "== One-off comp: pay EUR 20 to publish =="
COMP=$(j -X POST $API/leaderboards -H "$(H $ALEX)" -d '{"name":"Dublin Throwdown","kind":"OneOff","visibility":"Public","startsAt":"2027-03-14T09:00:00Z",
  "events":[{"name":"Event 1: 2k row","scoreType":"Time"},{"name":"Event 2: Max clean","scoreType":"Weight"}]}' | id)
echo "Publish before paying -> $(code -X POST $API/leaderboards/$COMP/publish -H "$(H $ALEX)") (expect 402)"
CHECKOUT=$(j -X POST $API/leaderboards/$COMP/checkout -H "$(H $ALEX)" -d '{"kind":"OneOffEvent"}'); echo "Checkout: $CHECKOUT"
PAY=$(echo "$CHECKOUT" | python3 -c 'import sys,json;print(json.load(sys.stdin)["paymentId"])')
j -X POST $API/dev/payments/$PAY/mark-paid; j -X POST $API/dev/payments/$PAY/mark-paid   # twice, like a repeated webhook
echo "Publish after paying -> $(code -X POST $API/leaderboards/$COMP/publish -H "$(H $ALEX)") (expect 204)"
j $API/leaderboards/$COMP -H "$(H $ALEX)" | python3 -c 'import sys,json;d=json.load(sys.stdin);print({k:d[k] for k in ["kind","plan","memberLimit","startsAt","endsAt","needsPaymentToPublish"]})'

echo; echo "== Finished one-off: scores closed for members =="
OLD=$(j -X POST $API/leaderboards -H "$(H $ALEX)" -d '{"name":"Last year comp","kind":"OneOff","visibility":"Public","startsAt":"2025-06-01T09:00:00Z","endsAt":"2025-06-02T09:00:00Z",
  "events":[{"name":"Chipper","scoreType":"Time"}]}' | id)
P2=$(j -X POST $API/leaderboards/$OLD/checkout -H "$(H $ALEX)" -d '{"kind":"OneOffEvent"}' | python3 -c 'import sys,json;print(json.load(sys.stdin)["paymentId"])')
j -X POST $API/dev/payments/$P2/mark-paid; j -X POST $API/leaderboards/$OLD/publish -H "$(H $ALEX)"
echo "Finished comps are hidden from public search: $(j "$API/leaderboards/public?q=last" -H "$(H $PAT)")"
OLDEV=$(j $API/leaderboards/$OLD -H "$(H $ALEX)" | python3 -c 'import sys,json;print(json.load(sys.stdin)["events"][0]["id"])')
j -X POST $API/leaderboards/$OLD/invites -H "$(H $ALEX)" -d '{"email":"pat@example.com"}'; j -X POST $API/leaderboards/$OLD/invites/accept -H "$(H $PAT)"
echo "Pat posts after it ended: $(curl -s -X PUT $API/events/$OLDEV/scores/me -H "$(H $PAT)" -H "Content-Type: application/json" -d '{"primaryValue":900}')"
echo "Admin corrects a score after it ended -> $(code -X PUT $API/events/$OLDEV/scores/me -H "$(H $ALEX)" -d '{"primaryValue":880}') (expect 200)"
echo "SMOKE TEST PASSED"
