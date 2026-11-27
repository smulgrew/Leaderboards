#!/usr/bin/env bash
# End-to-end smoke test of the API: invite flow, hidden results, ranking, hearts, seen-by,
# member caps, paid public boards, 30-day deletion of public one-offs, and board deletion.
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

paid() { # pay for a board: paid <board> <kind>
  local pid; pid=$(j -X POST $API/leaderboards/$1/checkout -H "$(H $ALEX)" -d "{\"kind\":\"$2\"}" | python3 -c 'import sys,json;print(json.load(sys.stdin)["paymentId"])')
  j -X POST $API/dev/payments/$pid/mark-paid; }

echo; echo "== Private board: free up to the cap (3 in this test: Alex, Sam, Jo) =="
echo "Inviting a 4th person -> $(code -X POST $API/leaderboards/$BOARD/invites -H "$(H $ALEX)" -d '{"email":"pat@example.com"}') (expect 402)"
echo "Wrong plan for a private board -> $(code -X POST $API/leaderboards/$BOARD/checkout -H "$(H $ALEX)" -d '{"kind":"PublicOngoing"}') (expect 400)"
paid $BOARD PrivateUnlimited
echo "After paying PrivateUnlimited, invite a 4th -> $(code -X POST $API/leaderboards/$BOARD/invites -H "$(H $ALEX)" -d '{"email":"pat@example.com"}') (expect 204)"

echo; echo "== Public ongoing board: paid, then anyone can join =="
GYM=$(j -X POST $API/leaderboards -H "$(H $ALEX)" -d '{"name":"Box 101 daily WODs","kind":"Ongoing","visibility":"Public"}' | id)
echo "Publish before paying -> $(code -X POST $API/leaderboards/$GYM/publish -H "$(H $ALEX)") (expect 402)"
paid $GYM PublicOngoing
j -X POST $API/leaderboards/$GYM/publish -H "$(H $ALEX)"
j -X POST $API/leaderboards/$GYM/events -H "$(H $ALEX)" -d '{"name":"Mon: 5k row","scoreType":"Time","scheduledOn":"2026-11-23"}' >/dev/null
j -X POST $API/leaderboards/$GYM/events -H "$(H $ALEX)" -d '{"name":"Fri: Grace","scoreType":"Time","scheduledOn":"2026-11-27"}' >/dev/null
echo "Pat searches 'box':"; j "$API/leaderboards/public?q=box" -H "$(H $PAT)"; echo
j -X POST $API/leaderboards/$GYM/join -H "$(H $PAT)"
echo "Events, newest day first:"; j $API/leaderboards/$GYM -H "$(H $PAT)" | python3 -c 'import sys,json;d=json.load(sys.stdin);print([(e["scheduledOn"],e["name"]) for e in d["events"]], "expiresAt:", d["expiresAt"])'
echo "Joining a private board directly -> $(code -X POST $API/leaderboards/$BOARD/join -H "$(H $PAT)") (expect 404)"

echo; echo "== Public one-off comp: paid, deleted 30 days after the event =="
COMP=$(j -X POST $API/leaderboards -H "$(H $ALEX)" -d '{"name":"Dublin Throwdown","kind":"OneOff","visibility":"Public","startsAt":"2027-03-14T09:00:00Z",
  "events":[{"name":"Event 1: 2k row","scoreType":"Time"},{"name":"Event 2: Max clean","scoreType":"Weight"}]}' | id)
echo "Publish before paying -> $(code -X POST $API/leaderboards/$COMP/publish -H "$(H $ALEX)") (expect 402)"
paid $COMP PublicOneOff
echo "Publish after paying -> $(code -X POST $API/leaderboards/$COMP/publish -H "$(H $ALEX)") (expect 204)"
j $API/leaderboards/$COMP -H "$(H $ALEX)" | python3 -c 'import sys,json;d=json.load(sys.stdin);print({k:d[k] for k in ["plan","memberLimit","startsAt","endsAt","expiresAt","requiredPaymentToPublish"]})'
echo "Cleanup as of 2027-04-12 (before expiry): $(j -X POST "$API/dev/cleanup-expired?asOf=2027-04-12T00:00:00Z")"
echo "Cleanup as of 2027-04-14 (after expiry):  $(j -X POST "$API/dev/cleanup-expired?asOf=2027-04-14T00:00:00Z")"
echo "Board after cleanup -> $(code $API/leaderboards/$COMP -H "$(H $ALEX)") (expect 404)"

echo; echo "== Finished one-off: scores closed for members =="
OLD=$(j -X POST $API/leaderboards -H "$(H $ALEX)" -d '{"name":"Last year comp","kind":"OneOff","visibility":"Private","startsAt":"2025-06-01T09:00:00Z","endsAt":"2025-06-02T09:00:00Z",
  "events":[{"name":"Chipper","scoreType":"Time"}]}' | id)
j -X POST $API/leaderboards/$OLD/publish -H "$(H $ALEX)"
OLDEV=$(j $API/leaderboards/$OLD -H "$(H $ALEX)" | python3 -c 'import sys,json;print(json.load(sys.stdin)["events"][0]["id"])')
j -X POST $API/leaderboards/$OLD/invites -H "$(H $ALEX)" -d '{"email":"pat@example.com"}'; j -X POST $API/leaderboards/$OLD/invites/accept -H "$(H $PAT)"
echo "Pat posts after it ended: $(curl -s -X PUT $API/events/$OLDEV/scores/me -H "$(H $PAT)" -H "Content-Type: application/json" -d '{"primaryValue":900}')"
echo "Admin corrects a score after it ended -> $(code -X PUT $API/events/$OLDEV/scores/me -H "$(H $ALEX)" -d '{"primaryValue":880}') (expect 200)"

echo; echo "== Owner deletes a board =="
echo "Member tries -> $(code -X DELETE $API/leaderboards/$OLD -H "$(H $PAT)") (expect 403); owner -> $(code -X DELETE $API/leaderboards/$OLD -H "$(H $ALEX)") (expect 204)"
echo "SMOKE TEST PASSED"
